using Wintangle.Core.Update;
using Xunit;

namespace Wintangle.Core.Tests.Update;

public class ReleaseVersionTests
{
    [Theory]
    [InlineData("v1.0.5", 1, 0, 5)]
    [InlineData("1.0.5", 1, 0, 5)]
    [InlineData("1.0.5.0", 1, 0, 5)]
    [InlineData("v1.2", 1, 2, 0)]
    [InlineData("1", 1, 0, 0)]
    [InlineData("1.0.5-beta", 1, 0, 5)]
    [InlineData("v1.0.5-rc.1+2023", 1, 0, 5)]
    [InlineData("  v2.1.0  ", 2, 1, 0)]
    [InlineData("v1.2+build456", 1, 2, 0)]
    public void TryParse_ValidInputs_ReturnsTrueAndCorrectVersion(string input, int major, int minor, int patch)
    {
        bool success = ReleaseVersion.TryParse(input, out var version);

        Assert.True(success);
        Assert.Equal(major, version.Major);
        Assert.Equal(minor, version.Minor);
        Assert.Equal(patch, version.Patch);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("v")]
    [InlineData("v.1")]
    [InlineData("1..0")]
    [InlineData("1.-2.3")]
    [InlineData("v1.a.3")]
    [InlineData("-1.0.0")]
    public void TryParse_InvalidInputs_ReturnsFalse(string? input)
    {
        bool success = ReleaseVersion.TryParse(input, out var version);

        Assert.False(success);
        Assert.Equal(default, version);
    }

    [Fact]
    public void CompareTo_OrdersCorrectly()
    {
        var v100 = new ReleaseVersion(1, 0, 0);
        var v101 = new ReleaseVersion(1, 0, 1);
        var v110 = new ReleaseVersion(1, 1, 0);
        var v200 = new ReleaseVersion(2, 0, 0);
        var v100Dup = new ReleaseVersion(1, 0, 0);

        Assert.True(v100.CompareTo(v101) < 0);
        Assert.True(v101.CompareTo(v100) > 0);
        Assert.True(v101.CompareTo(v110) < 0);
        Assert.True(v110.CompareTo(v200) < 0);
        Assert.Equal(0, v100.CompareTo(v100Dup));

        Assert.True(v100 < v101);
        Assert.True(v100 <= v100Dup);
        Assert.True(v200 > v110);
        Assert.True(v110 >= v101);
    }

    [Fact]
    public void ToString_And_ToDisplayString_FormatCorrectly()
    {
        var version = new ReleaseVersion(1, 0, 5);

        Assert.Equal("1.0.5", version.ToString());
        Assert.Equal("v1.0.5", version.ToDisplayString());
    }
}
