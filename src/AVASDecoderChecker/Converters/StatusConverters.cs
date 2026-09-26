using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace AVASDecoderChecker.Converters
{
    public class StatusToColorConverter : IValueConverter
    {
        private static readonly SolidColorBrush PassBrush = new(Color.FromRgb(16, 185, 129));   // Emerald 500
        private static readonly SolidColorBrush WarnBrush = new(Color.FromRgb(245, 158, 11));  // Amber 500
        private static readonly SolidColorBrush FailBrush = new(Color.FromRgb(239, 68, 68));   // Red 500
        private static readonly SolidColorBrush AnomalyBrush = new(Color.FromRgb(168, 85, 247)); // Purple 500
        private static readonly SolidColorBrush MutedBrush = new(Color.FromRgb(148, 163, 184)); // Slate 400

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string? status = value?.ToString()?.ToUpperInvariant();
            return status switch
            {
                "ANOMALY" => AnomalyBrush,
                "POST_REBOOT_RECOVERY_TIMEOUT" => AnomalyBrush,
                "MIDSTREAM_INTERMITTENT_DROPOUT" => AnomalyBrush,

                "PASS" => PassBrush,
                "CONNECTED" => PassBrush,
                "SUCCESS" => PassBrush,
                "STREAMING" => PassBrush,
                "ACTIVE" => PassBrush,
                "RECEIVING" => PassBrush,
                "RECEIVED" => PassBrush,
                "VIDEO RECEIVED" => PassBrush,
                "DISPLAYING" => PassBrush,
                "DISPLAYING_VIDEO" => PassBrush,
                "DISPLAYING VIDEO" => PassBrush,
                "LOCKED" => PassBrush,
                "SYNCHRONIZED" => PassBrush,
                "HAS_VIDEO" => PassBrush,
                "VIDEO ACTIVE" => PassBrush,
                "VIDEO_ACTIVE" => PassBrush,
                "NONE" => PassBrush,

                "WARN" => WarnBrush,
                "UNSUBSCRIBED" => WarnBrush,
                "INFO" => WarnBrush,
                "NO EDID" => WarnBrush,
                "MUTED" => WarnBrush,
                "CLOCK UNSTABLE" => WarnBrush,
                "WAITING_FOR_SOURCE" => WarnBrush,
                "WAITING FOR SOURCE" => WarnBrush,
                "SOURCE_REBOOTING" => WarnBrush,
                "NO_VIDEO" => WarnBrush,
                "REBOOTING" => WarnBrush,

                "FAIL" => FailBrush,
                "ERROR" => FailBrush,
                "DISCONNECTED" => FailBrush,
                "NO STREAM" => FailBrush,
                "NO VIDEO STREAM" => FailBrush,
                "NO_STREAM" => FailBrush,
                "NO CABLE / OFF" => FailBrush,
                "NO CABLE / DISPLAY OFF" => FailBrush,
                "NO_DISPLAY" => FailBrush,
                "HANDSHAKE FAILED" => FailBrush,
                "HANDSHAKE_FAILED" => FailBrush,
                "NO TMDS" => FailBrush,
                "HDCP BLOCKED" => FailBrush,
                "BLACK_SCREEN" => FailBrush,
                "BLACK SCREEN (HDCP)" => FailBrush,
                "DOWN" => FailBrush,
                "DECODER_PLL_DESYNC" => FailBrush,
                "DECODER_DUAL_DESYNC" => FailBrush,
                "DECODER_STREAM_LOSS" => FailBrush,
                "DECODER_DHCP_FAULT" => FailBrush,
                "DECODER_POWER_LOSS" => FailBrush,
                "NETWORK_LINK_DOWN" => FailBrush,
                "DISPLAY_HPD_DOWN" => FailBrush,
                "DISPLAY_EDID_CORRUPT" => FailBrush,
                "DISPLAY_HDCP_BLOCKED" => FailBrush,
                "SERVER_TIMEOUT" => FailBrush,

                _ => MutedBrush
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class StatusToBgBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush PassBg = new(Color.FromArgb(50, 16, 185, 129));
        private static readonly SolidColorBrush WarnBg = new(Color.FromArgb(50, 245, 158, 11));
        private static readonly SolidColorBrush FailBg = new(Color.FromArgb(50, 239, 68, 68));
        private static readonly SolidColorBrush AnomalyBg = new(Color.FromArgb(60, 168, 85, 247));
        private static readonly SolidColorBrush MutedBg = new(Color.FromArgb(40, 100, 116, 139));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string? status = value?.ToString()?.ToUpperInvariant();
            return status switch
            {
                "ANOMALY" or "POST_REBOOT_RECOVERY_TIMEOUT" or "MIDSTREAM_INTERMITTENT_DROPOUT" => AnomalyBg,
                "PASS" or "CONNECTED" or "SUCCESS" or "STREAMING" or "ACTIVE" or "RECEIVING" or "RECEIVED" or "VIDEO RECEIVED" or "DISPLAYING" or "DISPLAYING_VIDEO" or "DISPLAYING VIDEO" or "LOCKED" or "SYNCHRONIZED" or "HAS_VIDEO" or "VIDEO ACTIVE" or "VIDEO_ACTIVE" or "NONE" => PassBg,
                "WARN" or "UNSUBSCRIBED" or "INFO" or "NO EDID" or "MUTED" or "CLOCK UNSTABLE" or "WAITING_FOR_SOURCE" or "WAITING FOR SOURCE" or "SOURCE_REBOOTING" or "NO_VIDEO" or "REBOOTING" => WarnBg,
                "FAIL" or "ERROR" or "DISCONNECTED" or "NO STREAM" or "NO VIDEO STREAM" or "NO_STREAM" or "NO CABLE / OFF" or "NO CABLE / DISPLAY OFF" or "NO_DISPLAY" or "HANDSHAKE FAILED" or "HANDSHAKE_FAILED" or "NO TMDS" or "HDCP BLOCKED" or "BLACK_SCREEN" or "BLACK SCREEN (HDCP)" or "DOWN" or "DECODER_PLL_DESYNC" or "DECODER_DUAL_DESYNC" or "DECODER_STREAM_LOSS" or "DECODER_DHCP_FAULT" or "DECODER_POWER_LOSS" or "NETWORK_LINK_DOWN" or "DISPLAY_HPD_DOWN" or "DISPLAY_EDID_CORRUPT" or "DISPLAY_HDCP_BLOCKED" or "SERVER_TIMEOUT" => FailBg,
                _ => MutedBg
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class InverseNullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value == null ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class BoolToColorConverter : IValueConverter
    {
        private static readonly SolidColorBrush TrueBrush = new(Color.FromRgb(16, 185, 129));
        private static readonly SolidColorBrush FalseBrush = new(Color.FromRgb(239, 68, 68));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
            {
                return b ? TrueBrush : FalseBrush;
            }
            return FalseBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b) return !b;
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b) return !b;
            return false;
        }
    }
}
