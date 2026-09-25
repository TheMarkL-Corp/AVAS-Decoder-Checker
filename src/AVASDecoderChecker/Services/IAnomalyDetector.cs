using System;
using System.Collections.Generic;
using AVASDecoderChecker.Models;

namespace AVASDecoderChecker.Services
{
    public interface IAnomalyDetector
    {
        double AnomalyWindowSeconds { get; set; }
        event EventHandler<AnomalyEvent>? AnomalyDetected;

        void ProcessCycle(
            List<EncoderTelemetrySample> encoderSamples,
            List<DecoderTelemetrySample> decoderSamples,
            DateTime? currentTimestamp = null);

        void Reset();
    }
}
