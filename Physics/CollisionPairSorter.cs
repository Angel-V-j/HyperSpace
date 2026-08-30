using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using HyperSpace.Diagnostics;

namespace HyperSpace.Physics;

/// <summary>Reusable stable radix sort for the lexicographic collision resolve order.</summary>
internal sealed class CollisionPairSorter
{
    private (int FirstIndex, int SecondIndex)[] _scratch = [];
    private readonly int[] _offsets = new int[256];
    private int[][] _workerOffsets = [];

    public void Sort(List<(int FirstIndex, int SecondIndex)> pairs, int bodyCount)
    {
        // Supported physics indices fit in 16 bits. Preserve the general fallback
        // for larger callers and avoid radix setup for tiny sparse candidate sets.
        if (pairs.Count < 2_048 || bodyCount > 65_536)
        {
            pairs.Sort();
            return;
        }
        if (_scratch.Length < pairs.Count)
            Array.Resize(ref _scratch, Math.Max(pairs.Count, _scratch.Length * 2));

        var workers = ParallelWork.WorkerCountFor(pairs.Count, 32_768);
        if (workers == 1)
        {
            SortSerial(pairs);
            return;
        }
        EnsureWorkers(workers);
        for (var pass = 0; pass < 4; pass++)
        {
            var shift = pass * 8;
            var sourceIsList = (pass & 1) == 0;
            ParallelWork.ForRanges(pairs.Count, 32_768, (worker, start, end) =>
            {
                var offsets = _workerOffsets[worker];
                Array.Clear(offsets);
                var source = sourceIsList ? CollectionsMarshal.AsSpan(pairs) : _scratch.AsSpan(0, pairs.Count);
                for (var index = start; index < end; index++) offsets[Bucket(source[index], shift)]++;
            });

            // Prefix sums give each worker a disjoint output range in every bucket.
            // Worker order follows source order, preserving stability at every pass.
            var offset = 0;
            for (var bucket = 0; bucket < 256; bucket++)
                for (var worker = 0; worker < workers; worker++)
                {
                    var count = _workerOffsets[worker][bucket];
                    _workerOffsets[worker][bucket] = offset;
                    offset += count;
                }

            ParallelWork.ForRanges(pairs.Count, 32_768, (worker, start, end) =>
            {
                var offsets = _workerOffsets[worker];
                var listSpan = CollectionsMarshal.AsSpan(pairs);
                var scratchSpan = _scratch.AsSpan(0, pairs.Count);
                var source = sourceIsList ? listSpan : scratchSpan;
                var destination = sourceIsList ? scratchSpan : listSpan;
                for (var index = start; index < end; index++)
                {
                    var pair = source[index];
                    destination[offsets[Bucket(pair, shift)]++] = pair;
                }
            });
        }
    }

    private void SortSerial(List<(int FirstIndex, int SecondIndex)> pairs)
    {
        var source = CollectionsMarshal.AsSpan(pairs);
        var destination = _scratch.AsSpan(0, source.Length);
        for (var pass = 0; pass < 4; pass++)
        {
            Array.Clear(_offsets);
            var shift = pass * 8;
            foreach (var pair in source) _offsets[Bucket(pair, shift)]++;
            var offset = 0;
            for (var bucket = 0; bucket < _offsets.Length; bucket++)
            {
                var count = _offsets[bucket];
                _offsets[bucket] = offset;
                offset += count;
            }
            foreach (var pair in source) destination[_offsets[Bucket(pair, shift)]++] = pair;
            var previousSource = source;
            source = destination;
            destination = previousSource;
        }
    }

    private static int Bucket((int FirstIndex, int SecondIndex) pair, int shift) =>
        (int)(((((uint)pair.FirstIndex << 16) | (uint)pair.SecondIndex) >> shift) & 255);

    private void EnsureWorkers(int count)
    {
        if (_workerOffsets.Length >= count) return;
        var previousCount = _workerOffsets.Length;
        Array.Resize(ref _workerOffsets, count);
        for (var index = previousCount; index < count; index++) _workerOffsets[index] = new int[256];
    }
}
