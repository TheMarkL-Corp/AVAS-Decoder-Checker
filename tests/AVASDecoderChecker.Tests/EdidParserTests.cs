using AVASDecoderChecker.Services;
using Xunit;

namespace AVASDecoderChecker.Tests
{
    public class EdidParserTests
    {
        private const string ViewSonicEdidHex = "00ffffffffffff005a63348c11010101091c0103804627782e6875a6564fa2260e5054bfef80d100d1c0b300a94095008180810081c04dd000a0f0703e8030203500b9882100001a000000ff005630453138303930303136370a000000fd00184b0f873c000a202020202020000000fc005650333236382d344b0a202020018b020348f15e61605f5e5d6b6665010204030506070809101112131415161d1e1f20212223090707830100006d030c001000383c20006001020367d85dc401788803e3050000e20fe3a36600a0f0701f8030203500b9882100001a1a6800a0f0381f4030203a00b9882100001a4d6c80a070703e8030203a00b9882100001a00ab";

        [Fact]
        public void Parse_ValidViewSonicEdid_ExtractsCorrectDetails()
        {
            var info = EdidParser.Parse(ViewSonicEdidHex);

            Assert.True(info.IsValid);
            Assert.Equal("VP3268-4K", info.ModelName);
            Assert.Equal("V0E180900167", info.SerialNumber);
            Assert.Equal("ViewSonic", info.ManufacturerName);
            Assert.Equal(3840, info.PreferredWidth);
            Assert.Equal(2160, info.PreferredHeight);
            Assert.Equal(60, (int)info.PreferredRefreshRate);
            Assert.True(info.HasAudioSupport);
        }

        [Fact]
        public void Parse_NullOrEmptyEdid_ReturnsInvalid()
        {
            var info = EdidParser.Parse(null);
            Assert.False(info.IsValid);
            Assert.Equal("Unknown Display", info.ModelName);

            var infoEmpty = EdidParser.Parse("");
            Assert.False(infoEmpty.IsValid);
        }

        [Fact]
        public void Parse_CorruptedEdid_ReturnsInvalid()
        {
            var info = EdidParser.Parse("12345678deadbeef");
            Assert.False(info.IsValid);
        }
    }
}
