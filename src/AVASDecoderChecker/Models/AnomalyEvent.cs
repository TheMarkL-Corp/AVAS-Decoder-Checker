using System;

namespace AVASDecoderChecker.Models
{
    /// <summary>
    /// Type of anomaly or incident detected by the AnomalyDetector.
    /// </summary>
    public enum AnomalyEventType
    {
        /// <summary>
        /// Post-reboot recovery window expired (>2.0s) and decoder is still not displaying video.
        /// </summary>
        POST_REBOOT_TIMEOUT,

        /// <summary>
        /// Encoder is active with continuous video, but decoder suddenly experienced a mid-stream blackout/dropout.
        /// </summary>
        MIDSTREAM_INTERMITTENT_DROPOUT,

        /// <summary>
        /// An intermittent blackout occurred mid-stream, but the decoder successfully recovered before source rebooted.
        /// </summary>
        MIDSTREAM_RECOVERED
    }

    /// <summary>
    /// Represents an anomaly or dropout incident event detected during continuous monitoring.
    /// </summary>
    public class AnomalyEvent
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public AnomalyEventType EventType { get; set; }
        public string EncoderMac { get; set; } = string.Empty;
        public string EncoderName { get; set; } = string.Empty;
        public string AffectedDecoderMac { get; set; } = string.Empty;
        public string AffectedDecoderName { get; set; } = string.Empty;
        public double DurationSeconds { get; set; }
        public string Description { get; set; } = string.Empty;
    }
}
