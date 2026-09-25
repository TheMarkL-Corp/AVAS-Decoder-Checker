using System;

namespace AVASDecoderChecker.Models
{
    public class TestSessionSummary
    {
        public string SessionId { get; set; } = string.Empty;
        public string SessionDirectory { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public TimeSpan Duration => (EndTime ?? DateTime.Now) - StartTime;
        public int TotalSamples { get; set; }
        public int PassCount { get; set; }
        public int WarnCount { get; set; }
        public int FailCount { get; set; }
        public int MonitoredDecodersCount { get; set; }
        public double HealthPercentage => TotalSamples > 0 ? Math.Round((double)PassCount / TotalSamples * 100.0, 1) : 100.0;
    }
}
