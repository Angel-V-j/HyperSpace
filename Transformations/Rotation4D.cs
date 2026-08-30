using System;
using HyperSpace.Mathematics;

namespace HyperSpace.Transformations;

/// <summary>
/// Six plane angles composed in the fixed order XY, XZ, XW, YZ, YW, ZW.
/// Each step is a standard two-dimensional rotation in one coordinate plane.
/// </summary>
public readonly record struct Rotation4D(
    double XY,
    double XZ,
    double XW,
    double YZ,
    double YW,
    double ZW)
{
    public static Rotation4D Identity => default;

    public PreparedRotation4D Prepare() => new(this);

    public Vector4D Apply(Vector4D vector)
    {
        vector = RotateInPlane(vector, RotationPlane4D.XY, XY);
        vector = RotateInPlane(vector, RotationPlane4D.XZ, XZ);
        vector = RotateInPlane(vector, RotationPlane4D.XW, XW);
        vector = RotateInPlane(vector, RotationPlane4D.YZ, YZ);
        vector = RotateInPlane(vector, RotationPlane4D.YW, YW);
        vector = RotateInPlane(vector, RotationPlane4D.ZW, ZW);
        return vector;
    }

    /// <summary>
    /// Applies the inverse composition: opposite angles in reverse order.
    /// </summary>
    public Vector4D ApplyInverse(Vector4D vector)
    {
        vector = RotateInPlane(vector, RotationPlane4D.ZW, -ZW);
        vector = RotateInPlane(vector, RotationPlane4D.YW, -YW);
        vector = RotateInPlane(vector, RotationPlane4D.YZ, -YZ);
        vector = RotateInPlane(vector, RotationPlane4D.XW, -XW);
        vector = RotateInPlane(vector, RotationPlane4D.XZ, -XZ);
        vector = RotateInPlane(vector, RotationPlane4D.XY, -XY);
        return vector;
    }

    public Rotation4D WithAddedAngle(RotationPlane4D plane, double deltaRadians) =>
        plane switch
        {
            // Keep cumulative angles for an educational display: three +90 degree
            // requests should read as +270, although sine/cosine are periodic.
            RotationPlane4D.XY => this with { XY = XY + deltaRadians },
            RotationPlane4D.XZ => this with { XZ = XZ + deltaRadians },
            RotationPlane4D.XW => this with { XW = XW + deltaRadians },
            RotationPlane4D.YZ => this with { YZ = YZ + deltaRadians },
            RotationPlane4D.YW => this with { YW = YW + deltaRadians },
            RotationPlane4D.ZW => this with { ZW = ZW + deltaRadians },
            _ => throw new ArgumentOutOfRangeException(nameof(plane), plane, null)
        };

    public double GetAngle(RotationPlane4D plane) =>
        plane switch
        {
            RotationPlane4D.XY => XY,
            RotationPlane4D.XZ => XZ,
            RotationPlane4D.XW => XW,
            RotationPlane4D.YZ => YZ,
            RotationPlane4D.YW => YW,
            RotationPlane4D.ZW => ZW,
            _ => throw new ArgumentOutOfRangeException(nameof(plane), plane, null)
        };

    private static Vector4D RotateInPlane(
        Vector4D vector,
        RotationPlane4D plane,
        double angle)
    {
        var cosine = Math.Cos(angle);
        var sine = Math.Sin(angle);

        // This is the action of a 4x4 identity matrix whose selected a/b rows
        // and columns contain the 2x2 block [c -s; s c].
        return plane switch
        {
            RotationPlane4D.XY => new(
                cosine * vector.X - sine * vector.Y,
                sine * vector.X + cosine * vector.Y,
                vector.Z,
                vector.W),
            RotationPlane4D.XZ => new(
                cosine * vector.X - sine * vector.Z,
                vector.Y,
                sine * vector.X + cosine * vector.Z,
                vector.W),
            RotationPlane4D.XW => new(
                cosine * vector.X - sine * vector.W,
                vector.Y,
                vector.Z,
                sine * vector.X + cosine * vector.W),
            RotationPlane4D.YZ => new(
                vector.X,
                cosine * vector.Y - sine * vector.Z,
                sine * vector.Y + cosine * vector.Z,
                vector.W),
            RotationPlane4D.YW => new(
                vector.X,
                cosine * vector.Y - sine * vector.W,
                vector.Z,
                sine * vector.Y + cosine * vector.W),
            RotationPlane4D.ZW => new(
                vector.X,
                vector.Y,
                cosine * vector.Z - sine * vector.W,
                sine * vector.Z + cosine * vector.W),
            _ => throw new ArgumentOutOfRangeException(nameof(plane), plane, null)
        };
    }

}
