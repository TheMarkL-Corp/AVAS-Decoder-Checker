using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AVASDecoderChecker.Models;

namespace AVASDecoderChecker.Services
{
    public class MonitorEngine : IMonitorEngine
    {
        private readonly ISdvoeService _sdvoeService;
        private readonly ILoggingService _loggingService;
        private readonly IAnomalyDetector _anomalyDetector;

        private CancellationTokenSource? _cts;
        private Task? _monitorTask;
        private readonly Stopwatch _stopwatch = new();

        public bool IsRunning => _monitorTask != null && !_monitorTask.IsCompleted;
        public string? CurrentSessionDirectory => _loggingService.CurrentSessionDirectory;
        public TimeSpan ElapsedTime => _stopwatch.Elapsed;

        public event EventHandler<DecoderTelemetrySample>? TelemetrySampleReceived;
        public event EventHandler<EncoderTelemetrySample>? EncoderTelemetrySampleReceived;
        public event EventHandler<AnomalyEvent>? AnomalyDetected;
        public event EventHandler<TestSessionSummary>? SessionCompleted;
        public event EventHandler<string>? StatusMessageLogged;

        public MonitorEngine(ISdvoeService sdvoeService, ILoggingService loggingService, IAnomalyDetector? anomalyDetector = null)
        {
            _sdvoeService = sdvoeService ?? throw new ArgumentNullException(nameof(sdvoeService));
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
            _anomalyDetector = anomalyDetector ?? new AnomalyDetector();
            _anomalyDetector.AnomalyDetected += (sender, anomaly) =>
            {
                AnomalyDetected?.Invoke(this, anomaly);
                _ = _loggingService.LogAnomalyEventAsync(anomaly);
                StatusMessageLogged?.Invoke(this, $"⚠️ [{anomaly.EventType}] {anomaly.Description}");
            };
        }

        public async Task<string> StartAsync(AppSettings settings, List<DecoderItem> selectedDecoders, string? baseLogsDir = null)
        {
            if (IsRunning)
            {
                throw new InvalidOperationException("Test monitoring is already running.");
            }

            if (selectedDecoders == null || selectedDecoders.Count == 0)
            {
                throw new ArgumentException("No decoders selected for monitoring.", nameof(selectedDecoders));
            }

            string sessionDir = await _loggingService.StartNewSessionAsync(baseLogsDir);
            _cts = new CancellationTokenSource();
            _stopwatch.Restart();

            _anomalyDetector.AnomalyWindowSeconds = settings.AnomalyWindowSeconds > 0 ? settings.AnomalyWindowSeconds : 2.0;
            _anomalyDetector.Reset();

            StatusMessageLogged?.Invoke(this, $"Test session initiated: {sessionDir}");
            StatusMessageLogged?.Invoke(this, $"Monitoring {selectedDecoders.Count} decoder(s) at {settings.PollingIntervalSeconds:F1}s interval (Anomaly window: {_anomalyDetector.AnomalyWindowSeconds:F1}s).");

            List<EncoderItem> encoders = new();
            try
            {
                encoders = await _sdvoeService.QueryEncodersAsync(settings.ControlServerIp, settings.ControlServerPort, settings.TimeoutMs);
                if (encoders.Count > 0)
                {
                    StatusMessageLogged?.Invoke(this, $"Discovered {encoders.Count} upstream transmitter(s) for anomaly tracking.");
                }
            }
            catch (Exception ex)
            {
                StatusMessageLogged?.Invoke(this, $"Note: Transmitter discovery: {ex.Message}");
            }

            _monitorTask = Task.Run(() => PollingLoopAsync(settings, selectedDecoders, encoders, _cts.Token));
            return sessionDir;
        }

        public async Task<TestSessionSummary?> StopAsync()
        {
            if (!IsRunning)
            {
                return null;
            }

            StatusMessageLogged?.Invoke(this, "Stopping test cycle...");
            _cts?.Cancel();

            if (_monitorTask != null)
            {
                try
                {
                    await _monitorTask;
                }
                catch (OperationCanceledException) { }
            }

            _stopwatch.Stop();
            var summary = await _loggingService.ConcludeSessionAsync();

            StatusMessageLogged?.Invoke(this, $"Test session finished. Duration: {summary.Duration:hh\\:mm\\:ss}. Total samples: {summary.TotalSamples}, Pass: {summary.PassCount}, Fail: {summary.FailCount}.");
            SessionCompleted?.Invoke(this, summary);

            return summary;
        }

        private async Task PollingLoopAsync(AppSettings settings, List<DecoderItem> decoders, List<EncoderItem> encoders, CancellationToken ct)
        {
            int intervalMs = Math.Max(50, (int)(settings.PollingIntervalSeconds * 1000));
            TimeSpan? durationLimit = settings.DurationMinutes > 0 ? TimeSpan.FromMinutes(settings.DurationMinutes) : null;

            while (!ct.IsCancellationRequested)
            {
                if (durationLimit.HasValue && _stopwatch.Elapsed >= durationLimit.Value)
                {
                    StatusMessageLogged?.Invoke(this, $"Test duration limit ({settings.DurationMinutes} min) reached.");
                    break;
                }

                // 1. Poll upstream Encoders/Transmitters
                var encoderSamples = new List<EncoderTelemetrySample>();
                foreach (var enc in encoders)
                {
                    if (ct.IsCancellationRequested) break;
                    try
                    {
                        var encSample = await _sdvoeService.SampleEncoderTelemetryAsync(
                            settings.ControlServerIp,
                            settings.ControlServerPort,
                            enc,
                            settings.TimeoutMs
                        );
                        encoderSamples.Add(encSample);
                        await _loggingService.LogEncoderSampleAsync(encSample);
                        EncoderTelemetrySampleReceived?.Invoke(this, encSample);
                    }
                    catch (Exception ex)
                    {
                        var errEnc = new EncoderTelemetrySample
                        {
                            Timestamp = DateTime.Now,
                            EncoderName = enc.DeviceName,
                            MacAddress = enc.MacAddress,
                            IpAddress = enc.IpAddress,
                            OverallStatus = "FAIL",
                            Notes = $"Encoder poll error: {ex.Message}"
                        };
                        encoderSamples.Add(errEnc);
                        await _loggingService.LogEncoderSampleAsync(errEnc);
                        EncoderTelemetrySampleReceived?.Invoke(this, errEnc);
                    }
                }

                // 2. Poll Decoders
                var decoderSamples = new List<DecoderTelemetrySample>();
                foreach (var decoder in decoders)
                {
                    if (ct.IsCancellationRequested) break;

                    try
                    {
                        var sample = await _sdvoeService.SampleDecoderTelemetryAsync(
                            settings.ControlServerIp,
                            settings.ControlServerPort,
                            decoder,
                            settings.TimeoutMs
                        );
                        decoderSamples.Add(sample);
                    }
                    catch (Exception ex)
                    {
                        var errorSample = new DecoderTelemetrySample
                        {
                            Timestamp = DateTime.Now,
                            DecoderName = decoder.DeviceName,
                            MacAddress = decoder.MacAddress,
                            IpAddress = decoder.IpAddress,
                            OverallStatus = "FAIL",
                            Notes = $"Polling error: {ex.Message}"
                        };
                        decoderSamples.Add(errorSample);
                    }
                }

                // 3. Process Anomaly State Machine
                _anomalyDetector.ProcessCycle(encoderSamples, decoderSamples);

                // 4. Log and raise events for decoders
                foreach (var sample in decoderSamples)
                {
                    await _loggingService.LogSampleAsync(sample);
                    TelemetrySampleReceived?.Invoke(this, sample);
                }

                try
                {
                    await Task.Delay(intervalMs, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }
}
