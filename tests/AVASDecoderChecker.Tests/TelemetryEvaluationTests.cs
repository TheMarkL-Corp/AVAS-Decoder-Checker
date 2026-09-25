using AVASDecoderChecker.Services;
using Xunit;

namespace AVASDecoderChecker.Tests
{
    public class TelemetryEvaluationTests
    {
        [Fact]
        public void Evaluate_WhenAllConditionsHealthy_ReturnsPass()
        {
            var sample = HealthEvaluator.Evaluate(
                isStreaming: true,
                isDisplayConnected: true,
                isStable: true,
                subscriptionState: "CONNECTED",
                resolutionTiming: "1920x1080@60Hz",
                sourceTxMac: "00:0e:c6:11:22:33",
                decoderName: "RX-LivingRoom",
                macAddress: "00:0e:c6:aa:bb:cc"
            );

            Assert.Equal("PASS", sample.OverallStatus);
            Assert.True(sample.IsPushingVideo);
            Assert.True(sample.IsStreaming);
            Assert.True(sample.IsDisplayConnected);
            Assert.True(sample.IsStable);
            Assert.Equal("RX-LivingRoom", sample.DecoderName);
        }

        [Fact]
        public void Evaluate_WhenDisplayDisconnected_ReturnsFail()
        {
            var sample = HealthEvaluator.Evaluate(
                isStreaming: true,
                isDisplayConnected: false, // HPD down
                isStable: true,
                subscriptionState: "CONNECTED",
                resolutionTiming: "1920x1080@60Hz",
                decoderName: "RX-Conference"
            );

            Assert.Equal("FAIL", sample.OverallStatus);
            Assert.False(sample.IsDisplayConnected);
            Assert.False(sample.IsPushingVideo);
            Assert.Contains("Display Disconnected", sample.Notes);
        }

        [Fact]
        public void Evaluate_WhenStreamInactive_ReturnsFail()
        {
            var sample = HealthEvaluator.Evaluate(
                isStreaming: false, // Stream not active
                isDisplayConnected: true,
                isStable: false,
                subscriptionState: "UNSUBSCRIBED",
                resolutionTiming: "No Signal"
            );

            Assert.Equal("FAIL", sample.OverallStatus);
            Assert.False(sample.IsStreaming);
            Assert.Contains("Stream Inactive", sample.Notes);
        }

        [Fact]
        public void Evaluate_WhenClockSyncUnstable_ReturnsWarn()
        {
            var sample = HealthEvaluator.Evaluate(
                isStreaming: true,
                isDisplayConnected: true,
                isStable: false, // Unstable clock
                subscriptionState: "CONNECTED",
                resolutionTiming: "1920x1080@60Hz"
            );

            Assert.Equal("WARN", sample.OverallStatus);
            Assert.False(sample.IsPushingVideo);
            Assert.Contains("Clock Sync Unstable", sample.Notes);
        }

        [Fact]
        public void Evaluate_WhenUnsubscribed_ReturnsWarn()
        {
            var sample = HealthEvaluator.Evaluate(
                isStreaming: true,
                isDisplayConnected: true,
                isStable: true,
                subscriptionState: "UNSUBSCRIBED",
                resolutionTiming: "No Signal"
            );

            Assert.Equal("WARN", sample.OverallStatus);
            Assert.False(sample.IsPushingVideo);
            Assert.Contains("Subscription", sample.Notes);
        }

        [Fact]
        public void EvaluateJson_ParsesValidSDVoEPayloadCorrectly()
        {
            string json = @"{
                ""status"": ""SUCCESS"",
                ""result"": {
                    ""devices"": [
                        {
                            ""device_id"": ""00:0e:c6:88:99:aa"",
                            ""configuration"": {
                                ""device_name"": ""Decoder-Lab-01"",
                                ""resume_streaming"": true
                            },
                            ""streams"": [
                                {
                                    ""index"": 0,
                                    ""status"": { ""state"": ""STREAMING"" }
                                }
                            ],
                            ""subscriptions"": [
                                {
                                    ""type"": ""HDMI"",
                                    ""status"": { ""state"": ""CONNECTED"" },
                                    ""configuration"": { ""source"": ""00:0e:c6:11:22:33"" }
                                }
                            ],
                            ""nodes"": [
                                {
                                    ""type"": ""HDMI_MONITOR"",
                                    ""status"": { ""connected"": true }
                                },
                                {
                                    ""type"": ""HDMI_DECODER"",
                                    ""status"": {
                                        ""source_stable"": true,
                                        ""video_details"": {
                                            ""width"": 3840,
                                            ""height"": 2160,
                                            ""fps"": 60.0
                                        }
                                    }
                                }
                            ]
                        }
                    ]
                }
            }";

            var sample = HealthEvaluator.EvaluateJson(json, "00:0e:c6:88:99:aa", "192.168.1.150");

            Assert.Equal("PASS", sample.OverallStatus);
            Assert.Equal("Decoder-Lab-01", sample.DecoderName);
            Assert.Equal("00:0e:c6:88:99:aa", sample.MacAddress);
            Assert.Equal("192.168.1.150", sample.IpAddress);
            Assert.True(sample.IsStreaming);
            Assert.True(sample.IsDisplayConnected);
            Assert.True(sample.IsStable);
            Assert.True(sample.IsPushingVideo);
            Assert.Equal("3840x2160@60Hz", sample.ResolutionTiming);
            Assert.Equal("00:0e:c6:11:22:33", sample.SourceTxMac);
        }
    }
}
