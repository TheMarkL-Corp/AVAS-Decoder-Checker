using System;

namespace AVASDecoderChecker.Models
{
    public class DecoderTelemetrySample
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string DecoderName { get; set; } = string.Empty;
        public string MacAddress { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;

        // =========================================================================
        // QUESTION 1: Guarantee that the Decoder receives video from the Encoder
        // =========================================================================
        /// <summary>
        /// Status: RECEIVED, NO_STREAM, UNSUBSCRIBED, ERROR
        /// </summary>
        public string VideoReceivedStatus { get; set; } = "UNKNOWN";
        public string SourceEncoderMac { get; set; } = "N/A";
        public string SourceEncoderName { get; set; } = "N/A";
        public string MulticastAddress { get; set; } = "0.0.0.0";
        public bool IsClockStable { get; set; }
        public string VideoRaster { get; set; } = "No Raster";
        public double PixelClockMhz { get; set; }
        public string ColorFormat { get; set; } = string.Empty;
        public string Question1Badge { get; set; } = "UNKNOWN"; // RECEIVED, NO STREAM, UNSUBSCRIBED
        public string Question1Details { get; set; } = string.Empty;

        // =========================================================================
        // QUESTION 2: Check if connected display is displaying video or not
        // =========================================================================
        /// <summary>
        /// Status: DISPLAYING_VIDEO, NO_DISPLAY, HANDSHAKE_FAILED, BLACK_SCREEN, WAITING_FOR_SOURCE
        /// </summary>
        public string DisplayScreenStatus { get; set; } = "UNKNOWN";
        public bool IsHpdConnected { get; set; }
        public string DisplayModelName { get; set; } = "No Display";
        public string DisplaySerialNumber { get; set; } = string.Empty;
        public string DisplayNativeTiming { get; set; } = string.Empty;
        public bool HasAudioSupport { get; set; }
        public bool IsTmdsClockActive { get; set; }
        public bool IsHdcpProtected { get; set; }
        public bool IsHdcpBlocked { get; set; }
        public bool IsMuted { get; set; }
        public string Question2Badge { get; set; } = "UNKNOWN"; // DISPLAYING, NO DISPLAY, BLACK SCREEN, WAITING
        public string Question2Details { get; set; } = string.Empty;

        // =========================================================================
        // HARDWARE HEALTH & DECODER/DISPLAY ATTRIBUTION TELEMETRY (v1.0.1)
        // =========================================================================
        public int TemperatureC { get; set; }
        public string MultiLinkStatus { get; set; } = "N/A"; // SYNCHRONIZED, SINGLE, DESYNCHRONIZED, LOST
        public string MultiLinkMode { get; set; } = "N/A"; // DUAL, SINGLE
        public string NetworkPortSpeed { get; set; } = "N/A"; // 10.0, DOWN
        public bool NetworkPortActive { get; set; } = true;
        public bool IsColorGeneratorActive { get; set; }
        public bool HasInternalErrorCode { get; set; }
        public int InternalErrorCode { get; set; }

        // Network Interface & Power State Diagnostics (v1.0.3)
        public string NetworkIpMode { get; set; } = "UNKNOWN"; // DHCP, STATIC, UNKNOWN
        public string SubnetMask { get; set; } = string.Empty;
        public string GatewayIp { get; set; } = string.Empty;
        public bool IsDhcpFault { get; set; }
        public bool IsPowerLoss { get; set; }
        public bool IsNetworkLinkDown { get; set; }
        public bool IsDeviceActive { get; set; } = true;

        /// <summary>
        /// Root Cause Fault Attribution:
        /// NONE (Healthy),
        /// SOURCE_REBOOTING (Video source PC booting / TX no signal),
        /// POST_REBOOT_RECOVERY_TIMEOUT (Recovery exceeded 2.0s post-boot window),
        /// DECODER_PLL_DESYNC (Anomalous ~83.8% pixel clock: 248.8 or 497.7 MHz),
        /// DECODER_DUAL_DESYNC (Dual-link companion unsynchronized),
        /// DECODER_STREAM_LOSS (Subscribed but clock unlocked while source awake),
        /// DECODER_DHCP_FAULT (DHCP lease failure: APIPA 169.254.x.x or unassigned 0.0.0.0),
        /// DECODER_POWER_LOSS (Abrupt power cut: device unreachable + display HPD 0V),
        /// NETWORK_LINK_DOWN (Network disconnected: device unreachable, but display HPD 5V active),
        /// DISPLAY_HPD_DOWN (Display sleeping or cable unplugged),
        /// DISPLAY_EDID_CORRUPT (Display failed DDC handshake),
        /// DISPLAY_HDCP_BLOCKED (Encrypted content blocked by sink),
        /// SERVER_TIMEOUT (SDVoE server socket timeout)
        /// </summary>
        public string FaultAttribution { get; set; } = "NONE";

        // =========================================================================
        // Upstream Encoder Correlation & Anomaly Metrics (v1.0.2)
        // =========================================================================
        public string SourceEncoderStatus { get; set; } = "UNKNOWN"; // HAS_VIDEO, NO_VIDEO, UNKNOWN
        public bool IsAnomaly { get; set; }
        public double RecoveryTimeSec { get; set; }
        public double BlackoutDurationSec { get; set; }

        // =========================================================================
        // Overall & Diagnostic Notes
        // =========================================================================
        public string OverallStatus { get; set; } = "UNKNOWN"; // PASS, WARN, FAIL
        public string Notes { get; set; } = string.Empty;

        // Backward compatibility properties
        public bool IsStreaming
        {
            get => VideoReceivedStatus == "RECEIVED";
            set => VideoReceivedStatus = value ? "RECEIVED" : "NO_STREAM";
        }

        public bool IsDisplayConnected
        {
            get => IsHpdConnected;
            set => IsHpdConnected = value;
        }

        public bool IsPushingVideo
        {
            get => DisplayScreenStatus == "DISPLAYING_VIDEO";
            set {}
        }

        public bool IsStable
        {
            get => IsClockStable;
            set => IsClockStable = value;
        }

        public string SubscriptionState
        {
            get => VideoReceivedStatus == "RECEIVED" ? "STREAMING" : (VideoReceivedStatus == "UNSUBSCRIBED" ? "STOPPED" : "UNKNOWN");
            set {}
        }

        public string SourceTxMac
        {
            get => SourceEncoderMac;
            set => SourceEncoderMac = value;
        }

        public string ResolutionTiming
        {
            get => VideoRaster;
            set => VideoRaster = value;
        }
    }
}
