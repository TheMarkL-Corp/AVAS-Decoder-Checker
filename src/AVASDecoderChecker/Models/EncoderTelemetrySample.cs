using System;

namespace AVASDecoderChecker.Models
{
    /// <summary>
    /// Represents a point-in-time telemetry sample captured from an SDVoE Transmitter (TX / Encoder).
    /// </summary>
    public class EncoderTelemetrySample
    {
        // Identity
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string EncoderName { get; set; } = string.Empty;
        public string MacAddress { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;

        // Video Source Status (Inbound from PC source via HDMI_DECODER node)
        public bool IsSourceStable { get; set; }
        public bool HasVideo { get; set; }
        public string VideoRaster { get; set; } = "No Raster Detected";
        public double PixelClockMhz { get; set; }
        public string ColorFormat { get; set; } = string.Empty;

        // Streaming Status (Outbound multicast transmission via streams node)
        public bool IsStreaming { get; set; }
        public string StreamState { get; set; } = "UNKNOWN";
        public string MulticastAddress { get; set; } = "0.0.0.0";

        // HDCP Status
        public bool IsHdcpProtected { get; set; }

        // Multi-Link / Dual-Link Transmitter Pairing
        public string LinkMode { get; set; } = "SINGLE";
        public string LinkStatus { get; set; } = "UNKNOWN";
        public string CompanionMac { get; set; } = string.Empty;

        // Hardware Health & Internal Temperature
        public double? TemperatureC { get; set; }
        public bool HasErrorCode { get; set; }
        public int ErrorCode { get; set; }

        // Overall Evaluation Verdict
        public string OverallStatus { get; set; } = "UNKNOWN"; // PASS, WARN, FAIL, REBOOTING
        public string Notes { get; set; } = string.Empty;
    }
}
