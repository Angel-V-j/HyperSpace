using HyperSpace.Mathematics;
using HyperSpace.Transformations;

namespace HyperSpace.Projection;

/// <summary>
/// A 4D camera whose local +W axis is its forward direction.
/// </summary>
public sealed class Camera4D
{
    public static readonly Vector4D DefaultPosition = new(0.0, 0.0, 0.0, -4.0);

    public Camera4D()
    {
        Reset();
    }

    public Vector4D Position { get; private set; }

    public Rotation4D Orientation { get; private set; }

    /// <summary>
    /// Translates a world point and applies the inverse camera orientation.
    /// </summary>
    public Vector4D WorldToCameraSpace(Vector4D worldPoint) =>
        Orientation.ApplyInverse(worldPoint - Position);

    public void MoveWorld(Vector4D worldOffset)
    {
        Position += worldOffset;
    }

    public void Rotate(RotationPlane4D plane, double deltaRadians)
    {
        Orientation = Orientation.WithAddedAngle(plane, deltaRadians);
    }

    public void Reset()
    {
        Position = DefaultPosition;
        Orientation = Rotation4D.Identity;
    }
}
