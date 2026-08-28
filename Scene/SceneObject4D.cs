using System;
using HyperSpace.Geometry;
using HyperSpace.Rendering;
using HyperSpace.Transformations;

namespace HyperSpace.Scene;

/// <summary>
/// A geometry instance with independent 4D transform and visualization state.
/// </summary>
public sealed class SceneObject4D
{
    public SceneObject4D(IGeometry4D geometry, DisplayOptions? displayOptions = null)
    {
        Geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
        DisplayOptions = displayOptions ?? new DisplayOptions();
    }

    public IGeometry4D Geometry { get; private set; }

    public Transform4D Transform { get; } = new();

    public DisplayOptions DisplayOptions { get; }

    public void ReplaceGeometry(IGeometry4D geometry)
    {
        Geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
    }
}
