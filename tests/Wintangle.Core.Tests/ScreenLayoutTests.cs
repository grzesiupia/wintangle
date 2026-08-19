using System.Drawing;
using Wintangle.Core.Geometry;

namespace Wintangle.Core.Tests;

public class ScreenLayoutTests
{
    private static ScreenInfo Screen(string name, int x, int y, int w, int h, bool primary) =>
        new(new Rectangle(x, y, w, h), new Rectangle(x, y, w, h), primary, name);

    [Fact]
    public void OrderScreens_PrimaryFirst_RegardlessOfCoordinates()
    {
        var secondary = Screen("SEC", 0, 0, 1920, 1080, primary: false);
        var primaryLeft = Screen("PRI", -1920, 0, 1920, 1080, primary: true);

        var ordered = ScreenLayout.OrderScreens(new[] { secondary, primaryLeft });

        Assert.Equal(new[] { primaryLeft, secondary }, ordered);
    }

    [Fact]
    public void OrderScreens_SortsByXAscending()
    {
        var screens = new[]
        {
            Screen("C", 1920, 0, 1920, 1080, primary: false),
            Screen("PRI", 0, 0, 1920, 1080, primary: true),
            Screen("A", -1920, 0, 1920, 1080, primary: false),
            Screen("B", -3840, 0, 1920, 1080, primary: false),
        };

        var ordered = ScreenLayout.OrderScreens(screens);

        Assert.Equal(new[] { "PRI", "B", "A", "C" }, ordered.Select(s => s.DeviceName).ToArray());
    }

    [Fact]
    public void OrderScreens_SortsByYAscending_WhenXEqual()
    {
        var screens = new[]
        {
            Screen("MID", 2000, 1080, 1920, 1080, primary: false),
            Screen("TOP", 2000, 0, 1920, 1080, primary: false),
            Screen("PRI", 0, 0, 1920, 1080, primary: true),
            Screen("BOT", 2000, 2160, 1920, 1080, primary: false),
        };

        var ordered = ScreenLayout.OrderScreens(screens);

        Assert.Equal(new[] { "PRI", "TOP", "MID", "BOT" }, ordered.Select(s => s.DeviceName).ToArray());
    }

    [Fact]
    public void OrderScreens_IdenticalCoordinates_KeepEnumerationOrder()
    {
        var first = Screen("FIRST", 2000, 0, 1920, 1080, primary: false);
        var second = Screen("SECOND", 2000, 0, 1920, 1080, primary: false);
        var third = Screen("THIRD", 2000, 0, 1920, 1080, primary: false);

        var ordered = ScreenLayout.OrderScreens(new[] { first, second, third });

        Assert.Equal(new[] { "FIRST", "SECOND", "THIRD" }, ordered.Select(s => s.DeviceName).ToArray());
    }

    [Fact]
    public void OrderScreens_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ScreenLayout.OrderScreens(null!));
    }
}
