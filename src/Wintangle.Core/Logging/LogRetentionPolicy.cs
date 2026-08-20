namespace Wintangle.Core.Logging;

/// <summary>
/// Pure retention policy for log files: keeps at most 3 newest files and drops any files older than 3 days.
/// </summary>
public static class LogRetentionPolicy
{
    /// <summary>Default maximum number of newest log files to retain.</summary>
    public const int DefaultMaxFilesToKeep = 3;

    /// <summary>Default maximum age for log files before deletion.</summary>
    public static readonly TimeSpan DefaultMaxAge = TimeSpan.FromDays(3);

    /// <summary>
    /// Determines which log files should be deleted based on count and age limits.
    /// </summary>
    /// <param name="files">Collection of file paths and their last write UTC timestamps.</param>
    /// <param name="nowUtc">The reference current UTC time.</param>
    /// <param name="maxFilesToKeep">Maximum number of newest files to retain (default 3).</param>
    /// <param name="maxAge">Maximum allowed age of a file before deletion (default 3 days).</param>
    /// <returns>List of file paths that should be deleted.</returns>
    public static IReadOnlyList<string> GetFilesToDelete(
        IEnumerable<(string FilePath, DateTime LastWriteUtc)> files,
        DateTime nowUtc,
        int maxFilesToKeep = DefaultMaxFilesToKeep,
        TimeSpan? maxAge = null)
    {
        if (files == null)
        {
            return Array.Empty<string>();
        }

        var ageLimit = maxAge ?? DefaultMaxAge;
        var ordered = files
            .OrderByDescending(f => f.LastWriteUtc)
            .ThenBy(f => f.FilePath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var toDelete = new List<string>();

        for (int i = 0; i < ordered.Count; i++)
        {
            var (filePath, lastWriteUtc) = ordered[i];
            bool exceedsCount = i >= maxFilesToKeep;
            bool exceedsAge = (nowUtc - lastWriteUtc) > ageLimit;

            if (exceedsCount || exceedsAge)
            {
                toDelete.Add(filePath);
            }
        }

        return toDelete;
    }
}
