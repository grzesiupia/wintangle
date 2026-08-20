namespace Wintangle.Core.Geometry;

/// <summary>
/// Gap configuration used when tiling windows.
/// <para><see cref="WindowGap"/> — space left between two adjacent windows.
/// Each window applies the full window gap on its interior edge, so the seam
/// between two neighbors is exactly WindowGap.</para>
/// <para><see cref="EdgeGap"/> — space left between a window and the screen
/// edge. Boundary-touching edges apply only the edge gap (the window gap is
/// not added on top).</para>
/// </summary>
public sealed record GapSettings
{
    public const int MaxGap = 50;

    public int WindowGap { get; }

    public int EdgeGap { get; }

    public GapSettings(int windowGap, int edgeGap)
    {
        if (windowGap is < 0 or > MaxGap)
        {
            throw new ArgumentOutOfRangeException(
                nameof(windowGap),
                windowGap,
                $"WindowGap must be in [0, {MaxGap}].");
        }

        if (edgeGap is < 0 or > MaxGap)
        {
            throw new ArgumentOutOfRangeException(
                nameof(edgeGap),
                edgeGap,
                $"EdgeGap must be in [0, {MaxGap}].");
        }

        WindowGap = windowGap;
        EdgeGap = edgeGap;
    }
}
