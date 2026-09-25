namespace AVASDecoderChecker.Models
{
    public class AppSettings
    {
        public string ControlServerIp { get; set; } = "127.0.0.1";
        public int ControlServerPort { get; set; } = 8090;
        public int TimeoutMs { get; set; } = 3000;
        public double PollingIntervalSeconds { get; set; } = 2.0;
        public int DurationMinutes { get; set; } = 0; // 0 = continuous until stopped
        public double AnomalyWindowSeconds { get; set; } = 2.0;
        public List<string> SelectedDecoderMacs { get; set; } = new();
    }
}
