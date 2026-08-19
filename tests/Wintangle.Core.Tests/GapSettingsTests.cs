using Wintangle.Core.Geometry;

namespace Wintangle.Core.Tests;

public class GapSettingsTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(8, 8)]
    [InlineData(50, 50)]
    [InlineData(0, 50)]
    [InlineData(50, 0)]
    public void ValidValues_Construct(int windowGap, int edgeGap)
    {
        var gaps = new GapSettings(windowGap, edgeGap);

        Assert.Equal(windowGap, gaps.WindowGap);
        Assert.Equal(edgeGap, gaps.EdgeGap);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(51, 0)]
    public void WindowGapOutOfRange_Throws(int windowGap, int edgeGap)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GapSettings(windowGap, edgeGap));
    }

    [Theory]
    [InlineData(0, -1)]
    [InlineData(0, 51)]
    public void EdgeGapOutOfRange_Throws(int windowGap, int edgeGap)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GapSettings(windowGap, edgeGap));
    }
}
