using System;
using System.IO;
using AVASDecoderChecker.Services;
using Newtonsoft.Json.Linq;
using Xunit;

namespace AVASDecoderChecker.Tests
{
    public class HealthEvaluatorOverhaulTests
    {
        private string GetLiveDecoderJson()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "golden_RX_DECODER_74fe488b22ca.json");
            Assert.True(File.Exists(path), "golden_RX_DECODER_74fe488b22ca.json must exist in bin directory");
            return File.ReadAllText(path);
        }

        [Fact]
        public void EvaluateJson_WithLiveDecoderPayload_ReportsReceivedAndDisplaying()
        {
            string json = GetLiveDecoderJson();
            var sample = HealthEvaluator.EvaluateJson(json, "74fe488b22ca", "10.32.32.14");

            // Question 1: Video received from encoder
            Assert.Equal("RECEIVED", sample.VideoReceivedStatus);
            Assert.True(sample.IsClockStable);
            Assert.Equal("224.1.1.1", sample.MulticastAddress);
            Assert.Contains("60", sample.VideoRaster);

            // Question 2: Display connected and displaying
            Assert.Equal("DISPLAYING_VIDEO", sample.DisplayScreenStatus);
            Assert.True(sample.IsHpdConnected);
            Assert.Equal("VP3268-4K", sample.DisplayModelName);
            Assert.Equal("V0E180900167", sample.DisplaySerialNumber);
            Assert.True(sample.IsTmdsClockActive);
            Assert.False(sample.IsHdcpBlocked);

            // Overall verdict
            Assert.Equal("PASS", sample.OverallStatus);
        }

        [Fact]
        public void EvaluateJson_WhenDisplayUnplugged_ReportsNoDisplayDetected()
        {
            string json = GetLiveDecoderJson();
            var root = JObject.Parse(json);
            var dev = root["result"]["devices"][0];

            // Set HDMI_MONITOR connected to false
            var nodes = dev["nodes"] as JArray;
            foreach (var n in nodes)
            {
                if (n["type"]?.ToString() == "HDMI_MONITOR")
                {
                    n["status"]["connected"] = false;
                    n["status"]["edid"] = "";
                }
            }

            var sample = HealthEvaluator.EvaluateJson(root.ToString(), "74fe488b22ca", "10.32.32.14");

            Assert.Equal("RECEIVED", sample.VideoReceivedStatus);
            Assert.Equal("NO_DISPLAY", sample.DisplayScreenStatus);
            Assert.False(sample.IsHpdConnected);
            Assert.Equal("FAIL", sample.OverallStatus);
        }

        [Fact]
        public void EvaluateJson_WhenEdidMissing_ReportsHandshakeFailed()
        {
            string json = GetLiveDecoderJson();
            var root = JObject.Parse(json);
            var dev = root["result"]["devices"][0];

            var nodes = dev["nodes"] as JArray;
            foreach (var n in nodes)
            {
                if (n["type"]?.ToString() == "HDMI_MONITOR")
                {
                    n["status"]["connected"] = true;
                    n["status"]["edid"] = ""; // No EDID responded
                }
            }

            var sample = HealthEvaluator.EvaluateJson(root.ToString(), "74fe488b22ca", "10.32.32.14");

            Assert.Equal("RECEIVED", sample.VideoReceivedStatus);
            Assert.Equal("HANDSHAKE_FAILED", sample.DisplayScreenStatus);
            Assert.True(sample.IsHpdConnected);
            Assert.Equal("FAIL", sample.OverallStatus);
        }

        [Fact]
        public void EvaluateJson_WhenStreamStopped_ReportsNoStreamAndWaitingForSource()
        {
            string json = GetLiveDecoderJson();
            var root = JObject.Parse(json);
            var dev = root["result"]["devices"][0];

            // Set subscription state to STOPPED
            var subs = dev["subscriptions"] as JArray;
            subs[0]["status"]["state"] = "STOPPED";

            // Set HDMI_ENCODER source_stable to false
            var nodes = dev["nodes"] as JArray;
            foreach (var n in nodes)
            {
                if (n["type"]?.ToString() == "HDMI_ENCODER")
                {
                    n["status"]["source_stable"] = false;
                }
            }

            var sample = HealthEvaluator.EvaluateJson(root.ToString(), "74fe488b22ca", "10.32.32.14");

            Assert.Equal("NO_STREAM", sample.VideoReceivedStatus);
            Assert.False(sample.IsClockStable);
            Assert.Equal("WAITING_FOR_SOURCE", sample.DisplayScreenStatus);
            Assert.Equal("FAIL", sample.OverallStatus);
        }
    }
}
