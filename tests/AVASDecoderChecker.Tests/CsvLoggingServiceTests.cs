using System;
using System.IO;
using System.Threading.Tasks;
using AVASDecoderChecker.Models;
using AVASDecoderChecker.Services;
using Xunit;

namespace AVASDecoderChecker.Tests
{
    public class CsvLoggingServiceTests : IDisposable
    {
        private readonly string _tempLogsDir;

        public CsvLoggingServiceTests()
        {
            _tempLogsDir = Path.Combine(Path.GetTempPath(), $"avas_logs_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempLogsDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempLogsDir))
            {
                try
                {
                    Directory.Delete(_tempLogsDir, true);
                }
                catch { }
            }
        }

        [Fact]
        public async Task StartNewSession_CreatesUniqueDirectoryAndMasterCsv()
        {
            var logger = new CsvLoggingService(_tempLogsDir);
            string sessionDir = await logger.StartNewSessionAsync();

            Assert.True(logger.IsSessionActive);
            Assert.True(Directory.Exists(sessionDir));
            Assert.True(Directory.Exists(Path.Combine(sessionDir, "decoders")));

            string masterPath = Path.Combine(sessionDir, "master_telemetry.csv");
            Assert.True(File.Exists(masterPath));

            string content = await File.ReadAllTextAsync(masterPath);
            Assert.Contains("Timestamp,DecoderName,MAC,IP,OverallStatus", content);
        }

        [Fact]
        public async Task LogSample_AppendsToMasterCsv_AndCreatesPerDecoderCsv()
        {
            var logger = new CsvLoggingService(_tempLogsDir);
            string sessionDir = await logger.StartNewSessionAsync();

            var sample = new DecoderTelemetrySample
            {
                Timestamp = DateTime.Now,
                DecoderName = "Decoder-Office",
                MacAddress = "00:0e:c6:aa:bb:cc",
                IpAddress = "192.168.1.100",
                IsStreaming = true,
                IsDisplayConnected = true,
                IsStable = true,
                IsPushingVideo = true,
                SubscriptionState = "CONNECTED",
                ResolutionTiming = "1920x1080@60Hz",
                SourceTxMac = "00:0e:c6:11:22:33",
                OverallStatus = "PASS",
                Notes = "All OK"
            };

            await logger.LogSampleAsync(sample);

            string masterPath = Path.Combine(sessionDir, "master_telemetry.csv");
            string masterLines = await File.ReadAllTextAsync(masterPath);
            Assert.Contains("Decoder-Office", masterLines);
            Assert.Contains("00:0e:c6:aa:bb:cc", masterLines);
            Assert.Contains("1920x1080@60Hz", masterLines);

            string decoderFile = Path.Combine(sessionDir, "decoders", "Decoder-Office_00_0e_c6_aa_bb_cc.csv");
            Assert.True(File.Exists(decoderFile));

            string decoderLines = await File.ReadAllTextAsync(decoderFile);
            Assert.Contains("Timestamp,OverallStatus,FaultAttribution,SourceEncoderStatus,IsAnomaly,RecoveryTimeSec,BlackoutDurationSec", decoderLines);
            Assert.Contains("1920x1080@60Hz", decoderLines);
        }

        [Fact]
        public async Task StartNewSession_CreatesEncoderAndAnomalyCsvsWithHeaders()
        {
            var logger = new CsvLoggingService(_tempLogsDir);
            string sessionDir = await logger.StartNewSessionAsync();

            string encoderCsv = Path.Combine(sessionDir, "encoder_telemetry.csv");
            Assert.True(File.Exists(encoderCsv));
            string encHeader = await File.ReadAllTextAsync(encoderCsv);
            Assert.Contains("Timestamp,EncoderName,MAC,IP,OverallStatus,SourceStable,HasVideo", encHeader);

            string anomalyCsv = Path.Combine(sessionDir, "anomaly_events.csv");
            Assert.True(File.Exists(anomalyCsv));
            string anomHeader = await File.ReadAllTextAsync(anomalyCsv);
            Assert.Contains("Timestamp,EventType,ElapsedSeconds,EncoderMAC,AffectedDecoderName", anomHeader);
        }

        [Fact]
        public async Task LogEncoderSample_AppendsToEncoderCsv()
        {
            var logger = new CsvLoggingService(_tempLogsDir);
            string sessionDir = await logger.StartNewSessionAsync();

            var encSample = new EncoderTelemetrySample
            {
                EncoderName = "TX-Main-74fe488b07bb",
                MacAddress = "74:fe:48:8b:07:bb",
                IpAddress = "192.168.1.50",
                OverallStatus = "PASS",
                IsSourceStable = true,
                HasVideo = true,
                VideoRaster = "3840x2160@60Hz",
                PixelClockMhz = 594.0,
                StreamState = "STREAMING",
                MulticastAddress = "239.255.0.1",
                TemperatureC = 52.4
            };

            await logger.LogEncoderSampleAsync(encSample);

            string encoderCsv = Path.Combine(sessionDir, "encoder_telemetry.csv");
            string lines = await File.ReadAllTextAsync(encoderCsv);
            Assert.Contains("TX-Main-74fe488b07bb", lines);
            Assert.Contains("74:fe:48:8b:07:bb", lines);
            Assert.Contains("3840x2160@60Hz", lines);
            Assert.Contains("594.0", lines);
            Assert.Contains("52.4", lines);
        }

        [Fact]
        public async Task LogAnomalyEvent_AppendsToAnomalyCsv()
        {
            var logger = new CsvLoggingService(_tempLogsDir);
            string sessionDir = await logger.StartNewSessionAsync();

            var anomaly = new AnomalyEvent
            {
                Timestamp = DateTime.Now,
                EventType = AnomalyEventType.POST_REBOOT_TIMEOUT,
                EncoderMac = "74:fe:48:8b:07:bb",
                EncoderName = "TX-Main",
                AffectedDecoderMac = "74:fe:48:8b:07:81",
                AffectedDecoderName = "Decoder-Right",
                DurationSeconds = 3.45,
                Description = "Failed to display video after 3.45s post-reboot (>2.0s window)"
            };

            await logger.LogAnomalyEventAsync(anomaly);

            string anomalyCsv = Path.Combine(sessionDir, "anomaly_events.csv");
            string lines = await File.ReadAllTextAsync(anomalyCsv);
            Assert.Contains("POST_REBOOT_TIMEOUT", lines);
            Assert.Contains("74:fe:48:8b:07:bb", lines);
            Assert.Contains("Decoder-Right", lines);
            Assert.Contains("3.45", lines);
            Assert.Contains(">2.0s window", lines);
        }

        [Fact]
        public async Task ConcludeSession_GeneratesSummaryReport_AndResetsActiveSession()
        {
            var logger = new CsvLoggingService(_tempLogsDir);
            string sessionDir = await logger.StartNewSessionAsync();

            await logger.LogSampleAsync(new DecoderTelemetrySample
            {
                DecoderName = "RX1",
                MacAddress = "00:0e:c6:11:11:11",
                OverallStatus = "PASS",
                IsStreaming = true,
                IsDisplayConnected = true,
                IsPushingVideo = true
            });

            await logger.LogSampleAsync(new DecoderTelemetrySample
            {
                DecoderName = "RX2",
                MacAddress = "00:0e:c6:22:22:22",
                OverallStatus = "FAIL",
                IsStreaming = false,
                IsDisplayConnected = false,
                IsPushingVideo = false
            });

            var summary = await logger.ConcludeSessionAsync();

            Assert.False(logger.IsSessionActive);
            Assert.Equal(2, summary.TotalSamples);
            Assert.Equal(1, summary.PassCount);
            Assert.Equal(1, summary.FailCount);
            Assert.Equal(2, summary.MonitoredDecodersCount);
            Assert.Equal(50.0, summary.HealthPercentage);

            string summaryPath = Path.Combine(sessionDir, "summary_report.csv");
            Assert.True(File.Exists(summaryPath));

            string summaryText = await File.ReadAllTextAsync(summaryPath);
            Assert.Contains("Total Polling Samples,2", summaryText);
            Assert.Contains("Passing Samples,1", summaryText);
            Assert.Contains("Failing Samples,1", summaryText);
            Assert.Contains("RX1", summaryText);
            Assert.Contains("RX2", summaryText);
        }
    }
}
