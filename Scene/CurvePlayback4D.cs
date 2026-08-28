using System;

namespace HyperSpace.Scene;

/// <summary>
/// Reveals a sampled curve prefix over a fixed duration. It does not mutate geometry.
/// </summary>
public sealed class CurvePlayback4D
{
    public const double DefaultDurationSeconds = 4.0;

    private double _visibleSamplesExact;

    public CurvePlayback4D(int totalSampleCount, double durationSeconds = DefaultDurationSeconds)
    {
        if (!double.IsFinite(durationSeconds) || durationSeconds <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(durationSeconds));
        }

        DurationSeconds = durationSeconds;
        SetTotalSampleCount(totalSampleCount, showComplete: true);
    }

    public int TotalSampleCount { get; private set; }

    public int VisibleSampleCount => Math.Clamp((int)Math.Floor(_visibleSamplesExact), 1, TotalSampleCount);

    public double DurationSeconds { get; }

    public bool IsPlaying { get; private set; }

    public double Progress => TotalSampleCount <= 1
        ? 1.0
        : (VisibleSampleCount - 1.0) / (TotalSampleCount - 1.0);

    public void Play()
    {
        if (VisibleSampleCount >= TotalSampleCount)
        {
            _visibleSamplesExact = 1.0;
        }

        IsPlaying = true;
    }

    public void Reset()
    {
        _visibleSamplesExact = 1.0;
        IsPlaying = false;
    }

    public void ShowComplete()
    {
        _visibleSamplesExact = TotalSampleCount;
        IsPlaying = false;
    }

    public void SetTotalSampleCount(int totalSampleCount, bool showComplete)
    {
        if (totalSampleCount < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(totalSampleCount));
        }

        TotalSampleCount = totalSampleCount;
        if (showComplete)
        {
            ShowComplete();
        }
        else
        {
            Reset();
        }
    }

    public void Update(double elapsedSeconds)
    {
        if (!IsPlaying || elapsedSeconds <= 0.0)
        {
            return;
        }

        var samplesPerSecond = (TotalSampleCount - 1.0) / DurationSeconds;
        _visibleSamplesExact = Math.Min(
            TotalSampleCount,
            _visibleSamplesExact + (samplesPerSecond * elapsedSeconds));

        if (_visibleSamplesExact >= TotalSampleCount)
        {
            IsPlaying = false;
        }
    }
}
