using System;
using AVASDecoderChecker.Models;
using AVASDecoderChecker.Services;
using Xunit;

namespace AVASDecoderChecker.Tests
{
    public class EncoderEvaluationTests
    {
        private const string LiveEncoderJsonStable = @"{
            ""status"": ""SUCCESS"",
            ""result"": {
                ""devices"": [
                    {
                        ""device_id"": ""74fe488b07bb"",
                        ""status"": {
                            ""temperature"": 51.5
                        },
                        ""streams"": [
                            {
                                ""type"": ""HDMI"",
                                ""index"": 0,
                                ""status"": { ""state"": ""STREAMING"" },
                                ""configuration"": { ""address"": ""224.1.1.1"" }
                            }
                        ],
                        ""nodes"": [
                            {
                                ""type"": ""HDMI_DECODER"",
                                ""index"": 0,
                                ""status"": {
                                    ""source_stable"": true,
                                    ""Video"": { ""width"": 3840, ""height"": 2160 },
                                    ""video_details"": { ""pixel_clock"": 594000000, ""fps"": 60.0 }
                                }
                            },
                            {
                                ""type"": ""MULTI_LINK_TRANSMITTER"",
                                ""index"": 0,
                                ""status"": {
                                    ""link_mode"": ""DUAL"",
                                    ""link_status"": ""SYNCHRONIZED""
                                },
                                ""configuration"": {
                                    ""companions"": [""74fe488b07bc""]
                                }
                            }
                        ]
                    }
                ]
            }
        }";

        private const string LiveEncoderJsonUnstable = @"{
            ""status"": ""SUCCESS"",
            ""result"": {
                ""devices"": [
                    {
                        ""device_id"": ""74fe488b07bb"",
                        ""status"": {
                            ""temperature"": 52.0
                        },
                        ""streams"": [
                            {
                                ""type"": ""HDMI"",
                                ""index"": 0,
                                ""status"": { ""state"": ""STOPPED"" },
                                ""configuration"": { ""address"": ""224.1.1.1"" }
                            }
                        ],
                        ""nodes"": [
                            {
                                ""type"": ""HDMI_DECODER"",
                                ""index"": 0,
                                ""status"": {
                                    ""source_stable"": false,
                                    ""Video"": { ""width"": 0, ""height"": 0 },
                                    ""video_details"": { ""pixel_clock"": 0, ""fps"": 0.0 }
                                }
                            }
                        ]
                    }
                ]
            }
        }";

        [Fact]
        public void EvaluateEncoder_WithStableSource_ReturnsHasVideoTrue()
        {
            var encoderItem = new EncoderItem
            {
                MacAddress = "74fe488b07bb",
                DeviceName = "TX-Primary",
                IpAddress = "10.32.32.10"
            };

            var sample = HealthEvaluator.EvaluateEncoder(LiveEncoderJsonStable, encoderItem);

            Assert.True(sample.HasVideo);
            Assert.True(sample.IsSourceStable);
            Assert.True(sample.IsStreaming);
            Assert.Equal("STREAMING", sample.StreamState);
            Assert.Equal("224.1.1.1", sample.MulticastAddress);
            Assert.Equal(594.0, sample.PixelClockMhz);
            Assert.Contains("3840x2160", sample.VideoRaster);
            Assert.Equal("DUAL", sample.LinkMode);
            Assert.Equal("SYNCHRONIZED", sample.LinkStatus);
            Assert.Equal("74fe488b07bc", sample.CompanionMac);
            Assert.Equal(51.5, sample.TemperatureC);
            Assert.Equal("PASS", sample.OverallStatus);
        }

        [Fact]
        public void EvaluateEncoder_WithNoSource_ReportsNoVideo()
        {
            var encoderItem = new EncoderItem
            {
                MacAddress = "74fe488b07bb",
                DeviceName = "TX-Primary",
                IpAddress = "10.32.32.10"
            };

            var sample = HealthEvaluator.EvaluateEncoder(LiveEncoderJsonUnstable, encoderItem);

            Assert.False(sample.HasVideo);
            Assert.False(sample.IsSourceStable);
            Assert.False(sample.IsStreaming);
            Assert.Equal("FAIL", sample.OverallStatus);
        }
    }
}
