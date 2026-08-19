using System.Drawing;

namespace Wintangle.Core.Geometry;

/// <summary>
/// Translates rectangles between work-area frames and applies window-size
/// constraints. All math is origin-relative so negative-origin work areas
/// (monitors left/above the primary) behave identically to positive ones.
/// </summary>
public static class SlotTranslator
{
    /// <summary>
    /// Maps <paramref name="oldRect"/> from the <paramref name="oldWork"/>
    /// frame into the <paramref name="newWork"/> frame, preserving its
    /// relative position and size (scaled proportionally).
    /// </summary>
    /// <remarks>
    /// Degenerate (zero-sized) source frames fall back to scale 1.0 per axis
    /// instead of throwing.
    /// </remarks>
    public static Rectangle TranslateSlot(Rectangle oldWork, Rectangle newWork, Rectangle oldRect)
    {
        double scaleX = oldWork.Width > 0 ? (double)newWork.Width / oldWork.Width : 1.0;
        double scaleY = oldWork.Height > 0 ? (double)newWork.Height / oldWork.Height : 1.0;

        int x = newWork.X + (int)Math.Round((oldRect.X - oldWork.X) * scaleX, MidpointRounding.AwayFromZero);
        int y = newWork.Y + (int)Math.Round((oldRect.Y - oldWork.Y) * scaleY, MidpointRounding.AwayFromZero);
        int width = (int)Math.Round(oldRect.Width * scaleX, MidpointRounding.AwayFromZero);
        int height = (int)Math.Round(oldRect.Height * scaleY, MidpointRounding.AwayFromZero);

        return new Rectangle(x, y, width, height);
    }

    /// <summary>
    /// Shrinks <paramref name="rect"/> if needed and pins it fully inside
    /// <paramref name="workArea"/> (never below 1×1).
    /// </summary>
    public static Rectangle ClampToWorkArea(Rectangle rect, Rectangle workArea)
    {
        if (workArea.Width < 1 || workArea.Height < 1)
        {
            return new Rectangle(workArea.X, workArea.Y, 1, 1);
        }

        int width = Math.Min(Math.Max(1, rect.Width), workArea.Width);
        int height = Math.Min(Math.Max(1, rect.Height), workArea.Height);

        int x = Math.Clamp(rect.X, workArea.X, workArea.X + workArea.Width - width);
        int y = Math.Clamp(rect.Y, workArea.Y, workArea.Y + workArea.Height - height);

        return new Rectangle(x, y, width, height);
    }

    /// <summary>
    /// Clamps <paramref name="rect"/>'s width/height to the window's track
    /// limits — <paramref name="minTrack"/> is a lower bound (at least),
    /// <paramref name="maxTrack"/> an upper bound (at most; a dimension ≤ 0
    /// means "no cap") — then re-clamps the position.
    /// </summary>
    /// <remarks>
    /// Position re-clamping keeps the rect inside its original footprint:
    /// shrinking keeps the top-left anchor; min-track growth anchors the
    /// bottom-right corner so the window grows left/up instead of overflowing
    /// its slot's right/bottom edge.
    /// </remarks>
    public static Rectangle ApplyMinMax(Rectangle rect, Size minTrack, Size maxTrack)
    {
        int width = rect.Width;
        int height = rect.Height;

        if (minTrack.Width > 0)
        {
            width = Math.Max(width, minTrack.Width);
        }

        if (minTrack.Height > 0)
        {
            height = Math.Max(height, minTrack.Height);
        }

        if (maxTrack.Width > 0)
        {
            width = Math.Min(width, maxTrack.Width);
        }

        if (maxTrack.Height > 0)
        {
            height = Math.Min(height, maxTrack.Height);
        }

        int x = Math.Min(rect.X, rect.Right - width);
        int y = Math.Min(rect.Y, rect.Bottom - height);

        return new Rectangle(x, y, width, height);
    }
}
