using HyperSpace.Mathematics;

namespace HyperSpace.Physics;

public readonly record struct GravityLabDiagnostics4D(
    bool IsAvailable,
    double Distance,
    double Speed,
    double CentralAccelerationMagnitude,
    Vector4D DirectionTowardCentral,
    double OrbiterW)
{
    public static GravityLabDiagnostics4D Unavailable =>
        new(false, 0.0, 0.0, 0.0, Vector4D.Zero, 0.0);
}
