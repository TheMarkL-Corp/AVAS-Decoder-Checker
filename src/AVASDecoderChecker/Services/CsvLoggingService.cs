using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AVASDecoderChecker.Models;

namespace AVASDecoderChecker.Services
{
    public class CsvLoggingService : ILoggingService
    {
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private readonly ConcurrentBag<DecoderTelemetrySample> _sessionSamples = new();
        private DateTime _sessionStartTime;
        private string? _baseLogsDir;

        public string? CurrentSessionDirectory { get; private set; }
        public string? CurrentSessionId { get; private set; }
        public bool IsSessionActive => !string.IsNullOrEmpty(CurrentSessionDirectory);

        private const string MasterCsvHeader =
            "Timestamp,DecoderName,MAC,IP,OverallStatus,FaultAttribution,SourceEncoderStatus,IsAnomaly,RecoveryTimeSec,BlackoutDurationSec,VideoReceivedStatus,DisplayScreenStatus,SourceEncoderMac,MulticastAddress,ClockStable,VideoRaster,PixelClockMhz,MultiLinkStatus,MultiLinkMode,NetworkPortSpeed,TemperatureC,DisplayModel,DisplaySerial,HpdConnected,TmdsActive,HdcpBlocked,StreamingActive,DisplayConnected,StreamPushingToDisplay,Notes";

        private const string DecoderCsvHeader =
            "Timestamp,OverallStatus,FaultAttribution,SourceEncoderStatus,IsAnomaly,RecoveryTimeSec,BlackoutDurationSec,VideoReceivedStatus,DisplayScreenStatus,SourceEncoderMac,MulticastAddress,ClockStable,VideoRaster,PixelClockMhz,MultiLinkStatus,MultiLinkMode,NetworkPortSpeed,TemperatureC,DisplayModel,DisplaySerial,HpdConnected,TmdsActive,HdcpBlocked,StreamingActive,DisplayConnected,StreamPushingToDisplay,Notes";

        private const string EncoderCsvHeader =
            "Timestamp,EncoderName,MAC,IP,OverallStatus,SourceStable,HasVideo,VideoRaster,PixelClockMhz,StreamState,MulticastAddress,LinkMode,LinkStatus,Temperature,ErrorCode,Notes";

        private const string AnomalyCsvHeader =
            "Timestamp,EventType,ElapsedSeconds,EncoderMAC,AffectedDecoderName,AffectedDecoderMAC,Description";

        public CsvLoggingService(string? customBaseLogsDir = null)
        {
            _baseLogsDir = customBaseLogsDir;
        }

        public async Task<string> StartNewSessionAsync(string? baseLogsDirectory = null)
        {
            await _writeLock.WaitAsync();
            try
            {
                string root = baseLogsDirectory ?? _baseLogsDir ?? Path.Combine(Directory.GetCurrentDirectory(), "logs");
                if (!Directory.Exists(root))
                {
                    Directory.CreateDirectory(root);
                }

                _sessionStartTime = DateTime.Now;
                CurrentSessionId = $"Test_{_sessionStartTime:yyyyMMdd_HHmmss}";
                CurrentSessionDirectory = Path.Combine(root, CurrentSessionId);

                Directory.CreateDirectory(CurrentSessionDirectory);
                Directory.CreateDirectory(Path.Combine(CurrentSessionDirectory, "decoders"));

                // Clear memory samples from prior run
                while (_sessionSamples.TryTake(out _)) { }

                // Create master telemetry CSV with header
                string masterPath = Path.Combine(CurrentSessionDirectory, "master_telemetry.csv");
                await File.WriteAllTextAsync(masterPath, MasterCsvHeader + Environment.NewLine, Encoding.UTF8);

                // Create encoder telemetry CSV with header
                string encoderPath = Path.Combine(CurrentSessionDirectory, "encoder_telemetry.csv");
                await File.WriteAllTextAsync(encoderPath, EncoderCsvHeader + Environment.NewLine, Encoding.UTF8);

                // Create anomaly events CSV with header
                string anomalyPath = Path.Combine(CurrentSessionDirectory, "anomaly_events.csv");
                await File.WriteAllTextAsync(anomalyPath, AnomalyCsvHeader + Environment.NewLine, Encoding.UTF8);

                return CurrentSessionDirectory;
            }
            finally
            {
                _writeLock.Release();
            }
        }

        public async Task LogEncoderSampleAsync(EncoderTelemetrySample sample)
        {
            if (sample == null || string.IsNullOrEmpty(CurrentSessionDirectory)) return;

            string line = FormatEncoderCsvLine(sample);
            string encoderPath = Path.Combine(CurrentSessionDirectory, "encoder_telemetry.csv");

            await _writeLock.WaitAsync();
            try
            {
                if (!File.Exists(encoderPath))
                {
                    await File.WriteAllTextAsync(encoderPath, EncoderCsvHeader + Environment.NewLine, Encoding.UTF8);
                }
                await File.AppendAllTextAsync(encoderPath, line + Environment.NewLine, Encoding.UTF8);
            }
            finally
            {
                _writeLock.Release();
            }
        }

        public async Task LogAnomalyEventAsync(AnomalyEvent anomaly)
        {
            if (anomaly == null || string.IsNullOrEmpty(CurrentSessionDirectory)) return;

            string line = FormatAnomalyCsvLine(anomaly);
            string anomalyPath = Path.Combine(CurrentSessionDirectory, "anomaly_events.csv");

            await _writeLock.WaitAsync();
            try
            {
                if (!File.Exists(anomalyPath))
                {
                    await File.WriteAllTextAsync(anomalyPath, AnomalyCsvHeader + Environment.NewLine, Encoding.UTF8);
                }
                await File.AppendAllTextAsync(anomalyPath, line + Environment.NewLine, Encoding.UTF8);
            }
            finally
            {
                _writeLock.Release();
            }
        }

        public async Task LogSampleAsync(DecoderTelemetrySample sample)
        {
            if (sample == null || string.IsNullOrEmpty(CurrentSessionDirectory)) return;

            _sessionSamples.Add(sample);

            string masterLine = FormatMasterCsvLine(sample);
            string decoderLine = FormatDecoderCsvLine(sample);

            string sanitizedMac = SanitizeFileName(string.IsNullOrWhiteSpace(sample.MacAddress) ? "NoMAC" : sample.MacAddress);
            string sanitizedName = SanitizeFileName(string.IsNullOrWhiteSpace(sample.DecoderName) ? "Decoder" : sample.DecoderName);
            string decoderFileName = $"{sanitizedName}_{sanitizedMac}.csv";
            string decoderPath = Path.Combine(CurrentSessionDirectory, "decoders", decoderFileName);

            await _writeLock.WaitAsync();
            try
            {
                // Append to master CSV
                string masterPath = Path.Combine(CurrentSessionDirectory, "master_telemetry.csv");
                await File.AppendAllTextAsync(masterPath, masterLine + Environment.NewLine, Encoding.UTF8);

                // Append to per-decoder CSV (add header if newly created)
                if (!File.Exists(decoderPath))
                {
                    await File.WriteAllTextAsync(decoderPath, DecoderCsvHeader + Environment.NewLine, Encoding.UTF8);
                }
                await File.AppendAllTextAsync(decoderPath, decoderLine + Environment.NewLine, Encoding.UTF8);
            }
            finally
            {
                _writeLock.Release();
            }
        }

        public async Task<TestSessionSummary> ConcludeSessionAsync()
        {
            await _writeLock.WaitAsync();
            try
            {
                var now = DateTime.Now;
                var allSamples = _sessionSamples.ToList();

                var summary = new TestSessionSummary
                {
                    SessionId = CurrentSessionId ?? "Unknown",
                    SessionDirectory = CurrentSessionDirectory ?? string.Empty,
                    StartTime = _sessionStartTime,
                    EndTime = now,
                    TotalSamples = allSamples.Count,
                    PassCount = allSamples.Count(s => s.OverallStatus == "PASS"),
                    WarnCount = allSamples.Count(s => s.OverallStatus == "WARN"),
                    FailCount = allSamples.Count(s => s.OverallStatus == "FAIL"),
                    MonitoredDecodersCount = allSamples.Select(s => s.MacAddress).Distinct().Count()
                };

                if (!string.IsNullOrEmpty(CurrentSessionDirectory))
                {
                    string summaryPath = Path.Combine(CurrentSessionDirectory, "summary_report.csv");
                    var sb = new StringBuilder();
                    sb.AppendLine("=== AVAS SDVoE DECODER TEST RUN SUMMARY ===");
                    sb.AppendLine($"Session ID,{EscapeCsv(summary.SessionId)}");
                    sb.AppendLine($"Start Time,{summary.StartTime:yyyy-MM-dd HH:mm:ss}");
                    sb.AppendLine($"End Time,{summary.EndTime:yyyy-MM-dd HH:mm:ss}");
                    sb.AppendLine($"Total Duration,{summary.Duration.TotalSeconds:F1} seconds ({summary.Duration:hh\\:mm\\:ss})");
                    sb.AppendLine($"Monitored Decoders,{summary.MonitoredDecodersCount}");
                    sb.AppendLine($"Total Polling Samples,{summary.TotalSamples}");
                    sb.AppendLine($"Passing Samples,{summary.PassCount}");
                    sb.AppendLine($"Warning Samples,{summary.WarnCount}");
                    sb.AppendLine($"Failing Samples,{summary.FailCount}");
                    sb.AppendLine($"Health Percentage,{summary.HealthPercentage}%");
                    sb.AppendLine();

                    sb.AppendLine("=== PER-DECODER TELEMETRY BREAKDOWN ===");
                    sb.AppendLine("DecoderName,MAC,IP,Samples,PassCount,WarnCount,FailCount,StreamingUpPercentage,DisplayConnectedPercentage,VideoPushingPercentage");

                    var groups = allSamples.GroupBy(s => s.MacAddress);
                    foreach (var g in groups)
                    {
                        var first = g.First();
                        int total = g.Count();
                        int p = g.Count(s => s.OverallStatus == "PASS");
                        int w = g.Count(s => s.OverallStatus == "WARN");
                        int f = g.Count(s => s.OverallStatus == "FAIL");
                        double streamPct = total > 0 ? (double)g.Count(s => s.IsStreaming) / total * 100.0 : 0.0;
                        double displayPct = total > 0 ? (double)g.Count(s => s.IsDisplayConnected) / total * 100.0 : 0.0;
                        double pushPct = total > 0 ? (double)g.Count(s => s.IsPushingVideo) / total * 100.0 : 0.0;

                        sb.AppendLine($"{EscapeCsv(first.DecoderName)},{EscapeCsv(first.MacAddress)},{EscapeCsv(first.IpAddress)},{total},{p},{w},{f},{streamPct:F1}%,{displayPct:F1}%,{pushPct:F1}%");
                    }

                    await File.WriteAllTextAsync(summaryPath, sb.ToString(), Encoding.UTF8);
                }

                CurrentSessionDirectory = null;
                CurrentSessionId = null;
                return summary;
            }
            finally
            {
                _writeLock.Release();
            }
        }

        private static string FormatMasterCsvLine(DecoderTelemetrySample s)
        {
            return string.Join(",",
                s.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                EscapeCsv(s.DecoderName),
                EscapeCsv(s.MacAddress),
                EscapeCsv(s.IpAddress),
                s.OverallStatus,
                EscapeCsv(s.FaultAttribution),
                EscapeCsv(s.SourceEncoderStatus),
                s.IsAnomaly,
                s.RecoveryTimeSec.ToString("F2"),
                s.BlackoutDurationSec.ToString("F2"),
                EscapeCsv(s.VideoReceivedStatus),
                EscapeCsv(s.DisplayScreenStatus),
                EscapeCsv(s.SourceEncoderMac),
                EscapeCsv(s.MulticastAddress),
                s.IsClockStable,
                EscapeCsv(s.VideoRaster),
                s.PixelClockMhz.ToString("F1"),
                EscapeCsv(s.MultiLinkStatus),
                EscapeCsv(s.MultiLinkMode),
                EscapeCsv(s.NetworkPortSpeed),
                s.TemperatureC,
                EscapeCsv(s.DisplayModelName),
                EscapeCsv(s.DisplaySerialNumber),
                s.IsHpdConnected,
                s.IsTmdsClockActive,
                s.IsHdcpBlocked,
                s.IsStreaming,
                s.IsDisplayConnected,
                s.IsPushingVideo,
                EscapeCsv(s.Notes)
            );
        }

        private static string FormatDecoderCsvLine(DecoderTelemetrySample s)
        {
            return string.Join(",",
                s.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                s.OverallStatus,
                EscapeCsv(s.FaultAttribution),
                EscapeCsv(s.SourceEncoderStatus),
                s.IsAnomaly,
                s.RecoveryTimeSec.ToString("F2"),
                s.BlackoutDurationSec.ToString("F2"),
                EscapeCsv(s.VideoReceivedStatus),
                EscapeCsv(s.DisplayScreenStatus),
                EscapeCsv(s.SourceEncoderMac),
                EscapeCsv(s.MulticastAddress),
                s.IsClockStable,
                EscapeCsv(s.VideoRaster),
                s.PixelClockMhz.ToString("F1"),
                EscapeCsv(s.MultiLinkStatus),
                EscapeCsv(s.MultiLinkMode),
                EscapeCsv(s.NetworkPortSpeed),
                s.TemperatureC,
                EscapeCsv(s.DisplayModelName),
                EscapeCsv(s.DisplaySerialNumber),
                s.IsHpdConnected,
                s.IsTmdsClockActive,
                s.IsHdcpBlocked,
                s.IsStreaming,
                s.IsDisplayConnected,
                s.IsPushingVideo,
                EscapeCsv(s.Notes)
            );
        }

        private static string FormatEncoderCsvLine(EncoderTelemetrySample s)
        {
            return string.Join(",",
                s.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                EscapeCsv(s.EncoderName),
                EscapeCsv(s.MacAddress),
                EscapeCsv(s.IpAddress),
                s.OverallStatus,
                s.IsSourceStable,
                s.HasVideo,
                EscapeCsv(s.VideoRaster),
                s.PixelClockMhz.ToString("F1"),
                EscapeCsv(s.StreamState),
                EscapeCsv(s.MulticastAddress),
                EscapeCsv(s.LinkMode),
                EscapeCsv(s.LinkStatus),
                s.TemperatureC?.ToString("F1") ?? "",
                s.ErrorCode.ToString(),
                EscapeCsv(s.Notes)
            );
        }

        private static string FormatAnomalyCsvLine(AnomalyEvent a)
        {
            return string.Join(",",
                a.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                a.EventType.ToString(),
                a.DurationSeconds.ToString("F2"),
                EscapeCsv(a.EncoderMac),
                EscapeCsv(a.AffectedDecoderName),
                EscapeCsv(a.AffectedDecoderMac),
                EscapeCsv(a.Description)
            );
        }

        private static string EscapeCsv(string? value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }
            return value;
        }

        private static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name.Replace(':', '_').Replace(' ', '_');
        }
    }
}
