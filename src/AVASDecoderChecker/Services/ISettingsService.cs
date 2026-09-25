using System.Threading.Tasks;
using AVASDecoderChecker.Models;

namespace AVASDecoderChecker.Services
{
    public interface ISettingsService
    {
        string SettingsFilePath { get; }
        Task<AppSettings> LoadSettingsAsync();
        Task SaveSettingsAsync(AppSettings settings);
    }
}
