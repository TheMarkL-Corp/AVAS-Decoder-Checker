using System.Windows;
using AVASDecoderChecker.Services;
using AVASDecoderChecker.ViewModels;

namespace AVASDecoderChecker
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            var settingsService = new SettingsService();
            var sdvoeService = new SdvoeService();
            var loggingService = new CsvLoggingService();
            var monitorEngine = new MonitorEngine(sdvoeService, loggingService);

            var viewModel = new MainViewModel(settingsService, sdvoeService, monitorEngine);
            DataContext = viewModel;

            Loaded += async (s, e) =>
            {
                await viewModel.InitializeAsync();
            };
        }
    }
}