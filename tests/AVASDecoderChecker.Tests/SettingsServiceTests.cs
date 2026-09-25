using System;
using System.IO;
using System.Threading.Tasks;
using AVASDecoderChecker.Models;
using AVASDecoderChecker.Services;
using Xunit;

namespace AVASDecoderChecker.Tests
{
    public class SettingsServiceTests : IDisposable
    {
        private readonly string _tempFile;

        public SettingsServiceTests()
        {
            _tempFile = Path.Combine(Path.GetTempPath(), $"avas_settings_{Guid.NewGuid():N}.json");
        }

        public void Dispose()
        {
            if (File.Exists(_tempFile))
            {
                File.Delete(_tempFile);
            }
        }

        [Fact]
        public async Task LoadSettings_WhenFileDoesNotExist_ReturnsDefaultSettings()
        {
            var service = new SettingsService(_tempFile);
            var settings = await service.LoadSettingsAsync();

            Assert.NotNull(settings);
            Assert.Equal("127.0.0.1", settings.ControlServerIp);
            Assert.Equal(8090, settings.ControlServerPort);
        }

        [Fact]
        public async Task SaveSettings_ThenLoadSettings_RoundtripsSuccessfully()
        {
            var service = new SettingsService(_tempFile);
            var original = new AppSettings
            {
                ControlServerIp = "192.168.10.50",
                ControlServerPort = 6970,
                PollingIntervalSeconds = 1.5,
                TimeoutMs = 5000,
                DurationMinutes = 10,
                SelectedDecoderMacs = new() { "00:0e:c6:aa:bb:01", "00:0e:c6:aa:bb:02" }
            };

            await service.SaveSettingsAsync(original);

            Assert.True(File.Exists(_tempFile));

            var loaded = await service.LoadSettingsAsync();
            Assert.NotNull(loaded);
            Assert.Equal("192.168.10.50", loaded.ControlServerIp);
            Assert.Equal(6970, loaded.ControlServerPort);
            Assert.Equal(1.5, loaded.PollingIntervalSeconds);
            Assert.Equal(5000, loaded.TimeoutMs);
            Assert.Equal(10, loaded.DurationMinutes);
            Assert.Equal(2, loaded.SelectedDecoderMacs.Count);
            Assert.Contains("00:0e:c6:aa:bb:01", loaded.SelectedDecoderMacs);
            Assert.Contains("00:0e:c6:aa:bb:02", loaded.SelectedDecoderMacs);
        }
    }
}
