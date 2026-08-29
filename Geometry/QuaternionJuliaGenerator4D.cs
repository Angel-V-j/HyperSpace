using System;
using System.Collections.Generic;
using System.Diagnostics;
using HyperSpace.Mathematics;

namespace HyperSpace.Geometry;

/// <summary>
/// Starts deterministic, incremental scans of a four-dimensional sample grid.
/// </summary>
public sealed class QuaternionJuliaGenerator4D
{
    public QuaternionJuliaGeneration4D Start(JuliaParameters parameters) => new(parameters);

    public static FractalSample4D Evaluate(Vector4D point, JuliaParameters parameters)
    {
        parameters.Validate();
        return EvaluateValidated(point, parameters);
    }

    internal static FractalSample4D EvaluateValidated(
        Vector4D point,
        JuliaParameters parameters)
    {
        var q = Quaternion4D.FromVector4D(point);
        var escapeRadiusSquared = parameters.EscapeRadius * parameters.EscapeRadius;

        for (var iteration = 1; iteration <= parameters.MaxIterations; iteration++)
        {
            // A magnitude check before squaring prevents runaway values from
            // being multiplied again. IEEE infinity is treated as an escape.
            var magnitudeSquared = q.SquaredMagnitude;
            if (!double.IsFinite(magnitudeSquared) || magnitudeSquared > escapeRadiusSquared)
            {
                return new FractalSample4D(point, iteration - 1, IsBounded: false);
            }

            q = q.Square() + parameters.Constant;
            magnitudeSquared = q.SquaredMagnitude;
            if (!q.IsFinite ||
                !double.IsFinite(magnitudeSquared) ||
                magnitudeSquared > escapeRadiusSquared)
            {
                return new FractalSample4D(point, iteration, IsBounded: false);
            }
        }

        return new FractalSample4D(point, parameters.MaxIterations, IsBounded: true);
    }
}

/// <summary>
/// Mutable progress for one generation request. MonoGame advances it in small
/// batches, so input and rendering continue between batches without threads.
/// </summary>
public sealed class QuaternionJuliaGeneration4D
{
    private readonly List<FractalSample4D> _samples;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private int _nextSampleIndex;

    internal QuaternionJuliaGeneration4D(JuliaParameters parameters)
    {
        parameters.Validate();
        Parameters = parameters;
        _samples = new List<FractalSample4D>(parameters.TotalSampleCount);
    }

    public JuliaParameters Parameters { get; }

    public int ProcessedSampleCount => _nextSampleIndex;

    public int TotalSampleCount => Parameters.TotalSampleCount;

    public double Progress => TotalSampleCount == 0
        ? 1.0
        : (double)ProcessedSampleCount / TotalSampleCount;

    public TimeSpan Elapsed => _stopwatch.Elapsed;

    public bool IsCompleted { get; private set; }

    public bool IsCancelled { get; private set; }

    public void ProcessBatch(int maximumSampleCount)
    {
        if (maximumSampleCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumSampleCount));
        }

        if (IsCompleted || IsCancelled)
        {
            return;
        }

        var endIndex = Math.Min(TotalSampleCount, _nextSampleIndex + maximumSampleCount);
        while (_nextSampleIndex < endIndex)
        {
            var point = DecodeGridPoint(_nextSampleIndex, Parameters);
            _samples.Add(QuaternionJuliaGenerator4D.EvaluateValidated(point, Parameters));
            _nextSampleIndex++;
        }

        if (_nextSampleIndex == TotalSampleCount)
        {
            IsCompleted = true;
            _stopwatch.Stop();
        }
    }

    public void Cancel()
    {
        if (IsCompleted || IsCancelled)
        {
            return;
        }

        IsCancelled = true;
        _stopwatch.Stop();
    }

    public QuaternionJuliaSet4D CreateResult()
    {
        if (!IsCompleted)
        {
            throw new InvalidOperationException("The Julia generation is not complete.");
        }

        return new QuaternionJuliaSet4D(Parameters, _samples.ToArray(), _stopwatch.Elapsed);
    }

    private static Vector4D DecodeGridPoint(int linearIndex, JuliaParameters parameters)
    {
        var resolution = parameters.Resolution;
        var xIndex = linearIndex % resolution;
        linearIndex /= resolution;
        var yIndex = linearIndex % resolution;
        linearIndex /= resolution;
        var zIndex = linearIndex % resolution;
        var wIndex = linearIndex / resolution;
        var spacing = parameters.GridSpacing;

        return new Vector4D(
            parameters.MinimumCoordinate + (xIndex * spacing),
            parameters.MinimumCoordinate + (yIndex * spacing),
            parameters.MinimumCoordinate + (zIndex * spacing),
            parameters.MinimumCoordinate + (wIndex * spacing));
    }
}
