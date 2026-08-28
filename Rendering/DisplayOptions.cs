namespace HyperSpace.Rendering;

/// <summary>
/// Independent visibility switches for the educational rendering layers.
/// </summary>
public sealed class DisplayOptions
{
    public bool ShowGrid { get; private set; } = true;

    public bool ShowAxes { get; private set; } = true;

    public bool ShowCells { get; private set; } = true;

    public bool ShowEdges { get; private set; } = true;

    public bool ShowVertices { get; private set; } = true;

    public void ToggleGrid() => ShowGrid = !ShowGrid;

    public void ToggleAxes() => ShowAxes = !ShowAxes;

    public void ToggleCells() => ShowCells = !ShowCells;

    public void ToggleEdges() => ShowEdges = !ShowEdges;

    public void ToggleVertices() => ShowVertices = !ShowVertices;
}
