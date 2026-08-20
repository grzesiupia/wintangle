namespace Wintangle.Core.Update;

/// <summary>
/// Represents a semantic major.minor.patch release version for wintangle.
/// </summary>
public readonly record struct ReleaseVersion(int Major, int Minor, int Patch) : IComparable<ReleaseVersion>, IComparable
{
    /// <summary>
    /// Attempts to parse a version string such as "v1.0.5", "1.0.5", "1.0.5.0", "v1.2", or "1.0.5-beta".
    /// </summary>
    public static bool TryParse(string? text, out ReleaseVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var span = text.AsSpan().Trim();
        if (span.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            span = span[1..].TrimStart();
        }

        // Strip prerelease or build metadata suffixes (e.g. "-beta", "+build123")
        int dashIdx = span.IndexOf('-');
        int plusIdx = span.IndexOf('+');
        int cutIdx = -1;
        if (dashIdx >= 0 && plusIdx >= 0)
        {
            cutIdx = Math.Min(dashIdx, plusIdx);
        }
        else if (dashIdx >= 0)
        {
            cutIdx = dashIdx;
        }
        else if (plusIdx >= 0)
        {
            cutIdx = plusIdx;
        }

        if (cutIdx >= 0)
        {
            span = span[..cutIdx].TrimEnd();
        }

        if (span.IsEmpty)
        {
            return false;
        }

        int major = 0;
        int minor = 0;
        int patch = 0;

        int segIndex = 0;
        int start = 0;
        for (int i = 0; i <= span.Length; i++)
        {
            if (i == span.Length || span[i] == '.')
            {
                var segment = span[start..i];
                if (segment.IsEmpty)
                {
                    return false;
                }

                if (!int.TryParse(segment, out int val) || val < 0)
                {
                    return false;
                }

                if (segIndex == 0) major = val;
                else if (segIndex == 1) minor = val;
                else if (segIndex == 2) patch = val;
                // segIndex >= 3: optional 4th component (e.g. file version revision), ignored for 3-component semver

                segIndex++;
                start = i + 1;
            }
        }

        if (segIndex == 0)
        {
            return false;
        }

        version = new ReleaseVersion(major, minor, patch);
        return true;
    }

    public int CompareTo(ReleaseVersion other)
    {
        int c = Major.CompareTo(other.Major);
        if (c != 0) return c;
        c = Minor.CompareTo(other.Minor);
        if (c != 0) return c;
        return Patch.CompareTo(other.Patch);
    }

    public int CompareTo(object? obj)
    {
        if (obj is null) return 1;
        if (obj is ReleaseVersion other) return CompareTo(other);
        throw new ArgumentException("Object is not a ReleaseVersion", nameof(obj));
    }

    public static bool operator <(ReleaseVersion left, ReleaseVersion right) => left.CompareTo(right) < 0;
    public static bool operator <=(ReleaseVersion left, ReleaseVersion right) => left.CompareTo(right) <= 0;
    public static bool operator >(ReleaseVersion left, ReleaseVersion right) => left.CompareTo(right) > 0;
    public static bool operator >=(ReleaseVersion left, ReleaseVersion right) => left.CompareTo(right) >= 0;

    public override string ToString() => $"{Major}.{Minor}.{Patch}";

    public string ToDisplayString() => $"v{Major}.{Minor}.{Patch}";
}
