namespace Wintangle.Core.Update;

/// <summary>
/// Contains metadata for a published GitHub release.
/// </summary>
public sealed record ReleaseInfo(
    ReleaseVersion Version,
    string TagName,
    string HtmlUrl,
    string Body,
    string? AssetUrl,
    long AssetSizeBytes);
