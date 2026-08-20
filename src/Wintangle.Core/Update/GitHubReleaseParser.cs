using System.Text.Json;

namespace Wintangle.Core.Update;

/// <summary>
/// Parser for GitHub Release API JSON payloads.
/// </summary>
public static class GitHubReleaseParser
{
    private const string TargetAssetName = "wintangle-setup.exe";

    /// <summary>
    /// Parses a GitHub release JSON response into a <see cref="ReleaseInfo"/>.
    /// Ignores prereleases and drafts.
    /// </summary>
    public static bool TryParse(string? json, out ReleaseInfo release)
    {
        release = default!;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            // Ignore drafts and prereleases
            if (root.TryGetProperty("draft", out var draftProp) && draftProp.ValueKind == JsonValueKind.True)
            {
                return false;
            }

            if (root.TryGetProperty("prerelease", out var prereleaseProp) && prereleaseProp.ValueKind == JsonValueKind.True)
            {
                return false;
            }

            if (!root.TryGetProperty("tag_name", out var tagProp) || tagProp.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            string tagName = tagProp.GetString() ?? string.Empty;
            if (!ReleaseVersion.TryParse(tagName, out var version))
            {
                return false;
            }

            string htmlUrl = string.Empty;
            if (root.TryGetProperty("html_url", out var htmlProp) && htmlProp.ValueKind == JsonValueKind.String)
            {
                htmlUrl = htmlProp.GetString() ?? string.Empty;
            }

            string body = string.Empty;
            if (root.TryGetProperty("body", out var bodyProp) && bodyProp.ValueKind == JsonValueKind.String)
            {
                body = bodyProp.GetString() ?? string.Empty;
            }

            string? assetUrl = null;
            long assetSizeBytes = 0;

            if (root.TryGetProperty("assets", out var assetsProp) && assetsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assetsProp.EnumerateArray())
                {
                    if (asset.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    if (asset.TryGetProperty("name", out var nameProp) &&
                        nameProp.ValueKind == JsonValueKind.String &&
                        string.Equals(nameProp.GetString(), TargetAssetName, StringComparison.OrdinalIgnoreCase))
                    {
                        if (asset.TryGetProperty("browser_download_url", out var dlProp) && dlProp.ValueKind == JsonValueKind.String)
                        {
                            assetUrl = dlProp.GetString();
                        }

                        if (asset.TryGetProperty("size", out var sizeProp) && sizeProp.TryGetInt64(out long size))
                        {
                            assetSizeBytes = size;
                        }

                        break;
                    }
                }
            }

            release = new ReleaseInfo(version, tagName, htmlUrl, body, assetUrl, assetSizeBytes);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
