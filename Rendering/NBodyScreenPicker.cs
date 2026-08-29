using System;
using System.Collections.Generic;
using HyperSpace.Geometry;
using HyperSpace.Physics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace HyperSpace.Rendering;

/// <summary>
/// Selects a 4D body through its existing 4D-to-3D projection and final 3D screen projection.
/// Physics positions remain untouched; this class only compares rendered screen positions.
/// </summary>
public static class NBodyScreenPicker
{
    private const float MinimumHitRadiusPixels = 6.0f;
    private const float HitPaddingPixels = 3.0f;
    private const float ComparisonEpsilon = 0.001f;

    public static PhysicsBody4D? Pick(
        Point mousePosition,
        Viewport viewport,
        Wireframe3D projectedBodies,
        IReadOnlyList<PhysicsBody4D> bodies,
        OrbitCamera3D camera,
        double pointScale)
    {
        if (!viewport.Bounds.Contains(mousePosition) ||
            projectedBodies.Vertices.Count == 0 ||
            bodies.Count == 0)
        {
            return null;
        }

        var view = camera.View;
        var projection = camera.CreateProjection(viewport.AspectRatio);
        var inverseView = Matrix.Invert(view);
        var cameraRight = Vector3.Normalize(new Vector3(
            inverseView.M11,
            inverseView.M12,
            inverseView.M13));
        var candidateCount = Math.Min(projectedBodies.Vertices.Count, bodies.Count);

        PhysicsBody4D? bestBody = null;
        var bestDistanceSquared = float.PositiveInfinity;
        var bestScreenDepth = float.PositiveInfinity;
        var bestCameraDepth4D = double.PositiveInfinity;

        for (var index = 0; index < candidateCount; index++)
        {
            var projected = projectedBodies.Vertices[index];
            var body = bodies[index];
            if (!body.IsAlive || !projected.IsVisible ||
                !TryToVector3(projected.Position, out var center))
            {
                continue;
            }

            var screenCenter = viewport.Project(center, projection, view, Matrix.Identity);
            if (!IsOnVisibleDepth(screenCenter))
            {
                continue;
            }

            var markerRadius = WireframeRenderer3D.NBodyMarkerRadius(body, pointScale);
            var screenEdge = viewport.Project(
                center + (cameraRight * markerRadius),
                projection,
                view,
                Matrix.Identity);
            var renderedRadius = Vector2.Distance(
                new Vector2(screenCenter.X, screenCenter.Y),
                new Vector2(screenEdge.X, screenEdge.Y));
            var hitRadius = Math.Max(MinimumHitRadiusPixels, renderedRadius + HitPaddingPixels);
            var deltaX = screenCenter.X - mousePosition.X;
            var deltaY = screenCenter.Y - mousePosition.Y;
            var distanceSquared = (deltaX * deltaX) + (deltaY * deltaY);
            if (distanceSquared > hitRadius * hitRadius)
            {
                continue;
            }

            if (!IsBetterCandidate(
                    body,
                    distanceSquared,
                    screenCenter.Z,
                    projected.CameraDepth4D,
                    bestBody,
                    bestDistanceSquared,
                    bestScreenDepth,
                    bestCameraDepth4D))
            {
                continue;
            }

            bestBody = body;
            bestDistanceSquared = distanceSquared;
            bestScreenDepth = screenCenter.Z;
            bestCameraDepth4D = projected.CameraDepth4D;
        }

        return bestBody;
    }

    private static bool IsBetterCandidate(
        PhysicsBody4D candidate,
        float distanceSquared,
        float screenDepth,
        double cameraDepth4D,
        PhysicsBody4D? current,
        float currentDistanceSquared,
        float currentScreenDepth,
        double currentCameraDepth4D)
    {
        if (current is null || distanceSquared < currentDistanceSquared - ComparisonEpsilon)
        {
            return true;
        }

        if (Math.Abs(distanceSquared - currentDistanceSquared) > ComparisonEpsilon)
        {
            return false;
        }

        if (screenDepth < currentScreenDepth - ComparisonEpsilon)
        {
            return true;
        }

        if (Math.Abs(screenDepth - currentScreenDepth) > ComparisonEpsilon)
        {
            return false;
        }

        if (cameraDepth4D < currentCameraDepth4D - ComparisonEpsilon)
        {
            return true;
        }

        return Math.Abs(cameraDepth4D - currentCameraDepth4D) <= ComparisonEpsilon &&
            candidate.Id < current.Id;
    }

    private static bool IsOnVisibleDepth(Vector3 screenPoint) =>
        float.IsFinite(screenPoint.X) &&
        float.IsFinite(screenPoint.Y) &&
        float.IsFinite(screenPoint.Z) &&
        screenPoint.Z is >= 0.0f and <= 1.0f;

    private static bool TryToVector3(HyperSpace.Mathematics.Vector3D source, out Vector3 result)
    {
        if (!source.IsFinite ||
            source.X < float.MinValue || source.X > float.MaxValue ||
            source.Y < float.MinValue || source.Y > float.MaxValue ||
            source.Z < float.MinValue || source.Z > float.MaxValue)
        {
            result = Vector3.Zero;
            return false;
        }

        result = new Vector3((float)source.X, (float)source.Y, (float)source.Z);
        return true;
    }
}
