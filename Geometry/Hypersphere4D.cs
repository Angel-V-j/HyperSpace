using System;
using System.Collections.Generic;
using HyperSpace.Mathematics;

namespace HyperSpace.Geometry;

/// <summary>
/// A sampled 3-sphere: x^2 + y^2 + z^2 + w^2 = radius^2.
/// Constant-chi 2-sphere shells provide renderable faces; edges between shells
/// expose the third surface parameter without pretending S3 is a 2D skin.
/// </summary>
public sealed class Hypersphere4D : IGeometry4D
{
    private readonly Vector4D[] _vertices;
    private readonly Edge[] _edges;
    private readonly Face4D[] _faces;

    public Hypersphere4D(
        double radius = 1.25,
        int wSegments = 4,
        int polarSegments = 4,
        int azimuthSegments = 8)
    {
        if (!double.IsFinite(radius) || radius <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(radius));
        }

        if (wSegments < 2 || polarSegments < 2 || azimuthSegments < 3)
        {
            throw new ArgumentOutOfRangeException(
                nameof(wSegments),
                "Use at least 2 W intervals, 2 polar intervals and 3 azimuth intervals.");
        }

        Radius = radius;
        WSegments = wSegments;
        PolarSegments = polarSegments;
        AzimuthSegments = azimuthSegments;
        (_vertices, _edges, _faces) = CreateGeometry();
    }

    public string Name => "Hypersphere S3";

    public GeometryVisualStyle4D VisualStyle => GeometryVisualStyle4D.Hypersphere;

    public IReadOnlyList<Vector4D> Vertices => _vertices;

    public IReadOnlyList<Edge> Edges => _edges;

    public IReadOnlyList<Face4D> Faces => _faces;

    public IReadOnlyList<Cell4D> Cells => Array.Empty<Cell4D>();

    public string ResolutionDescription =>
        $"chi {WSegments}, theta {PolarSegments}, phi {AzimuthSegments}";

    public double Radius { get; }

    public int WSegments { get; }

    public int PolarSegments { get; }

    public int AzimuthSegments { get; }

    private (Vector4D[] Vertices, Edge[] Edges, Face4D[] Faces) CreateGeometry()
    {
        var vertices = new List<Vector4D>();
        var edges = new List<Edge>();
        var faces = new List<Face4D>();
        var layerStarts = new List<int>();

        var positiveWPole = vertices.Count;
        vertices.Add(new Vector4D(0.0, 0.0, 0.0, Radius));

        for (var wLayer = 1; wLayer < WSegments; wLayer++)
        {
            var chi = Math.PI * wLayer / WSegments;
            var spatialRadius = Radius * Math.Sin(chi);
            var w = Radius * Math.Cos(chi);
            var layerStart = vertices.Count;
            layerStarts.Add(layerStart);

            vertices.Add(new Vector4D(0.0, 0.0, spatialRadius, w));
            for (var polar = 1; polar < PolarSegments; polar++)
            {
                var theta = Math.PI * polar / PolarSegments;
                for (var azimuth = 0; azimuth < AzimuthSegments; azimuth++)
                {
                    var phi = 2.0 * Math.PI * azimuth / AzimuthSegments;
                    var sinTheta = Math.Sin(theta);
                    vertices.Add(new Vector4D(
                        spatialRadius * sinTheta * Math.Cos(phi),
                        spatialRadius * sinTheta * Math.Sin(phi),
                        spatialRadius * Math.Cos(theta),
                        w));
                }
            }

            vertices.Add(new Vector4D(0.0, 0.0, -spatialRadius, w));
            AddLayerTopology(layerStart, edges, faces);
        }

        var negativeWPole = vertices.Count;
        vertices.Add(new Vector4D(0.0, 0.0, 0.0, -Radius));
        var verticesPerLayer = VerticesPerLayer;

        AddCollapsedLayerConnections(positiveWPole, layerStarts[0], edges);
        for (var layer = 0; layer < layerStarts.Count - 1; layer++)
        {
            for (var offset = 0; offset < verticesPerLayer; offset++)
            {
                edges.Add(new Edge(layerStarts[layer] + offset, layerStarts[layer + 1] + offset));
            }
        }

        AddCollapsedLayerConnections(layerStarts[^1], negativeWPole, edges, startIsLayer: true);
        return (vertices.ToArray(), edges.ToArray(), faces.ToArray());
    }

    private int VerticesPerLayer => 2 + ((PolarSegments - 1) * AzimuthSegments);

    private void AddLayerTopology(
        int layerStart,
        ICollection<Edge> edges,
        ICollection<Face4D> faces)
    {
        var north = layerStart;
        var firstRing = layerStart + 1;
        var south = layerStart + VerticesPerLayer - 1;

        for (var azimuth = 0; azimuth < AzimuthSegments; azimuth++)
        {
            var next = (azimuth + 1) % AzimuthSegments;
            edges.Add(new Edge(north, firstRing + azimuth));
            faces.Add(new Face4D(north, firstRing + azimuth, firstRing + next));
        }

        for (var polarRing = 0; polarRing < PolarSegments - 1; polarRing++)
        {
            var ringStart = firstRing + (polarRing * AzimuthSegments);
            for (var azimuth = 0; azimuth < AzimuthSegments; azimuth++)
            {
                var next = (azimuth + 1) % AzimuthSegments;
                edges.Add(new Edge(ringStart + azimuth, ringStart + next));

                if (polarRing < PolarSegments - 2)
                {
                    var nextRingStart = ringStart + AzimuthSegments;
                    edges.Add(new Edge(ringStart + azimuth, nextRingStart + azimuth));
                    faces.Add(new Face4D(
                        ringStart + azimuth,
                        nextRingStart + azimuth,
                        nextRingStart + next,
                        ringStart + next));
                }
            }
        }

        var lastRing = firstRing + ((PolarSegments - 2) * AzimuthSegments);
        for (var azimuth = 0; azimuth < AzimuthSegments; azimuth++)
        {
            var next = (azimuth + 1) % AzimuthSegments;
            edges.Add(new Edge(lastRing + azimuth, south));
            faces.Add(new Face4D(lastRing + next, lastRing + azimuth, south));
        }
    }

    private void AddCollapsedLayerConnections(
        int start,
        int end,
        ICollection<Edge> edges,
        bool startIsLayer = false)
    {
        for (var offset = 0; offset < VerticesPerLayer; offset++)
        {
            edges.Add(startIsLayer
                ? new Edge(start + offset, end)
                : new Edge(start, end + offset));
        }
    }
}
