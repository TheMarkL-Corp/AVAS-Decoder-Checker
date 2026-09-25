using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AVASDecoderChecker.Models
{
    /// <summary>
    /// Represents a discovered SDVoE Transmitter (TX / Encoder).
    /// </summary>
    public class EncoderItem : INotifyPropertyChanged
    {
        private bool _isOnline;
        private string _deviceName = string.Empty;
        private string _ipAddress = string.Empty;
        private string _companionMac = string.Empty;
        private string _multicastAddress = string.Empty;

        public string MacAddress { get; set; } = string.Empty;

        public string DeviceName
        {
            get => _deviceName;
            set
            {
                if (_deviceName != value)
                {
                    _deviceName = value;
                    OnPropertyChanged();
                }
            }
        }

        public string IpAddress
        {
            get => _ipAddress;
            set
            {
                if (_ipAddress != value)
                {
                    _ipAddress = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsTransmitter { get; set; } = true;

        public bool IsOnline
        {
            get => _isOnline;
            set
            {
                if (_isOnline != value)
                {
                    _isOnline = value;
                    OnPropertyChanged();
                }
            }
        }

        public string CompanionMac
        {
            get => _companionMac;
            set
            {
                if (_companionMac != value)
                {
                    _companionMac = value;
                    OnPropertyChanged();
                }
            }
        }

        public string MulticastAddress
        {
            get => _multicastAddress;
            set
            {
                if (_multicastAddress != value)
                {
                    _multicastAddress = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
