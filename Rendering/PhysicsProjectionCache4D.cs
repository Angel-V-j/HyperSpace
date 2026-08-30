using System;
using System.Collections.Generic;
using HyperSpace.Geometry;
using HyperSpace.Mathematics;
using HyperSpace.Physics;
using HyperSpace.Projection;
using HyperSpace.Transformations;

namespace HyperSpace.Rendering;

/// <summary>
/// Owns the transient 3D projections used by the physics visualizations.
/// Physics state and rendering remain separate; this class only prepares wireframes.
/// </summary>
internal sealed class PhysicsProjectionCache4D
{
    private readonly WireframeProjectionPipeline4D _pipeline;
    private readonly Camera4D _camera;
    private readonly PerspectiveProjector4D _projector;
    private readonly HyperplaneGrid4D _hyperplaneGrid = new();
    private readonly Transform4D _transform = new();
    private Vector4D[] _particlePositions = [];
    private readonly List<Edge> _gravityTrailEdges = [];
    private readonly List<Edge> _nBodyTrailEdges = [];
    private readonly List<Vector4D> _gravityFieldPoints = [];
    private readonly Edge[] _gravityFieldEdges = [new Edge(0, 1, EdgeKind.Grid)];

    public PhysicsProjectionCache4D(
        WireframeProjectionPipeline4D pipeline,
        Camera4D camera,
        PerspectiveProjector4D projector)
    {
        _pipeline = pipeline;
        _camera = camera;
        _projector = projector;

        Particles = ProjectEmpty();
        Hyperplane = _pipeline.Project(
            _hyperplaneGrid.Vertices,
            _hyperplaneGrid.Edges,
            _transform,
            _camera,
            _projector);
        GravityTrail = ProjectEmpty();
        NBodyTrail = ProjectEmpty();
        GravityField = ProjectEmpty();
    }

    public Wireframe3D Particles { get; private set; }
    public Wireframe3D Hyperplane { get; private set; }
    public Wireframe3D GravityTrail { get; private set; }
    public Wireframe3D NBodyTrail { get; private set; }
    public Wireframe3D GravityField { get; private set; }

    public void Update(
        PhysicsWorld4D world,
        GravityLab4D gravityLab,
        NBodyLab4D nBodyLab,
        bool projectParticles = true)
    {
        if (projectParticles)
        {
            UpdateParticles(world);
        }
        UpdateReferenceProjections(gravityLab, nBodyLab);
    }

    public Wireframe3D UpdateParticles(PhysicsWorld4D world)
    {
        if (_particlePositions.Length != world.Bodies.Count)
        {
            _particlePositions = new Vector4D[world.Bodies.Count];
        }

        HyperSpace.Diagnostics.ParallelWork.ForRanges(
            world.Bodies.Count,
            minimumItemsPerWorker: 2_048,
            (_, start, end) =>
            {
                for (var index = start; index < end; index++)
                {
                    _particlePositions[index] = world.Bodies[index].Position;
                }
            });

        Particles = _pipeline.Project(
            _particlePositions,
            Array.Empty<Edge>(),
            _transform,
            _camera,
            _projector);
        return Particles;
    }

    private void UpdateReferenceProjections(GravityLab4D gravityLab, NBodyLab4D nBodyLab)
    {
        Hyperplane = _pipeline.Project(
            _hyperplaneGrid.Vertices,
            _hyperplaneGrid.Edges,
            _transform,
            _camera,
            _projector);

        BuildSequentialEdges(gravityLab.Trail.Points.Count, _gravityTrailEdges);
        GravityTrail = _pipeline.Project(
            gravityLab.Trail.Points,
            _gravityTrailEdges,
            _transform,
            _camera,
            _projector);

        BuildSequentialEdges(nBodyLab.SelectedTrail.Points.Count, _nBodyTrailEdges);
        NBodyTrail = _pipeline.Project(
            nBodyLab.SelectedTrail.Points,
            _nBodyTrailEdges,
            _transform,
            _camera,
            _projector);

        _gravityFieldPoints.Clear();
        if (gravityLab.HasExperiment)
        {
            _gravityFieldPoints.Add(gravityLab.CentralBody!.Position);
            _gravityFieldPoints.Add(gravityLab.Orbiter!.Position);
        }

        GravityField = _pipeline.Project(
            _gravityFieldPoints,
            _gravityFieldPoints.Count == 2 ? _gravityFieldEdges : Array.Empty<Edge>(),
            _transform,
            _camera,
            _projector);
    }

    private Wireframe3D ProjectEmpty() => _pipeline.Project(
        Array.Empty<Vector4D>(),
        Array.Empty<Edge>(),
        _transform,
        _camera,
        _projector);

    private static void BuildSequentialEdges(int pointCount, List<Edge> edges)
    {
        edges.Clear();
        for (var index = 1; index < pointCount; index++)
        {
            edges.Add(new Edge(index - 1, index, EdgeKind.Grid));
        }
    }
}
