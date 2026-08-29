using HyperSpace.Geometry;
using Microsoft.Xna.Framework;

namespace HyperSpace.Rendering;

/// <summary>
/// One shared palette for scene rendering and the on-screen legend.
/// </summary>
public static class VisualizationPalette
{
    public const float CellSurfaceAlpha = 0.18f;

    private static readonly Color[] Cells =
    [
        new(205, 92, 104),  // X-
        new(220, 142, 75),  // X+
        new(205, 185, 86),  // Y-
        new(91, 174, 112),  // Y+
        new(70, 169, 177),  // Z-
        new(84, 123, 205),  // Z+
        new(140, 105, 204), // W-
        new(194, 92, 171)   // W+
    ];

    public static readonly Color EdgeX = new(242, 116, 116);
    public static readonly Color EdgeY = new(111, 220, 139);
    public static readonly Color EdgeZ = new(91, 166, 246);
    public static readonly Color EdgeW = new(208, 126, 238);

    public static readonly Color VertexNegativeW = new(105, 215, 255);
    public static readonly Color VertexPositiveW = new(255, 197, 92);

    public static readonly Color HypersphereNegativeW = new(60, 201, 222);
    public static readonly Color HyperspherePositiveW = new(216, 102, 235);
    public static readonly Color SimplexNegativeW = new(255, 161, 78);
    public static readonly Color SimplexPositiveW = new(255, 224, 112);
    public static readonly Color IrregularNegativeW = new(79, 206, 146);
    public static readonly Color IrregularPositiveW = new(245, 103, 122);
    public static readonly Color SpiralNegativeW = new(50, 211, 255);
    public static readonly Color SpiralPositiveW = new(255, 88, 190);
    public static readonly Color FractalNegativeW = new(55, 190, 255);
    public static readonly Color FractalZeroW = new(198, 218, 255);
    public static readonly Color FractalPositiveW = new(255, 80, 174);
    public static readonly Color FractalFastEscape = new(35, 64, 125);
    public static readonly Color FractalSlowEscape = new(255, 182, 74);
    public static readonly Color FractalBounded = new(244, 230, 255);
    public static readonly Color PhysicsParticleNegativeW = new(78, 205, 255);
    public static readonly Color PhysicsParticlePositiveW = new(255, 111, 187);
    public static readonly Color PhysicsParticleSelected = new(255, 224, 92);
    public static readonly Color PhysicsPlane = new(108, 225, 190, 72);
    public static readonly Color GravityCentralMass = new(255, 142, 72);
    public static readonly Color GravityOrbiter = new(255, 232, 112);
    public static readonly Color GravityField = new(255, 176, 84);
    public static readonly Color GravityTrailNegativeW = new(66, 196, 255);
    public static readonly Color GravityTrailZeroW = new(216, 231, 255);
    public static readonly Color GravityTrailPositiveW = new(255, 79, 181);
    public static readonly Color CurveStart = new(91, 232, 139);
    public static readonly Color CurveEnd = new(255, 207, 82);

    public static readonly Color Grid = new(115, 125, 155, 40);
    public static readonly Color AxisX = new(190, 90, 95, 82);
    public static readonly Color AxisY = new(90, 175, 110, 82);
    public static readonly Color AxisZ = new(80, 125, 200, 82);
    public static readonly Color AxisW = new(160, 100, 195, 70);

    public static readonly Color RotationAccent = new(93, 205, 255);
    public static readonly Color TransformAccent = new(122, 193, 164);
    public static readonly Color SystemAccent = new(214, 154, 94);
    public static readonly Color DisplayAccent = new(174, 128, 224);
    public static readonly Color ObjectInfoAccent = new(90, 145, 210);
    public static readonly Color CurveAccent = new(58, 205, 210);
    public static readonly Color FractalAccent = new(224, 102, 191);
    public static readonly Color PhysicsAccent = new(91, 220, 164);
    public static readonly Color GravityLabAccent = new(255, 166, 82);

    public static int CellColorCount => Cells.Length;

    public static Color CellColor(int index) => Cells[index];

    public static Color CellColor(int index, GeometryVisualStyle4D style) =>
        style == GeometryVisualStyle4D.Simplex
            ? Cells[(index + 1) % Cells.Length]
            : Cells[index % Cells.Length];

    public static Color EdgeColor(CoordinateAxis4D? axis) =>
        axis switch
        {
            CoordinateAxis4D.X => EdgeX,
            CoordinateAxis4D.Y => EdgeY,
            CoordinateAxis4D.Z => EdgeZ,
            CoordinateAxis4D.W => EdgeW,
            _ => Color.White
        };

    public static Color WDepthColor(GeometryVisualStyle4D style, float amount)
    {
        var (negative, positive) = style switch
        {
            GeometryVisualStyle4D.Hypersphere =>
                (HypersphereNegativeW, HyperspherePositiveW),
            GeometryVisualStyle4D.Simplex =>
                (SimplexNegativeW, SimplexPositiveW),
            GeometryVisualStyle4D.Irregular =>
                (IrregularNegativeW, IrregularPositiveW),
            GeometryVisualStyle4D.Spiral =>
                (SpiralNegativeW, SpiralPositiveW),
            _ => (VertexNegativeW, VertexPositiveW)
        };

        return Color.Lerp(negative, positive, amount);
    }

    public static Color FractalWDepthColor(double normalizedW)
    {
        var clamped = (float)System.Math.Clamp(normalizedW, -1.0, 1.0);
        return clamped < 0.0f
            ? Color.Lerp(FractalZeroW, FractalNegativeW, -clamped)
            : Color.Lerp(FractalZeroW, FractalPositiveW, clamped);
    }

    public static Color GravityTrailWColor(double worldW)
    {
        const double fullGradientDistance = 3.0;
        var normalized = (float)System.Math.Clamp(worldW / fullGradientDistance, -1.0, 1.0);
        return normalized < 0.0f
            ? Color.Lerp(GravityTrailZeroW, GravityTrailNegativeW, -normalized)
            : Color.Lerp(GravityTrailZeroW, GravityTrailPositiveW, normalized);
    }
}
