using System;
using System.Collections.Generic;
using HyperSpace.Mathematics;

namespace HyperSpace.Geometry;

/// <summary>
/// An immutable sampled quaternion Julia dataset. It is a point cloud, not a mesh.
/// </summary>
public sealed class QuaternionJuliaSet4D : IGeometry4D
{
    private readonly FractalSample4D[] _samples;
    private readonly Vector4D[] _vertices;

    public QuaternionJuliaSet4D(
        JuliaParameters parameters,
        FractalSample4D[] samples,
        TimeSpan generationTime)
    {
        Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        _samples = samples ?? throw new ArgumentNullException(nameof(samples));
        _vertices = new Vector4D[samples.Length];

        var boundedCount = 0;
        for (var index = 0; index < samples.Length; index++)
        {
            _vertices[index] = samples[index].Position;
            if (samples[index].IsBounded)
            {
                boundedCount++;
            }
        }

        BoundedPointCount = boundedCount;
        GenerationTime = generationTime;
    }

    public string Name => "4D Quaternion Julia Set";

    public GeometryVisualStyle4D VisualStyle => GeometryVisualStyle4D.Fractal;

    public IReadOnlyList<Vector4D> Vertices => _vertices;

    public IReadOnlyList<Edge> Edges => Array.Empty<Edge>();

    public IReadOnlyList<Face4D> Faces => Array.Empty<Face4D>();

    public IReadOnlyList<Cell4D> Cells => Array.Empty<Cell4D>();

    public string ResolutionDescription =>
        $"{Parameters.Resolution}^4 = {_samples.Length:N0} samples";

    public JuliaParameters Parameters { get; }

    public IReadOnlyList<FractalSample4D> Samples => _samples;

    public int BoundedPointCount { get; }

    public int EscapedPointCount => _samples.Length - BoundedPointCount;

    public TimeSpan GenerationTime { get; }

    public static QuaternionJuliaSet4D Empty(JuliaParameters parameters) =>
        new(parameters, [], TimeSpan.Zero);
}
