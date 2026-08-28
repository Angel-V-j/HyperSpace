using HyperSpace.Mathematics;

namespace HyperSpace.Geometry;

/// <summary>
/// A 3D projected position plus its original camera-space W depth.
/// </summary>
public readonly record struct ProjectedVertex3D(
    Vector3D Position,
    double CameraDepth4D,
    double SourceW,
    bool IsVisible)
{
    public static ProjectedVertex3D Hidden(double cameraDepth4D, double sourceW) =>
        new(Vector3D.Zero, cameraDepth4D, sourceW, IsVisible: false);
}
