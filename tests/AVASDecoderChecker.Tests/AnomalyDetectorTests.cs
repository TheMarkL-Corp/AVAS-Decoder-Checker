using System;
using System.Collections.Generic;
using AVASDecoderChecker.Models;
using AVASDecoderChecker.Services;
using Xunit;

namespace AVASDecoderChecker.Tests
{
    public class AnomalyDetectorTests
    {
        [Fact]
        public void ProcessCycle_EncoderAlive_DecoderRecoversUnder2s_NoAnomaly()
        {
            var detector = new AnomalyDetector { AnomalyWindowSeconds = 2.0 };
            var anomaliesFired = new List<AnomalyEvent>();
            detector.AnomalyDetected += (s, e) => anomaliesFired.Add(e);

            var encoder = new EncoderTelemetrySample
            {
                MacAddress = "74fe488b07bb",
                EncoderName = "TX-01",
                HasVideo = false
            };

            var decoder = new DecoderTelemetrySample
            {
                MacAddress = "74fe488b22ca",
                DecoderName = "RX-01",
                VideoReceivedStatus = "NO_STREAM",
                DisplayScreenStatus = "NO_DISPLAY"
            };

            // Cycle 1: Encoder is down (source rebooting)
            detector.ProcessCycle(new List<EncoderTelemetrySample> { encoder }, new List<DecoderTelemetrySample> { decoder });
            Assert.Empty(anomaliesFired);
            Assert.False(decoder.IsAnomaly);

            // Cycle 2: Encoder comes alive
            encoder.HasVideo = true;
            encoder.IsSourceStable = true;
            encoder.IsStreaming = true;
            decoder.DisplayScreenStatus = "DISPLAYING_VIDEO";
            decoder.VideoReceivedStatus = "RECEIVED";

            detector.ProcessCycle(new List<EncoderTelemetrySample> { encoder }, new List<DecoderTelemetrySample> { decoder });
            Assert.Empty(anomaliesFired);
            Assert.False(decoder.IsAnomaly);
            Assert.True(decoder.RecoveryTimeSec >= 0);
        }

        [Fact]
        public void ProcessCycle_EncoderAlive_DecoderFailsToRecoverAfter2s_FlagsAnomaly()
        {
            var detector = new AnomalyDetector { AnomalyWindowSeconds = 2.0 };
            var anomaliesFired = new List<AnomalyEvent>();
            detector.AnomalyDetected += (s, e) => anomaliesFired.Add(e);

            var encoder = new EncoderTelemetrySample
            {
                MacAddress = "74fe488b07bb",
                EncoderName = "TX-01",
                HasVideo = false
            };

            var decoder = new DecoderTelemetrySample
            {
                MacAddress = "74fe488b22ca",
                DecoderName = "RX-01",
                VideoReceivedStatus = "NO_STREAM",
                DisplayScreenStatus = "NO_DISPLAY"
            };

            // Cycle 1: Rebooting
            detector.ProcessCycle(new List<EncoderTelemetrySample> { encoder }, new List<DecoderTelemetrySample> { decoder });

            // Cycle 2: Encoder restored at T0
            encoder.HasVideo = true;
            encoder.IsSourceStable = true;
            encoder.IsStreaming = true;

            // Simulate transition with explicit timestamp
            detector.ProcessCycle(new List<EncoderTelemetrySample> { encoder }, new List<DecoderTelemetrySample> { decoder }, DateTime.UtcNow);
            Assert.Empty(anomaliesFired); // Within 2.0s window, no anomaly yet

            // Cycle 3: 2.5s elapsed (> 2.0s), decoder still NO_DISPLAY
            detector.ProcessCycle(new List<EncoderTelemetrySample> { encoder }, new List<DecoderTelemetrySample> { decoder }, DateTime.UtcNow.AddSeconds(2.5));

            Assert.Single(anomaliesFired);
            Assert.Equal(AnomalyEventType.POST_REBOOT_TIMEOUT, anomaliesFired[0].EventType);
            Assert.True(decoder.IsAnomaly);
            Assert.Equal("ANOMALY", decoder.OverallStatus);
        }

        [Fact]
        public void ProcessCycle_EncoderActive_DecoderSuddenlyDropsOut_DetectsIntermittentBlackout()
        {
            var detector = new AnomalyDetector { AnomalyWindowSeconds = 2.0 };
            var anomaliesFired = new List<AnomalyEvent>();
            detector.AnomalyDetected += (s, e) => anomaliesFired.Add(e);

            var encoder = new EncoderTelemetrySample
            {
                MacAddress = "74fe488b07bb",
                EncoderName = "TX-01",
                HasVideo = true,
                IsSourceStable = true,
                IsStreaming = true
            };

            var decoder = new DecoderTelemetrySample
            {
                MacAddress = "74fe488b22ca",
                DecoderName = "RX-01",
                VideoReceivedStatus = "RECEIVED",
                DisplayScreenStatus = "DISPLAYING_VIDEO",
                OverallStatus = "PASS"
            };

            // Cycle 1: Fully healthy steady state
            detector.ProcessCycle(new List<EncoderTelemetrySample> { encoder }, new List<DecoderTelemetrySample> { decoder }, DateTime.UtcNow);
            Assert.Empty(anomaliesFired);

            // Cycle 2: Sudden drop while encoder stays active!
            decoder.DisplayScreenStatus = "BLACK_SCREEN";
            decoder.OverallStatus = "FAIL";

            detector.ProcessCycle(new List<EncoderTelemetrySample> { encoder }, new List<DecoderTelemetrySample> { decoder }, DateTime.UtcNow.AddSeconds(1.0));

            Assert.Single(anomaliesFired);
            Assert.Equal(AnomalyEventType.MIDSTREAM_INTERMITTENT_DROPOUT, anomaliesFired[0].EventType);
            Assert.True(decoder.IsAnomaly);
            Assert.Equal("ANOMALY", decoder.OverallStatus);
        }

        [Fact]
        public void ProcessCycle_IntermittentBlackoutRecoversBeforeReboot_LogsRecoveredDuration()
        {
            var detector = new AnomalyDetector { AnomalyWindowSeconds = 2.0 };
            var anomaliesFired = new List<AnomalyEvent>();
            detector.AnomalyDetected += (s, e) => anomaliesFired.Add(e);

            var encoder = new EncoderTelemetrySample
            {
                MacAddress = "74fe488b07bb",
                EncoderName = "TX-01",
                HasVideo = true,
                IsSourceStable = true,
                IsStreaming = true
            };

            var decoder = new DecoderTelemetrySample
            {
                MacAddress = "74fe488b22ca",
                DecoderName = "RX-01",
                VideoReceivedStatus = "RECEIVED",
                DisplayScreenStatus = "DISPLAYING_VIDEO"
            };

            DateTime t0 = DateTime.UtcNow;

            // Cycle 1: Healthy
            detector.ProcessCycle(new List<EncoderTelemetrySample> { encoder }, new List<DecoderTelemetrySample> { decoder }, t0);

            // Cycle 2: Sudden blackout
            decoder.DisplayScreenStatus = "NO_DISPLAY";
            detector.ProcessCycle(new List<EncoderTelemetrySample> { encoder }, new List<DecoderTelemetrySample> { decoder }, t0.AddSeconds(1.0));

            Assert.Single(anomaliesFired);
            Assert.Equal(AnomalyEventType.MIDSTREAM_INTERMITTENT_DROPOUT, anomaliesFired[0].EventType);

            // Cycle 3: Recovers after 1.5 seconds!
            decoder.DisplayScreenStatus = "DISPLAYING_VIDEO";
            decoder.VideoReceivedStatus = "RECEIVED";
            detector.ProcessCycle(new List<EncoderTelemetrySample> { encoder }, new List<DecoderTelemetrySample> { decoder }, t0.AddSeconds(2.5));

            Assert.Equal(2, anomaliesFired.Count);
            Assert.Equal(AnomalyEventType.MIDSTREAM_RECOVERED, anomaliesFired[1].EventType);
            Assert.True(anomaliesFired[1].DurationSeconds >= 1.5);
            Assert.True(decoder.BlackoutDurationSec >= 1.5);
        }
    }
}
