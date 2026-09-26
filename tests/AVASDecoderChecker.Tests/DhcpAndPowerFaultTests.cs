using System;
using AVASDecoderChecker.Models;
using AVASDecoderChecker.Services;
using Xunit;

namespace AVASDecoderChecker.Tests
{
    public class DhcpAndPowerFaultTests
    {
        [Fact]
        public void Evaluate_DhcpApipaFallback_SetsFaultAttributionToDecoderDhcpFault()
        {
            // Arrange: Device configured for DHCP, but leased an APIPA (169.254.x.x) address
            var json = @"{
                'device_id': '74fe488b22ca',
                'configuration': { 'device_name': 'TestDecoder' },
                'status': { 'active': true, 'temperature': 45 },
                'nodes': [
                    {
                        'type': 'NETWORK_INTERFACE',
                        'configuration': {
                            'ip': { 'mode': 'DHCP', 'address': '10.32.32.14' }
                        },
                        'status': {
                            'ip': { 'address': '169.254.120.45', 'mode': 'DHCP', 'mask': '255.255.0.0' }
                        }
                    },
                    {
                        'type': 'HDMI_MONITOR',
                        'status': { 'connected': true }
                    }
                ],
                'subscriptions': [
                    {
                        'type': 'HDMI',
                        'status': { 'state': 'STOPPED' },
                        'configuration': { 'address': '0.0.0.0', 'enable': false }
                    }
                ]
            }";

            // Act
            var sample = HealthEvaluator.EvaluateJson(json);

            // Assert
            Assert.Equal("DECODER_DHCP_FAULT", sample.FaultAttribution);
            Assert.True(sample.IsDhcpFault);
            Assert.Equal("DHCP", sample.NetworkIpMode);
            Assert.Equal("169.254.120.45", sample.IpAddress);
            Assert.Equal("FAIL", sample.OverallStatus);
            Assert.Contains("DHCP Lease Failure", sample.Notes);
        }

        [Fact]
        public void Evaluate_DhcpZeroIp_SetsFaultAttributionToDecoderDhcpFault()
        {
            // Arrange: Device configured for DHCP, but has 0.0.0.0 unassigned IP
            var json = @"{
                'device_id': '74fe488b22cb',
                'configuration': { 'device_name': 'TestDecoder2' },
                'status': { 'active': true },
                'nodes': [
                    {
                        'type': 'NETWORK_INTERFACE',
                        'configuration': {
                            'ip': { 'mode': 'DHCP' }
                        },
                        'status': {
                            'ip': { 'address': '0.0.0.0' }
                        }
                    }
                ]
            }";

            // Act
            var sample = HealthEvaluator.EvaluateJson(json);

            // Assert
            Assert.Equal("DECODER_DHCP_FAULT", sample.FaultAttribution);
            Assert.True(sample.IsDhcpFault);
            Assert.Contains("DHCP Unassigned", sample.Notes);
        }

        [Fact]
        public void Evaluate_StaticIpNormal_DoesNotTriggerDhcpFault()
        {
            // Arrange: Normal static IP configuration
            var json = @"{
                'device_id': '74fe488b22cc',
                'configuration': { 'device_name': 'StaticDecoder' },
                'status': { 'active': true },
                'nodes': [
                    {
                        'type': 'NETWORK_INTERFACE',
                        'configuration': {
                            'ip': { 'mode': 'STATIC', 'address': '192.168.1.50' }
                        },
                        'status': {
                            'ip': { 'address': '192.168.1.50' }
                        }
                    },
                    {
                        'type': 'HDMI_MONITOR',
                        'status': { 'connected': true }
                    },
                    {
                        'type': 'HDMI_ENCODER',
                        'status': { 'source_stable': true, 'video_details': { 'width': 1920, 'height': 1080, 'fps': 60, 'pixel_clock': 148500000 } }
                    }
                ],
                'subscriptions': [
                    {
                        'type': 'HDMI',
                        'status': { 'state': 'STREAMING' },
                        'configuration': { 'address': '224.1.1.100', 'enable': true }
                    }
                ]
            }";

            // Act
            var sample = HealthEvaluator.EvaluateJson(json);

            // Assert
            Assert.False(sample.IsDhcpFault);
            Assert.Equal("STATIC", sample.NetworkIpMode);
            Assert.Equal("PASS", sample.OverallStatus);
        }

        [Fact]
        public void Evaluate_DeviceInactive_HpdLow_SetsDecoderPowerLoss()
        {
            // Arrange: SDVoE server reports device inactive, AND HDMI monitor connected is false (power cut)
            var json = @"{
                'device_id': '74fe488b22cd',
                'configuration': { 'device_name': 'PowerLostDecoder' },
                'status': { 'active': false, 'temperature': 25 },
                'nodes': [
                    {
                        'type': 'NETWORK_INTERFACE',
                        'status': { 'ip': { 'address': '10.32.32.50' } }
                    },
                    {
                        'type': 'HDMI_MONITOR',
                        'status': { 'connected': false }
                    }
                ]
            }";

            // Act
            var sample = HealthEvaluator.EvaluateJson(json);

            // Assert
            Assert.Equal("DECODER_POWER_LOSS", sample.FaultAttribution);
            Assert.True(sample.IsPowerLoss);
            Assert.False(sample.IsDeviceActive);
            Assert.Equal("FAIL", sample.OverallStatus);
            Assert.Contains("Power Loss", sample.Notes);
        }

        [Fact]
        public void Evaluate_DeviceInactive_HpdHigh_SetsNetworkLinkDown()
        {
            // Arrange: Device inactive in server, BUT HDMI monitor is still connected (decoder has power, network cut)
            var json = @"{
                'device_id': '74fe488b22ce',
                'configuration': { 'device_name': 'CableCutDecoder' },
                'status': { 'active': false },
                'nodes': [
                    {
                        'type': 'NETWORK_INTERFACE',
                        'status': { 'ip': { 'address': '10.32.32.51' } }
                    },
                    {
                        'type': 'HDMI_MONITOR',
                        'status': { 'connected': true }
                    }
                ]
            }";

            // Act
            var sample = HealthEvaluator.EvaluateJson(json);

            // Assert
            Assert.Equal("NETWORK_LINK_DOWN", sample.FaultAttribution);
            Assert.True(sample.IsNetworkLinkDown);
            Assert.False(sample.IsPowerLoss);
            Assert.False(sample.IsDeviceActive);
            Assert.Equal("FAIL", sample.OverallStatus);
            Assert.Contains("Network Link Disconnected", sample.Notes);
        }

        [Fact]
        public void CreateUnreachableSample_LanPingSucceeds_SetsServerTimeout()
        {
            // Arrange: Control server timed out, but direct LAN ping to decoder succeeds
            var sample = HealthEvaluator.CreateUnreachableSample("74fe488b22cf", "Decoder_Pingable", "10.32.32.52", isLanPingable: true, wasHpdConnected: true);

            // Assert
            Assert.Equal("SERVER_TIMEOUT", sample.FaultAttribution);
            Assert.False(sample.IsDeviceActive);
            Assert.Contains("SDVoE Server Unresponsive", sample.Notes);
        }

        [Fact]
        public void CreateUnreachableSample_LanPingFails_HpdLow_SetsDecoderPowerLoss()
        {
            // Arrange: Server timed out, direct LAN ping fails, and HPD is down
            var sample = HealthEvaluator.CreateUnreachableSample("74fe488b22d0", "Decoder_Dead", "10.32.32.53", isLanPingable: false, wasHpdConnected: false);

            // Assert
            Assert.Equal("DECODER_POWER_LOSS", sample.FaultAttribution);
            Assert.True(sample.IsPowerLoss);
            Assert.Contains("Power Loss", sample.Notes);
        }

        [Fact]
        public void CreateUnreachableSample_LanPingFails_HpdHigh_SetsNetworkLinkDown()
        {
            // Arrange: Server timed out, LAN ping fails, but HPD was high (display awake)
            var sample = HealthEvaluator.CreateUnreachableSample("74fe488b22d1", "Decoder_CableOut", "10.32.32.54", isLanPingable: false, wasHpdConnected: true);

            // Assert
            Assert.Equal("NETWORK_LINK_DOWN", sample.FaultAttribution);
            Assert.True(sample.IsNetworkLinkDown);
            Assert.False(sample.IsPowerLoss);
            Assert.Contains("Network Link Down", sample.Notes);
        }
    }
}
