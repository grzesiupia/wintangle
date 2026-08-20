using Wintangle.Core.Logging;

namespace Wintangle.Core.Tests.Logging;

public class LogRetentionPolicyTests
{
    private readonly DateTime _now = new(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void GetFilesToDelete_NullOrEmpty_ReturnsEmptyList()
    {
        var resultNull = LogRetentionPolicy.GetFilesToDelete(null!, _now);
        var resultEmpty = LogRetentionPolicy.GetFilesToDelete(Array.Empty<(string, DateTime)>(), _now);

        Assert.Empty(resultNull);
        Assert.Empty(resultEmpty);
    }

    [Fact]
    public void GetFilesToDelete_UnderLimitAndWithinAge_KeepsAll()
    {
        var files = new (string, DateTime)[]
        {
            ("file1.log", _now.AddHours(-1)),
            ("file2.log", _now.AddHours(-2)),
            ("file3.log", _now.AddHours(-3)),
        };

        var toDelete = LogRetentionPolicy.GetFilesToDelete(files, _now);

        Assert.Empty(toDelete);
    }

    [Fact]
    public void GetFilesToDelete_MoreThanMaxFiles_DeletesOldestExceedingCount()
    {
        var files = new (string, DateTime)[]
        {
            ("file1.log", _now.AddHours(-1)),
            ("file2.log", _now.AddHours(-2)),
            ("file3.log", _now.AddHours(-3)),
            ("file4.log", _now.AddHours(-4)),
            ("file5.log", _now.AddHours(-5)),
        };

        var toDelete = LogRetentionPolicy.GetFilesToDelete(files, _now);

        Assert.Equal(2, toDelete.Count);
        Assert.Contains("file4.log", toDelete);
        Assert.Contains("file5.log", toDelete);
    }

    [Fact]
    public void GetFilesToDelete_FilesOlderThan3Days_DeletesEvenIfUnderMaxFilesCount()
    {
        var files = new (string, DateTime)[]
        {
            ("recent.log", _now.AddDays(-1)),
            ("old1.log", _now.AddDays(-3).AddMinutes(-1)),
            ("old2.log", _now.AddDays(-5)),
        };

        var toDelete = LogRetentionPolicy.GetFilesToDelete(files, _now);

        Assert.Equal(2, toDelete.Count);
        Assert.Contains("old1.log", toDelete);
        Assert.Contains("old2.log", toDelete);
        Assert.DoesNotContain("recent.log", toDelete);
    }

    [Fact]
    public void GetFilesToDelete_MixedCountAndAgeViolations_IdentifiesAllForDeletion()
    {
        var files = new (string, DateTime)[]
        {
            ("new1.log", _now.AddHours(-1)),
            ("new2.log", _now.AddHours(-2)),
            ("new3.log", _now.AddHours(-3)),
            ("new4.log", _now.AddHours(-4)), // Exceeds count of 3
            ("old1.log", _now.AddDays(-4)),  // Exceeds count AND age
            ("old2.log", _now.AddDays(-10)), // Exceeds count AND age
        };

        var toDelete = LogRetentionPolicy.GetFilesToDelete(files, _now);

        Assert.Equal(3, toDelete.Count);
        Assert.Contains("new4.log", toDelete);
        Assert.Contains("old1.log", toDelete);
        Assert.Contains("old2.log", toDelete);
    }

    [Fact]
    public void GetFilesToDelete_CustomParameters_RespectsProvidedLimits()
    {
        var files = new (string, DateTime)[]
        {
            ("file1.log", _now.AddHours(-1)),
            ("file2.log", _now.AddHours(-2)),
            ("file3.log", _now.AddHours(-3)),
        };

        // Keep at most 1 file
        var toDelete = LogRetentionPolicy.GetFilesToDelete(files, _now, maxFilesToKeep: 1, maxAge: TimeSpan.FromDays(1));

        Assert.Equal(2, toDelete.Count);
        Assert.Contains("file2.log", toDelete);
        Assert.Contains("file3.log", toDelete);
        Assert.DoesNotContain("file1.log", toDelete);
    }
}
