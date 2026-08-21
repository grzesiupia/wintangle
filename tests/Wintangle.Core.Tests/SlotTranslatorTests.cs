using System.Drawing;
using Wintangle.Core.Geometry;

namespace Wintangle.Core.Tests;

public class SlotTranslatorTests
{
    [Fact]
    public void TranslateSlot_PreservesRelativePositionAndSize()
    {
        var oldWork = new Rectangle(0, 0, 1920, 1080);
        var newWork = new Rectangle(0, 0, 3840, 2160); // 2× scale
        var oldRect = new Rectangle(16, 16, 1888, 1048);

        var translated = SlotTranslator.TranslateSlot(oldWork, newWork, oldRect);

        Assert.Equal(new Rectangle(32, 32, 3776, 2096), translated);
        // Relative position preserved exactly (cross-multiplied).
        Assert.Equal(oldRect.Left * newWork.Width, translated.Left * oldWork.Width);
        Assert.Equal(oldRect.Top * newWork.Height, translated.Top * oldWork.Height);
    }

    [Fact]
    public void TranslateSlot_OffsetNewFrame_AddsFrameOrigin()
    {
        var oldWork = new Rectangle(0, 0, 1920, 1080);
        var newWork = new Rectangle(100, 200, 1920, 1080); // same size, shifted origin
        var oldRect = new Rectangle(16, 16, 1888, 1048);

        var translated = SlotTranslator.TranslateSlot(oldWork, newWork, oldRect);

        Assert.Equal(new Rectangle(116, 216, 1888, 1048), translated);
    }

    [Fact]
    public void TranslateSlot_NegativeOriginSourceFrame_IsOriginRelative()
    {
        // Monitor left of primary: relative offset 16px inside is computed
        // against the frame origin, not absolute coordinates.
        var oldWork = new Rectangle(-1920, 0, 1920, 1080);
        var newWork = new Rectangle(0, 0, 1920, 1080);
        var oldRect = new Rectangle(-1904, 16, 1888, 1048);

        var translated = SlotTranslator.TranslateSlot(oldWork, newWork, oldRect);

        Assert.Equal(new Rectangle(16, 16, 1888, 1048), translated);
    }

    [Fact]
    public void TranslateSlot_DegenerateSourceFrame_DoesNotThrow()
    {
        var translated = SlotTranslator.TranslateSlot(
            new Rectangle(0, 0, 0, 0),
            new Rectangle(0, 0, 1920, 1080),
            new Rectangle(10, 10, 100, 100));

        // Falls back to scale 1.0, offset by newWork origin.
        Assert.Equal(new Rectangle(10, 10, 100, 100), translated);
    }

    [Fact]
    public void TranslateSlot_FullscreenRect_AcrossEqualWorkAreas_IsIdentity()
    {
        var oldWork = new Rectangle(0, 0, 1920, 1080);
        var newWork = new Rectangle(3840, 0, 1920, 1080); // same size, shifted origin
        var oldRect = new Rectangle(8, 8, 1904, 1064);

        var translated = SlotTranslator.TranslateSlot(oldWork, newWork, oldRect);

        Assert.Equal(new Rectangle(3848, 8, 1904, 1064), translated);
    }

    [Fact]
    public void TranslateSlot_FullscreenRect_AcrossDifferentSizedWorkAreas_ScalesToFill()
    {
        var oldWork = new Rectangle(0, 0, 1920, 1080);
        var newWork = new Rectangle(0, 0, 3840, 2160); // 2× scale
        var oldRect = new Rectangle(8, 8, 1904, 1064);

        var translated = SlotTranslator.TranslateSlot(oldWork, newWork, oldRect);

        Assert.Equal(new Rectangle(16, 16, 3808, 2128), translated);
    }

    [Fact]
    public void ClampToWorkArea_RectFullyInside_IsUnchanged()
    {
        var rect = new Rectangle(100, 100, 200, 200);
        var work = new Rectangle(0, 0, 300, 300);

        Assert.Equal(rect, SlotTranslator.ClampToWorkArea(rect, work));
    }

    [Fact]
    public void ClampToWorkArea_OversizedRect_ShrinksAndPinsToOrigin()
    {
        var rect = new Rectangle(0, 0, 400, 400);
        var work = new Rectangle(100, 100, 300, 300);

        Assert.Equal(new Rectangle(100, 100, 300, 300), SlotTranslator.ClampToWorkArea(rect, work));
    }

    [Fact]
    public void ClampToWorkArea_NegativeOffsets_PinToTopLeft()
    {
        var rect = new Rectangle(-50, -50, 100, 100);
        var work = new Rectangle(0, 0, 300, 300);

        Assert.Equal(new Rectangle(0, 0, 100, 100), SlotTranslator.ClampToWorkArea(rect, work));
    }

    [Fact]
    public void ClampToWorkArea_OverflowRightBottom_PinsInside()
    {
        var rect = new Rectangle(250, 250, 100, 100);
        var work = new Rectangle(0, 0, 300, 300);

        var clamped = SlotTranslator.ClampToWorkArea(rect, work);

        Assert.Equal(new Rectangle(200, 200, 100, 100), clamped);
        Assert.True(clamped.Right <= work.Right && clamped.Bottom <= work.Bottom);
    }

    [Fact]
    public void ClampToWorkArea_NegativeOriginWorkArea_KeepsRectInside()
    {
        var work = new Rectangle(-1920, 0, 1920, 1080);

        // Fits already → unchanged.
        var inside = new Rectangle(-1000, 0, 100, 100);
        Assert.Equal(inside, SlotTranslator.ClampToWorkArea(inside, work));

        // Overflows left edge → pinned.
        var overflow = new Rectangle(-2000, 0, 300, 300);
        var clamped = SlotTranslator.ClampToWorkArea(overflow, work);
        Assert.Equal(new Rectangle(-1920, 0, 300, 300), clamped);
    }

    [Fact]
    public void ApplyMinMax_MinTrackForcesMinSize_AndReclampsPosition()
    {
        var rect = new Rectangle(10, 20, 30, 40);
        var min = new Size(50, 60);
        var max = new Size(0, 0); // no cap

        var result = SlotTranslator.ApplyMinMax(rect, min, max);

        Assert.Equal(50, result.Width);
        Assert.Equal(60, result.Height);
        // Bottom-right corner stays anchored at the original right/bottom edge.
        Assert.Equal(rect.Right, result.Right);
        Assert.Equal(rect.Bottom, result.Bottom);
    }

    [Fact]
    public void ApplyMinMax_MaxTrackCapsSize_PositionUnchanged()
    {
        var rect = new Rectangle(10, 20, 200, 200);
        var min = new Size(0, 0);
        var max = new Size(100, 150);

        var result = SlotTranslator.ApplyMinMax(rect, min, max);

        Assert.Equal(new Rectangle(10, 20, 100, 150), result);
    }

    [Fact]
    public void ApplyMinMax_NoLimits_IsIdentity()
    {
        var rect = new Rectangle(10, 20, 50, 60);
        var noLimit = new Size(0, 0);

        Assert.Equal(rect, SlotTranslator.ApplyMinMax(rect, noLimit, noLimit));
    }
}
