using System;
using HyperSpace.Mathematics;

namespace HyperSpace.Transformations;

/// <summary>
/// A Rotation4D with its six sine/cosine pairs cached for repeated point transforms.
/// </summary>
public readonly struct PreparedRotation4D
{
    private readonly double _xyCosine;
    private readonly double _xySine;
    private readonly double _xzCosine;
    private readonly double _xzSine;
    private readonly double _xwCosine;
    private readonly double _xwSine;
    private readonly double _yzCosine;
    private readonly double _yzSine;
    private readonly double _ywCosine;
    private readonly double _ywSine;
    private readonly double _zwCosine;
    private readonly double _zwSine;

    public PreparedRotation4D(Rotation4D rotation)
    {
        (_xySine, _xyCosine) = Math.SinCos(rotation.XY);
        (_xzSine, _xzCosine) = Math.SinCos(rotation.XZ);
        (_xwSine, _xwCosine) = Math.SinCos(rotation.XW);
        (_yzSine, _yzCosine) = Math.SinCos(rotation.YZ);
        (_ywSine, _ywCosine) = Math.SinCos(rotation.YW);
        (_zwSine, _zwCosine) = Math.SinCos(rotation.ZW);
    }

    public Vector4D Apply(Vector4D vector)
    {
        vector = RotateXY(vector, _xyCosine, _xySine);
        vector = RotateXZ(vector, _xzCosine, _xzSine);
        vector = RotateXW(vector, _xwCosine, _xwSine);
        vector = RotateYZ(vector, _yzCosine, _yzSine);
        vector = RotateYW(vector, _ywCosine, _ywSine);
        return RotateZW(vector, _zwCosine, _zwSine);
    }

    public Vector4D ApplyInverse(Vector4D vector)
    {
        vector = RotateZW(vector, _zwCosine, -_zwSine);
        vector = RotateYW(vector, _ywCosine, -_ywSine);
        vector = RotateYZ(vector, _yzCosine, -_yzSine);
        vector = RotateXW(vector, _xwCosine, -_xwSine);
        vector = RotateXZ(vector, _xzCosine, -_xzSine);
        return RotateXY(vector, _xyCosine, -_xySine);
    }

    private static Vector4D RotateXY(Vector4D value, double cosine, double sine) =>
        new(cosine * value.X - sine * value.Y, sine * value.X + cosine * value.Y, value.Z, value.W);

    private static Vector4D RotateXZ(Vector4D value, double cosine, double sine) =>
        new(cosine * value.X - sine * value.Z, value.Y, sine * value.X + cosine * value.Z, value.W);

    private static Vector4D RotateXW(Vector4D value, double cosine, double sine) =>
        new(cosine * value.X - sine * value.W, value.Y, value.Z, sine * value.X + cosine * value.W);

    private static Vector4D RotateYZ(Vector4D value, double cosine, double sine) =>
        new(value.X, cosine * value.Y - sine * value.Z, sine * value.Y + cosine * value.Z, value.W);

    private static Vector4D RotateYW(Vector4D value, double cosine, double sine) =>
        new(value.X, cosine * value.Y - sine * value.W, value.Z, sine * value.Y + cosine * value.W);

    private static Vector4D RotateZW(Vector4D value, double cosine, double sine) =>
        new(value.X, value.Y, cosine * value.Z - sine * value.W, sine * value.Z + cosine * value.W);
}
