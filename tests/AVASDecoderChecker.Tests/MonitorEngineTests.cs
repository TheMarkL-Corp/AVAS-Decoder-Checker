using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AVASDecoderChecker.Models;
using AVASDecoderChecker.Services;
using Xunit;

namespace AVASDecoderChecker.Tests
{
    public class FakeSdvoeService : ISdvoeService
    {
        public int SampleCallsCount { get; private set; }

        public Task<(bool Success, string Message, long LatencyMs, int DetectedPort)> TestConnectionAsync(string ip, int port, int timeoutMs = 3000)
        {
            return Task.FromResult((true, "OK", 10L, port));
        }

        public Task<List<DecoderItem>> QueryDecodersAsync(string ip, int port, int timeoutMs = 5000)
        {
            return Task.FromResult(new List<DecoderItem>
            {
                new DecoderItem { MacAddress = "00:0e:c6:aa:bb:01", DeviceName = "Decoder-1", IsSelected = true }
            });
        }

        public Task<DecoderTelemetrySample> SampleDecoderTelemetryAsync(string ip, int port, DecoderItem decoder, int timeoutMs = 3000)
        {
            SampleCallsCount++;
            return Task.FromResult(new DecoderTelemetrySample
            {
                DecoderName = decoder.DeviceName,
                MacAddress = decoder.MacAddress,
                OverallStatus = "PASS",
                IsStreaming = true,
                IsDisplayConnected = true,
                IsPushingVideo = true,
                IsStable = true
            });
        }

        public Task<List<EncoderItem>> QueryEncodersAsync(string ip, int port, int timeoutMs = 5000)
        {
            return Task.FromResult(new List<EncoderItem>
            {
                new EncoderItem { MacAddress = "00:0e:c6:tx:01", DeviceName = "TX-Main", IsTransmitter = true }
            });
        }

        public Task<EncoderTelemetrySample> SampleEncoderTelemetryAsync(string ip, int port, EncoderItem encoder, int timeoutMs = 3000)
        {
            return Task.FromResult(new EncoderTelemetrySample
            {
                EncoderName = encoder.DeviceName,
                MacAddress = encoder.MacAddress,
                HasVideo = true,
                IsSourceStable = true,
                OverallStatus = "PASS"
            });
        }
    }

    public class MonitorEngineTests : IDisposable
    {
        private readonly string _tempLogsDir;

        public MonitorEngineTests()
        {
            _tempLogsDir = Path.Combine(Path.GetTempPath(), $"avas_engine_logs_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempLogsDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempLogsDir))
            {
                try { Directory.Delete(_tempLogsDir, true); } catch { }
            }
        }

        [Fact]
        public async Task StartAsync_DispatchesSamplesAndStopsCleanly()
        {
            var fakeSdvoe = new FakeSdvoeService();
            var logger = new CsvLoggingService(_tempLogsDir);
            var engine = new MonitorEngine(fakeSdvoe, logger);

            var settings = new AppSettings
            {
                ControlServerIp = "127.0.0.1",
                ControlServerPort = 80,
                PollingIntervalSeconds = 0.1 // Fast polling for test
            };

            var decoders = new List<DecoderItem>
            {
                new DecoderItem { MacAddress = "00:0e:c6:11:11:11", DeviceName = "RX-Test-1", IsSelected = true }
            };

            var receivedSamples = new List<DecoderTelemetrySample>();
            engine.TelemetrySampleReceived += (s, e) => receivedSamples.Add(e);

            string sessionDir = await engine.StartAsync(settings, decoders, _tempLogsDir);

            Assert.True(engine.IsRunning);
            Assert.True(Directory.Exists(sessionDir));

            // Wait a brief moment to allow a couple of poll cycles
            await Task.Delay(350);

            var summary = await engine.StopAsync();

            Assert.False(engine.IsRunning);
            Assert.NotNull(summary);
            Assert.True(receivedSamples.Count >= 2);
            Assert.True(summary.TotalSamples >= 2);
            Assert.Equal(summary.PassCount, summary.TotalSamples);
        }

        [Fact]
        public async Task StartAsync_WithEncoders_DispatchesEncoderSamplesAndLogsThem()
        {
            var fakeSdvoe = new FakeSdvoeService();
            var logger = new CsvLoggingService(_tempLogsDir);
            var engine = new MonitorEngine(fakeSdvoe, logger);

            var settings = new AppSettings
            {
                ControlServerIp = "127.0.0.1",
                ControlServerPort = 80,
                PollingIntervalSeconds = 0.1,
                AnomalyWindowSeconds = 2.0
            };

            var decoders = new List<DecoderItem>
            {
                new DecoderItem { MacAddress = "00:0e:c6:11:11:11", DeviceName = "RX-Test-1", IsSelected = true }
            };

            var receivedEncoders = new List<EncoderTelemetrySample>();
            engine.EncoderTelemetrySampleReceived += (s, e) => receivedEncoders.Add(e);

            string sessionDir = await engine.StartAsync(settings, decoders, _tempLogsDir);

            await Task.Delay(350);
            await engine.StopAsync();

            Assert.True(receivedEncoders.Count >= 2);
            string encoderCsv = Path.Combine(sessionDir, "encoder_telemetry.csv");
            Assert.True(File.Exists(encoderCsv));
            string content = await File.ReadAllTextAsync(encoderCsv);
            Assert.Contains("TX-Main", content);
            Assert.Contains("00:0e:c6:tx:01", content);
        }
    }
}
