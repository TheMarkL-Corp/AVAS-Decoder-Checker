using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using AVASDecoderChecker.Models;

namespace AVASDecoderChecker.Services
{
    public class AnomalyDetector : IAnomalyDetector
    {
        private class DecoderTrackState
        {
            public bool WasDisplaying { get; set; }
            public DateTime? BlackoutStartTimestamp { get; set; }
            public bool IsCurrentlyBlackedOut { get; set; }
            public bool HasReportedTimeoutThisWindow { get; set; }
        }

        private bool _previousCompositeHasVideo = false;
        private DateTime? _videoAliveTimestamp = null;
        private bool _inRecoveryWindow = false;

        private readonly ConcurrentDictionary<string, DecoderTrackState> _decoderStates = new(StringComparer.OrdinalIgnoreCase);

        public double AnomalyWindowSeconds { get; set; } = 2.0;

        public event EventHandler<AnomalyEvent>? AnomalyDetected;

        public void ProcessCycle(
            List<EncoderTelemetrySample> encoderSamples,
            List<DecoderTelemetrySample> decoderSamples,
            DateTime? currentTimestamp = null)
        {
            DateTime now = currentTimestamp ?? DateTime.UtcNow;

            // 1. Determine composite encoder status
            // If dual transmitters exist, HasVideo requires all chips to have video.
            bool currentCompositeHasVideo = encoderSamples.Count > 0 && encoderSamples.All(e => e.HasVideo);
            string encoderMac = encoderSamples.FirstOrDefault()?.MacAddress ?? "TX-N/A";
            string encoderName = encoderSamples.FirstOrDefault()?.EncoderName ?? "TX-Source";

            // Annotate decoders with source encoder status
            string encoderStatusStr = encoderSamples.Count == 0 ? "UNKNOWN" : (currentCompositeHasVideo ? "HAS_VIDEO" : "NO_VIDEO");
            foreach (var dec in decoderSamples)
            {
                dec.SourceEncoderStatus = encoderStatusStr;
            }

            // 2. Track Encoder Transition: false -> true (Source booted, video returned)
            if (currentCompositeHasVideo && !_previousCompositeHasVideo)
            {
                _videoAliveTimestamp = now;
                _inRecoveryWindow = true;

                // Reset timeout report flag for all decoders for this new window
                foreach (var state in _decoderStates.Values)
                {
                    state.HasReportedTimeoutThisWindow = false;
                }
            }
            // Track Encoder Transition: true -> false (Source reboot started)
            else if (!currentCompositeHasVideo && _previousCompositeHasVideo)
            {
                _inRecoveryWindow = false;
                _videoAliveTimestamp = null;

                // Clear midstream blackout tracking on reboot so routine reboot isn't falsely marked as blackout
                foreach (var state in _decoderStates.Values)
                {
                    state.IsCurrentlyBlackedOut = false;
                    state.BlackoutStartTimestamp = null;
                    state.WasDisplaying = false;
                }
            }

            _previousCompositeHasVideo = currentCompositeHasVideo;

            // 3. Process Decoders
            foreach (var dec in decoderSamples)
            {
                string mac = dec.MacAddress;
                var track = _decoderStates.GetOrAdd(mac, _ => new DecoderTrackState());
                bool isDisplaying = dec.DisplayScreenStatus == "DISPLAYING_VIDEO";

                // --- CASE A: Inside Post-Reboot Recovery Window ---
                if (_inRecoveryWindow && _videoAliveTimestamp.HasValue)
                {
                    double elapsedSinceAlive = (now - _videoAliveTimestamp.Value).TotalSeconds;

                    if (isDisplaying)
                    {
                        dec.RecoveryTimeSec = Math.Max(0.0, Math.Round(elapsedSinceAlive, 2));
                        track.WasDisplaying = true;
                    }
                    else if (elapsedSinceAlive > AnomalyWindowSeconds)
                    {
                        // Exceeded recovery window (e.g. >2.0s) and still not displaying!
                        dec.IsAnomaly = true;
                        dec.OverallStatus = "ANOMALY";
                        if (dec.FaultAttribution == "NONE" || dec.FaultAttribution == "SOURCE_REBOOTING")
                        {
                            dec.FaultAttribution = "POST_REBOOT_RECOVERY_TIMEOUT";
                        }
                        dec.Notes = $"⚠️ ANOMALY: Failed to recover video {elapsedSinceAlive:F1}s after source alive (>{AnomalyWindowSeconds}s window)";

                        if (!track.HasReportedTimeoutThisWindow)
                        {
                            track.HasReportedTimeoutThisWindow = true;
                            AnomalyDetected?.Invoke(this, new AnomalyEvent
                            {
                                Timestamp = now,
                                EventType = AnomalyEventType.POST_REBOOT_TIMEOUT,
                                EncoderMac = encoderMac,
                                EncoderName = encoderName,
                                AffectedDecoderMac = dec.MacAddress,
                                AffectedDecoderName = dec.DecoderName,
                                DurationSeconds = Math.Round(elapsedSinceAlive, 2),
                                Description = $"Decoder failed to display video after {elapsedSinceAlive:F1}s post-reboot (Window: {AnomalyWindowSeconds}s)"
                            });
                        }
                    }
                }
                // --- CASE B: Steady State (Encoder has continuous video) ---
                else if (currentCompositeHasVideo)
                {
                    // Check for sudden mid-stream intermittent dropout
                    if (track.WasDisplaying && !isDisplaying && !track.IsCurrentlyBlackedOut)
                    {
                        // Transitioned from DISPLAYING -> BLACKOUT while encoder still has video!
                        track.IsCurrentlyBlackedOut = true;
                        track.BlackoutStartTimestamp = now;

                        dec.IsAnomaly = true;
                        dec.OverallStatus = "ANOMALY";
                        dec.Notes = $"⚠️ ANOMALY: Sudden mid-stream blackout while encoder is active ({dec.FaultAttribution})";

                        AnomalyDetected?.Invoke(this, new AnomalyEvent
                        {
                            Timestamp = now,
                            EventType = AnomalyEventType.MIDSTREAM_INTERMITTENT_DROPOUT,
                            EncoderMac = encoderMac,
                            EncoderName = encoderName,
                            AffectedDecoderMac = dec.MacAddress,
                            AffectedDecoderName = dec.DecoderName,
                            DurationSeconds = 0,
                            Description = $"Intermittent mid-stream blackout started on {dec.DecoderName} while encoder has video ({dec.FaultAttribution})"
                        });
                    }
                    else if (track.IsCurrentlyBlackedOut && isDisplaying)
                    {
                        // Recovered from intermittent mid-stream blackout before reboot!
                        double blackoutDuration = track.BlackoutStartTimestamp.HasValue
                            ? (now - track.BlackoutStartTimestamp.Value).TotalSeconds
                            : 0;

                        track.IsCurrentlyBlackedOut = false;
                        track.BlackoutStartTimestamp = null;
                        track.WasDisplaying = true;

                        dec.BlackoutDurationSec = Math.Round(blackoutDuration, 2);
                        dec.Notes = $"Recovered: Intermittent blackout lasted {blackoutDuration:F1}s before recovering";

                        AnomalyDetected?.Invoke(this, new AnomalyEvent
                        {
                            Timestamp = now,
                            EventType = AnomalyEventType.MIDSTREAM_RECOVERED,
                            EncoderMac = encoderMac,
                            EncoderName = encoderName,
                            AffectedDecoderMac = dec.MacAddress,
                            AffectedDecoderName = dec.DecoderName,
                            DurationSeconds = Math.Round(blackoutDuration, 2),
                            Description = $"Intermittent blackout recovered on {dec.DecoderName} after {blackoutDuration:F1}s"
                        });
                    }
                    else if (track.IsCurrentlyBlackedOut && !isDisplaying)
                    {
                        // Still in mid-stream blackout
                        double currentBlackoutSec = track.BlackoutStartTimestamp.HasValue
                            ? (now - track.BlackoutStartTimestamp.Value).TotalSeconds
                            : 0;
                        dec.IsAnomaly = true;
                        dec.OverallStatus = "ANOMALY";
                        dec.BlackoutDurationSec = Math.Round(currentBlackoutSec, 2);
                    }
                    else if (isDisplaying)
                    {
                        track.WasDisplaying = true;
                    }
                }
            }

            // Close recovery window if all decoders have recovered
            if (_inRecoveryWindow && decoderSamples.All(d => d.DisplayScreenStatus == "DISPLAYING_VIDEO"))
            {
                _inRecoveryWindow = false;
            }
        }

        public void Reset()
        {
            _previousCompositeHasVideo = false;
            _videoAliveTimestamp = null;
            _inRecoveryWindow = false;
            _decoderStates.Clear();
        }
    }
}
