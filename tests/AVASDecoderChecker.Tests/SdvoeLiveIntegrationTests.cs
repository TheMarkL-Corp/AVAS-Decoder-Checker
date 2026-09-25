using System.Threading.Tasks;
using AVASDecoderChecker.Services;
using Xunit;

namespace AVASDecoderChecker.Tests
{
    public class SdvoeLiveIntegrationTests
    {
        [Fact]
        public async Task TestConnection_WithLocalhostAnd127_BothSucceed()
        {
            var service = new SdvoeService();
            var check = await service.TestConnectionAsync("127.0.0.1", 8090, 1000);
            if (!check.Success)
            {
                // Local controlserver.exe not running in this environment, skip live test
                return;
            }

            var res127 = await service.TestConnectionAsync("127.0.0.1", 8090, 3000);
            Assert.True(res127.Success, $"Failed on 127.0.0.1: {res127.Message}");

            var resLocalhost = await service.TestConnectionAsync("localhost", 8090, 3000);
            Assert.True(resLocalhost.Success, $"Failed on localhost: {resLocalhost.Message}");
        }

        [Fact]
        public async Task QueryDecoders_WithLocalhost_DiscoversDevices()
        {
            var service = new SdvoeService();
            var check = await service.TestConnectionAsync("127.0.0.1", 8090, 1000);
            if (!check.Success)
            {
                // Local controlserver.exe not running in this environment, skip live test
                return;
            }

            var decoders = await service.QueryDecodersAsync("localhost", 8090, 3000);
            Assert.NotEmpty(decoders);
        }
    }
}
