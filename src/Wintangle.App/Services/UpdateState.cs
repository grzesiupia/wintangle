using Wintangle.Core.Update;

namespace Wintangle.App.Services;

/// <summary>
/// States for the in-app update lifecycle.
/// </summary>
public enum UpdateState
{
    Idle,
    Checking,
    UpToDate,
    UpdateAvailable,
    Downloading,
    Installing,
    Error
}

/// <summary>
/// Result of an update check against the GitHub Releases API.
/// </summary>
public sealed record UpdateCheckResult(
    bool Success,
    bool IsUpdateAvailable,
    ReleaseInfo? Release,
    string? ErrorMessage);
