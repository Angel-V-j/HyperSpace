using System;
using HyperSpace.Mathematics;

namespace HyperSpace.Projection;

/// <summary>
/// Projects camera-space 4D points from the origin onto the W = focalDistance
/// hyperplane. Points at or behind the near W hyperplane are rejected safely.
/// </summary>
public sealed class PerspectiveProjector4D
{
    public const double DefaultFocalDistance = 2.5;
    public const double DefaultNearPlane = 0.1;
    public const double MinimumFocalDistance = 0.25;
    public const double MaximumFocalDistance = 8.0;

    public PerspectiveProjector4D(
        double focalDistance = DefaultFocalDistance,
        double nearPlane = DefaultNearPlane)
    {
        if (!double.IsFinite(nearPlane) || nearPlane <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(nearPlane),
                "Near plane must be finite and greater than zero.");
        }

        NearPlane = nearPlane;
        SetFocalDistance(focalDistance);
    }

    public double FocalDistance { get; private set; }

    public double NearPlane { get; }

    public bool TryProject(Vector4D cameraPoint, out Vector3D projectedPoint)
    {
        // W is true camera depth. W <= nearPlane includes the singularity at
        // W = 0, points behind the camera, and numerically unstable points.
        if (!cameraPoint.IsFinite || cameraPoint.W <= NearPlane)
        {
            projectedPoint = Vector3D.Zero;
            return false;
        }

        var perspectiveScale = FocalDistance / cameraPoint.W;
        projectedPoint = new Vector3D(
            cameraPoint.X * perspectiveScale,
            cameraPoint.Y * perspectiveScale,
            cameraPoint.Z * perspectiveScale);

        if (!projectedPoint.IsFinite)
        {
            projectedPoint = Vector3D.Zero;
            return false;
        }

        return true;
    }

    public void AdjustFocalDistance(double delta)
    {
        SetFocalDistance(FocalDistance + delta);
    }

    public void Reset()
    {
        FocalDistance = DefaultFocalDistance;
    }

    private void SetFocalDistance(double focalDistance)
    {
        if (!double.IsFinite(focalDistance))
        {
            throw new ArgumentOutOfRangeException(
                nameof(focalDistance),
                "Focal distance must be finite.");
        }

        FocalDistance = Math.Clamp(
            focalDistance,
            MinimumFocalDistance,
            MaximumFocalDistance);
    }
}
