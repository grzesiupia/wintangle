using Wintangle.Core.Logging;

namespace Wintangle.Core.Tests.Logging;

public class LogRingBufferTests
{
    [Fact]
    public void InitialState_IsEmpty()
    {
        var buffer = new LogRingBuffer(5);

        Assert.Equal(5, buffer.Capacity);
        Assert.Equal(0, buffer.Count);
        Assert.Empty(buffer.Snapshot());
    }

    [Fact]
    public void Add_UnderCapacity_RetainsAllInOrder()
    {
        var buffer = new LogRingBuffer(5);
        buffer.Add("entry 1");
        buffer.Add("entry 2");
        buffer.Add("entry 3");

        Assert.Equal(3, buffer.Count);
        var snapshot = buffer.Snapshot();
        Assert.Equal(new[] { "entry 1", "entry 2", "entry 3" }, snapshot);
    }

    [Fact]
    public void Add_ExceedingCapacity_RollsOverAndPreservesChronologicalOrder()
    {
        var buffer = new LogRingBuffer(3);
        buffer.Add("e1");
        buffer.Add("e2");
        buffer.Add("e3");
        buffer.Add("e4");
        buffer.Add("e5");

        Assert.Equal(3, buffer.Count);
        var snapshot = buffer.Snapshot();
        Assert.Equal(new[] { "e3", "e4", "e5" }, snapshot);
    }

    [Fact]
    public void Clear_EmptiesBuffer()
    {
        var buffer = new LogRingBuffer(3);
        buffer.Add("e1");
        buffer.Add("e2");

        buffer.Clear();

        Assert.Equal(0, buffer.Count);
        Assert.Empty(buffer.Snapshot());

        buffer.Add("e3");
        Assert.Equal(1, buffer.Count);
        Assert.Equal(new[] { "e3" }, buffer.Snapshot());
    }

    [Fact]
    public void Constructor_InvalidCapacity_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LogRingBuffer(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LogRingBuffer(-5));
    }

    [Fact]
    public void Concurrency_MultiThreadedAddAndSnapshot_DoesNotThrowOrCorrupt()
    {
        var buffer = new LogRingBuffer(50);
        const int threadCount = 8;
        const int itemsPerThread = 500;

        Parallel.For(0, threadCount, threadIdx =>
        {
            for (int i = 0; i < itemsPerThread; i++)
            {
                buffer.Add($"thread-{threadIdx}-item-{i}");
                if (i % 50 == 0)
                {
                    _ = buffer.Snapshot();
                }
            }
        });

        Assert.Equal(50, buffer.Count);
        var snapshot = buffer.Snapshot();
        Assert.Equal(50, snapshot.Count);
        foreach (var item in snapshot)
        {
            Assert.NotNull(item);
        }
    }
}
