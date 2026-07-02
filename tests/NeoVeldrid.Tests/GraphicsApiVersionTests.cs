using System.Diagnostics;
using Xunit;

namespace NeoVeldrid.Tests
{
    public class GraphicsApiVersionTests
    {
        [Theory]
        [InlineData("2.1", 2, 1, 0, 0)]
        [InlineData("3.3.0", 3, 3, 0, 0)]
        [InlineData("5.3.0.123", 5, 3, 0, 123)]
        [InlineData("4.1.0-beta", 4, 1, 0, 0)]
        [InlineData("4.6.0 NVIDIA 510.06", 4, 6, 0, 0)]
        [InlineData("OpenGL ES 3.2 Mesa 22.0.1", 3, 2, 0, 0)]
        public void VersionString_TryParse_Succeeds(string input, int major, int minor, int subminor, int patch)
        {
            bool success = GraphicsApiVersion.TryParseVersion(input, out var version);
            Assert.True(success);
            Assert.Equal(major, version.Major);
            Assert.Equal(minor, version.Minor);
            Assert.Equal(subminor, version.Subminor);
            Assert.Equal(patch, version.Patch);
        }
    }
}
