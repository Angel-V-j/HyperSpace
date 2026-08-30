using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("HyperSpace.MathChecks")]

namespace HyperSpace.Diagnostics;

/// <summary>
/// Runs independent indexed work on the reusable .NET thread pool.
/// Simulation phases call this sequentially, so parallel loops are never nested.
/// </summary>
internal static class ParallelWork
{
    private static readonly ParallelOptions[] Options = CreateOptions();
    private static int _maximumWorkerCount = ReadWorkerLimit();

    public static int MaximumWorkerCount
    {
        get => _maximumWorkerCount;
        set => _maximumWorkerCount = Math.Clamp(value, 1, Environment.ProcessorCount);
    }

    public static int WorkerCountFor(int itemCount, int minimumItemsPerWorker)
    {
        if (itemCount <= 0)
        {
            return 0;
        }

        return Math.Min(
            MaximumWorkerCount,
            Math.Max(1, (itemCount + minimumItemsPerWorker - 1) / minimumItemsPerWorker));
    }

    public static void ForRanges(
        int itemCount,
        int minimumItemsPerWorker,
        Action<int, int, int> body)
    {
        var workerCount = WorkerCountFor(itemCount, minimumItemsPerWorker);
        if (workerCount <= 1)
        {
            if (itemCount > 0)
            {
                body(0, 0, itemCount);
            }
            return;
        }

        Parallel.For(
            0,
            workerCount,
            Options[workerCount],
            workerIndex =>
            {
                var start = (int)((long)itemCount * workerIndex / workerCount);
                var end = (int)((long)itemCount * (workerIndex + 1) / workerCount);
                body(workerIndex, start, end);
            });
    }

    private static ParallelOptions[] CreateOptions()
    {
        var options = new ParallelOptions[Environment.ProcessorCount + 1];
        for (var index = 1; index < options.Length; index++)
        {
            options[index] = new ParallelOptions { MaxDegreeOfParallelism = index };
        }
        return options;
    }

    private static int ReadWorkerLimit() =>
        int.TryParse(Environment.GetEnvironmentVariable("HYPERSPACE_WORKERS"), out var limit)
            ? Math.Clamp(limit, 1, Environment.ProcessorCount)
            : Environment.ProcessorCount;
}
