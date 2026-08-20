using Wintangle.Core.Update;
using Xunit;

namespace Wintangle.Core.Tests.Update;

public class GitHubReleaseParserTests
{
    [Fact]
    public void TryParse_ValidPayloadWithAsset_ReturnsReleaseInfo()
    {
        string json = """
        {
          "tag_name": "v1.0.6",
          "html_url": "https://github.com/grzesiupia/wintangle/releases/tag/v1.0.6",
          "body": "Bug fixes and improvements",
          "draft": false,
          "prerelease": false,
          "assets": [
            {
              "name": "other-file.zip",
              "browser_download_url": "https://github.com/grzesiupia/wintangle/releases/download/v1.0.6/other-file.zip",
              "size": 1000
            },
            {
              "name": "wintangle-setup.exe",
              "browser_download_url": "https://github.com/grzesiupia/wintangle/releases/download/v1.0.6/wintangle-setup.exe",
              "size": 12345678
            }
          ]
        }
        """;

        bool success = GitHubReleaseParser.TryParse(json, out var release);

        Assert.True(success);
        Assert.NotNull(release);
        Assert.Equal(new ReleaseVersion(1, 0, 6), release.Version);
        Assert.Equal("v1.0.6", release.TagName);
        Assert.Equal("https://github.com/grzesiupia/wintangle/releases/tag/v1.0.6", release.HtmlUrl);
        Assert.Equal("Bug fixes and improvements", release.Body);
        Assert.Equal("https://github.com/grzesiupia/wintangle/releases/download/v1.0.6/wintangle-setup.exe", release.AssetUrl);
        Assert.Equal(12345678, release.AssetSizeBytes);
    }

    [Fact]
    public void TryParse_ValidPayloadWithoutAsset_ReturnsReleaseInfoWithNullAsset()
    {
        string json = """
        {
          "tag_name": "v1.0.6",
          "html_url": "https://github.com/grzesiupia/wintangle/releases/tag/v1.0.6",
          "body": "Release notes without binary",
          "draft": false,
          "prerelease": false,
          "assets": []
        }
        """;

        bool success = GitHubReleaseParser.TryParse(json, out var release);

        Assert.True(success);
        Assert.NotNull(release);
        Assert.Equal(new ReleaseVersion(1, 0, 6), release.Version);
        Assert.Equal("v1.0.6", release.TagName);
        Assert.Null(release.AssetUrl);
        Assert.Equal(0, release.AssetSizeBytes);
    }

    [Fact]
    public void TryParse_PrereleasePayload_ReturnsFalse()
    {
        string json = """
        {
          "tag_name": "v1.1.0-preview",
          "html_url": "https://github.com/grzesiupia/wintangle/releases/tag/v1.1.0-preview",
          "body": "Preview build",
          "draft": false,
          "prerelease": true,
          "assets": [
            {
              "name": "wintangle-setup.exe",
              "browser_download_url": "https://example.com/wintangle-setup.exe",
              "size": 5000
            }
          ]
        }
        """;

        bool success = GitHubReleaseParser.TryParse(json, out var release);

        Assert.False(success);
    }

    [Fact]
    public void TryParse_DraftPayload_ReturnsFalse()
    {
        string json = """
        {
          "tag_name": "v1.0.6",
          "html_url": "https://github.com/grzesiupia/wintangle/releases/tag/v1.0.6",
          "body": "Draft notes",
          "draft": true,
          "prerelease": false,
          "assets": []
        }
        """;

        bool success = GitHubReleaseParser.TryParse(json, out var release);

        Assert.False(success);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{ invalid json }")]
    [InlineData("[]")]
    [InlineData("""{"body": "no tag"}""")]
    [InlineData("""{"tag_name": "invalid-tag"}""")]
    public void TryParse_InvalidJsonOrMissingFields_ReturnsFalse(string? json)
    {
        bool success = GitHubReleaseParser.TryParse(json, out var release);

        Assert.False(success);
    }
}
