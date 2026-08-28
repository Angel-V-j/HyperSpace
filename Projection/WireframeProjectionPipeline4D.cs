using System.Collections.Generic;
using HyperSpace.Geometry;
using HyperSpace.Mathematics;
using HyperSpace.Transformations;

namespace HyperSpace.Projection;

/// <summary>
/// Converts 4D wireframe geometry into an explicit projected 3D wireframe.
/// </summary>
public sealed class WireframeProjectionPipeline4D
{
    public Wireframe3D Project(
        IGeometry4D geometry,
        Transform4D objectTransform,
        Camera4D camera,
        PerspectiveProjector4D projector)
        => Project(
            geometry.Vertices,
            geometry.Edges,
            objectTransform,
            camera,
            projector);

    public Wireframe3D Project(
        IReadOnlyList<Vector4D> sourceVertices,
        IReadOnlyList<Edge> sourceEdges,
        Transform4D objectTransform,
        Camera4D camera,
        PerspectiveProjector4D projector)
    {
        var vertices = new ProjectedVertex3D[sourceVertices.Count];

        for (var index = 0; index < sourceVertices.Count; index++)
        {
            var worldPoint = objectTransform.TransformPoint(sourceVertices[index]);
            var cameraPoint = camera.WorldToCameraSpace(worldPoint);

            vertices[index] = projector.TryProject(cameraPoint, out var projectedPoint)
                ? new ProjectedVertex3D(
                    projectedPoint,
                    cameraPoint.W,
                    sourceVertices[index].W,
                    worldPoint.W,
                    IsVisible: true)
                : ProjectedVertex3D.Hidden(cameraPoint.W, sourceVertices[index].W, worldPoint.W);
        }

        return new Wireframe3D(vertices, sourceEdges);
    }
}
