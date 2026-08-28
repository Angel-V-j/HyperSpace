using System;
using HyperSpace.Mathematics;

namespace HyperSpace.Transformations;

/// <summary>
/// Applies one time-based 4D transformation request through incremental updates.
/// It is independent of rendering and never blocks the MonoGame update loop.
/// </summary>
public sealed class TransformationAnimator4D
{
    public const double DefaultDurationSeconds = 1.0;
    public const double QuarterTurnRadians = Math.PI / 2.0;

    private readonly double _durationSeconds;
    private double _elapsedSeconds;
    private double _previousEasedProgress;
    private RotationPlane4D _rotationPlane;
    private Vector4D _translation;
    private double _scaleFactor = 1.0;

    public TransformationAnimator4D(double durationSeconds = DefaultDurationSeconds)
    {
        if (!double.IsFinite(durationSeconds) || durationSeconds <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(durationSeconds),
                "Animation duration must be finite and positive.");
        }

        _durationSeconds = durationSeconds;
    }

    public TransformationAnimationKind4D Kind { get; private set; }

    public bool IsActive => Kind != TransformationAnimationKind4D.None;

    public double Progress => Math.Clamp(_elapsedSeconds / _durationSeconds, 0.0, 1.0);

    public RotationPlane4D? ActiveRotationPlane =>
        Kind == TransformationAnimationKind4D.Rotation ? _rotationPlane : null;

    public string ActiveLabel =>
        Kind switch
        {
            TransformationAnimationKind4D.Rotation => $"{_rotationPlane} rotation",
            TransformationAnimationKind4D.Translation => "4D translation",
            TransformationAnimationKind4D.UniformScale => "Uniform 4D scale",
            _ => "None"
        };

    public double CurrentRotationDegrees =>
        Kind == TransformationAnimationKind4D.Rotation
            ? SmoothStep(Progress) * 90.0
            : 0.0;

    public bool TryStartRotation(RotationPlane4D plane) =>
        TryStart(TransformationAnimationKind4D.Rotation, plane, Vector4D.Zero, 1.0);

    public bool TryStartTranslation(Vector4D worldOffset)
    {
        if (!worldOffset.IsFinite)
        {
            throw new ArgumentOutOfRangeException(nameof(worldOffset), "Translation must be finite.");
        }

        return TryStart(
            TransformationAnimationKind4D.Translation,
            default,
            worldOffset,
            1.0);
    }

    public bool TryStartUniformScale(double factor)
    {
        if (!double.IsFinite(factor) || factor <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(factor), "Scale factor must be finite and positive.");
        }

        return TryStart(
            TransformationAnimationKind4D.UniformScale,
            default,
            Vector4D.Zero,
            factor);
    }

    public void Update(double elapsedSeconds, Transform4D transform)
    {
        ArgumentNullException.ThrowIfNull(transform);

        if (!IsActive || !double.IsFinite(elapsedSeconds) || elapsedSeconds <= 0.0)
        {
            return;
        }

        _elapsedSeconds = Math.Min(_elapsedSeconds + elapsedSeconds, _durationSeconds);
        var easedProgress = SmoothStep(Progress);
        var progressDelta = easedProgress - _previousEasedProgress;

        switch (Kind)
        {
            case TransformationAnimationKind4D.Rotation:
                transform.Rotate(_rotationPlane, QuarterTurnRadians * progressDelta);
                break;
            case TransformationAnimationKind4D.Translation:
                transform.MoveWorld(_translation * progressDelta);
                break;
            case TransformationAnimationKind4D.UniformScale:
                // Exponential interpolation makes scale-up and its reciprocal
                // scale-down follow symmetric paths and compose incrementally.
                var previousFactor = Math.Pow(_scaleFactor, _previousEasedProgress);
                var currentFactor = Math.Pow(_scaleFactor, easedProgress);
                transform.MultiplyScale(currentFactor / previousFactor);
                break;
        }

        _previousEasedProgress = easedProgress;

        if (_elapsedSeconds >= _durationSeconds)
        {
            Cancel();
        }
    }

    public void Cancel()
    {
        Kind = TransformationAnimationKind4D.None;
        _elapsedSeconds = 0.0;
        _previousEasedProgress = 0.0;
        _translation = Vector4D.Zero;
        _scaleFactor = 1.0;
    }

    private bool TryStart(
        TransformationAnimationKind4D kind,
        RotationPlane4D rotationPlane,
        Vector4D translation,
        double scaleFactor)
    {
        if (IsActive)
        {
            return false;
        }

        Kind = kind;
        _rotationPlane = rotationPlane;
        _translation = translation;
        _scaleFactor = scaleFactor;
        _elapsedSeconds = 0.0;
        _previousEasedProgress = 0.0;
        return true;
    }

    private static double SmoothStep(double progress) =>
        progress * progress * (3.0 - 2.0 * progress);
}
