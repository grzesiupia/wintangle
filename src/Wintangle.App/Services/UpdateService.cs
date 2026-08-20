using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using Wintangle.Core.Update;

namespace Wintangle.App.Services;

/// <summary>
/// Service responsible for querying GitHub Releases API, downloading setup packages,
/// and executing in-app updates.
/// </summary>
public class UpdateService
{
    public const string ReleasesEndpoint = "https://api.github.com/repos/grzesiupia/wintangle/releases/latest";

    private static readonly HttpClient s_httpClient = new();

    /// <summary>
    /// Gets the current running application version.
    /// </summary>
    public static ReleaseVersion CurrentVersion
    {
        get
        {
            var asm = typeof(UpdateService).Assembly;
            var infoVer = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (ReleaseVersion.TryParse(infoVer, out var ver))
            {
                return ver;
            }

            var processPath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(processPath) && File.Exists(processPath))
            {
                var fvi = FileVersionInfo.GetVersionInfo(processPath);
                if (ReleaseVersion.TryParse(fvi.ProductVersion ?? fvi.FileVersion, out var fileVer))
                {
                    return fileVer;
                }
            }

            var asmVer = asm.GetName().Version;
            if (asmVer != null)
            {
                return new ReleaseVersion(asmVer.Major, asmVer.Minor, Math.Max(0, asmVer.Build));
            }

            return new ReleaseVersion(1, 0, 0);
        }
    }

    /// <summary>
    /// Checks the latest GitHub release and determines if an update is available.
    /// </summary>
    public async Task<UpdateCheckResult> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, ReleasesEndpoint);
            request.Headers.UserAgent.ParseAdd("wintangle");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");

            using var response = await s_httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new UpdateCheckResult(
                    Success: false,
                    IsUpdateAvailable: false,
                    Release: null,
                    ErrorMessage: $"GitHub API returned status {(int)response.StatusCode} ({response.ReasonPhrase})");
            }

            string json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (!GitHubReleaseParser.TryParse(json, out var release))
            {
                return new UpdateCheckResult(
                    Success: false,
                    IsUpdateAvailable: false,
                    Release: null,
                    ErrorMessage: "Failed to parse release information from GitHub response.");
            }

            bool isUpdateAvailable = release.Version.CompareTo(CurrentVersion) > 0;
            return new UpdateCheckResult(
                Success: true,
                IsUpdateAvailable: isUpdateAvailable,
                Release: release,
                ErrorMessage: null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult(
                Success: false,
                IsUpdateAvailable: false,
                Release: null,
                ErrorMessage: ex.Message);
        }
    }

    /// <summary>
    /// Downloads the setup executable from the release asset into %TEMP% with progress reporting.
    /// </summary>
    public async Task<string> DownloadAsync(ReleaseInfo release, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        if (release == null)
        {
            throw new ArgumentNullException(nameof(release));
        }

        if (string.IsNullOrWhiteSpace(release.AssetUrl))
        {
            throw new InvalidOperationException("Release does not contain a download URL for wintangle-setup.exe.");
        }

        string tempPath = Path.Combine(Path.GetTempPath(), $"wintangle-setup-{release.Version}.exe");
        string partPath = tempPath + ".part";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, release.AssetUrl);
            request.Headers.UserAgent.ParseAdd("wintangle");

            using var response = await s_httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            long totalBytes = response.Content.Headers.ContentLength ?? (release.AssetSizeBytes > 0 ? release.AssetSizeBytes : -1);

            await using (var contentStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
            await using (var fileStream = new FileStream(partPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                byte[] buffer = new byte[81920];
                long totalRead = 0;
                int read;

                while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                    totalRead += read;

                    if (totalBytes > 0 && progress != null)
                    {
                        double percentage = Math.Clamp((double)totalRead / totalBytes, 0.0, 1.0);
                        progress.Report(percentage);
                    }
                }
            }

            File.Move(partPath, tempPath, overwrite: true);
            progress?.Report(1.0);
            return tempPath;
        }
        catch
        {
            try
            {
                if (File.Exists(partPath))
                {
                    File.Delete(partPath);
                }
            }
            catch
            {
                // Ignored to avoid masking original exception
            }

            throw;
        }
    }

    /// <summary>
    /// Launches the installer in silent mode and requests application shutdown.
    /// </summary>
    public static void LaunchInstallerAndQuit(string installerPath)
    {
        if (string.IsNullOrWhiteSpace(installerPath) || !File.Exists(installerPath))
        {
            throw new FileNotFoundException("Installer executable was not found.", installerPath);
        }

        var psi = new ProcessStartInfo
        {
            FileName = installerPath,
            Arguments = "/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS /NORESTART",
            UseShellExecute = true,
        };

        Process.Start(psi);
        Program.RequestQuit();
    }
}
