using System;
using HyperSpace.Mathematics;

namespace HyperSpace.Geometry;

/// <summary>
/// Samples P(t)=(r1 cos t, r1 sin t, r2 cos kt, r2 sin kt)
/// into vertices and consecutive polyline edges.
/// </summary>
public sealed class Spiral4DGenerator
{
    public Spiral4D Generate(SpiralParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        parameters.Validate();

        var vertices = new Vector4D[parameters.SampleCount];
        var edges = new Edge[parameters.SampleCount - 1];
        var step = (parameters.TEnd - parameters.TStart) / (parameters.SampleCount - 1);

        for (var index = 0; index < parameters.SampleCount; index++)
        {
            var t = parameters.TStart + (index * step);
            vertices[index] = Evaluate(parameters, t);

            if (index > 0)
            {
                edges[index - 1] = new Edge(index - 1, index);
            }
        }

        return new Spiral4D(parameters, vertices, edges);
    }

    public static Vector4D Evaluate(SpiralParameters parameters, double t)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        return new Vector4D(
            parameters.R1 * Math.Cos(t),
            parameters.R1 * Math.Sin(t),
            parameters.R2 * Math.Cos(parameters.K * t),
            parameters.R2 * Math.Sin(parameters.K * t));
    }
}
