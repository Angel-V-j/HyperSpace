using System.Collections.Generic;
using HyperSpace.Diagnostics;
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
        var objectRotation = objectTransform.Rotation.Prepare();
        var objectScale = objectTransform.Scale;
        var objectPosition = objectTransform.Position;
        var cameraRotation = camera.Orientation.Prepare();
        var cameraPosition = camera.Position;
        var focalDistance = projector.FocalDistance;
        var nearPlane = projector.NearPlane;

        ParallelWork.ForRanges(
            sourceVertices.Count,
            minimumItemsPerWorker: 1_024,
            (_, start, end) =>
            {
                for (var index = start; index < end; index++)
                {
                    var sourcePoint = sourceVertices[index];
                    var worldPoint = objectRotation.Apply(sourcePoint * objectScale) + objectPosition;
                    var cameraPoint = cameraRotation.ApplyInverse(worldPoint - cameraPosition);

                    if (TryProject(cameraPoint, focalDistance, nearPlane, out var projectedPoint))
                    {
                        vertices[index] = new ProjectedVertex3D(
                            projectedPoint,
                            cameraPoint.W,
                            sourcePoint.W,
                            worldPoint.W,
                            IsVisible: true);
                    }
                    else
                    {
                        vertices[index] = ProjectedVertex3D.Hidden(
                            cameraPoint.W,
                            sourcePoint.W,
                            worldPoint.W);
                    }
                }
            });

        return new Wireframe3D(vertices, sourceEdges);
    }

    private static bool TryProject(
        Vector4D cameraPoint,
        double focalDistance,
        double nearPlane,
        out Vector3D projectedPoint)
    {
        if (!cameraPoint.IsFinite || cameraPoint.W <= nearPlane)
        {
            projectedPoint = Vector3D.Zero;
            return false;
        }

        var perspectiveScale = focalDistance / cameraPoint.W;
        projectedPoint = new Vector3D(
            cameraPoint.X * perspectiveScale,
            cameraPoint.Y * perspectiveScale,
            cameraPoint.Z * perspectiveScale);
        return projectedPoint.IsFinite;
    }
}
