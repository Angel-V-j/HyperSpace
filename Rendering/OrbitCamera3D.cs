using System;
using Microsoft.Xna.Framework;

namespace HyperSpace.Rendering;

/// <summary>
/// A conventional 3D orbit camera used only after the 4D to 3D projection.
/// </summary>
public sealed class OrbitCamera3D
{
    private const float MinimumPitch = -1.45f;
    private const float MaximumPitch = 1.45f;
    private const float MinimumDistance = 2.0f;
    private const float MaximumDistance = 20.0f;
    private const float OverlayFramingOffset = 1.0f;

    private static readonly Vector3 OrbitTarget = new(0.0f, OverlayFramingOffset, 0.0f);

    public OrbitCamera3D()
    {
        Reset();
    }

    public float Yaw { get; private set; }

    public float Pitch { get; private set; }

    public float Distance { get; private set; }

    public Matrix View
    {
        get
        {
            var horizontalScale = MathF.Cos(Pitch);
            var position = OrbitTarget + new Vector3(
                MathF.Sin(Yaw) * horizontalScale,
                MathF.Sin(Pitch),
                MathF.Cos(Yaw) * horizontalScale) * Distance;

            // Aim slightly above the 3D origin so the wireframe is framed below
            // the debug overlay without changing the projected geometry itself.
            return Matrix.CreateLookAt(position, OrbitTarget, Vector3.Up);
        }
    }

    public Matrix CreateProjection(float aspectRatio) =>
        Matrix.CreatePerspectiveFieldOfView(
            MathHelper.PiOver4,
            aspectRatio,
            nearPlaneDistance: 0.1f,
            farPlaneDistance: 100.0f);

    public void Orbit(float deltaYaw, float deltaPitch)
    {
        Yaw = MathHelper.WrapAngle(Yaw + deltaYaw);
        Pitch = Math.Clamp(Pitch + deltaPitch, MinimumPitch, MaximumPitch);
    }

    public void Zoom(float distanceDelta)
    {
        Distance = Math.Clamp(Distance + distanceDelta, MinimumDistance, MaximumDistance);
    }

    public void Reset()
    {
        Yaw = 0.55f;
        Pitch = 0.3f;
        Distance = 5.5f;
    }
}
