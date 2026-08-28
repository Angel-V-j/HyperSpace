namespace HyperSpace.Rendering;

/// <summary>
/// Independent visibility switches for the educational rendering layers.
/// </summary>
public sealed class DisplayOptions
{
    public DisplayOptions(
        bool showCells = true,
        bool showEdges = true,
        bool showVertices = true,
        bool showDirection = false)
    {
        ShowCells = showCells;
        ShowEdges = showEdges;
        ShowVertices = showVertices;
        ShowDirection = showDirection;
    }

    public bool ShowGrid { get; private set; } = true;

    public bool ShowAxes { get; private set; } = true;

    public bool ShowCells { get; private set; } = true;

    public bool ShowEdges { get; private set; } = true;

    public bool ShowVertices { get; private set; } = true;

    public bool ShowDirection { get; private set; }

    public void ToggleGrid() => ShowGrid = !ShowGrid;

    public void ToggleAxes() => ShowAxes = !ShowAxes;

    public void ToggleCells() => ShowCells = !ShowCells;

    public void ToggleEdges() => ShowEdges = !ShowEdges;

    public void ToggleVertices() => ShowVertices = !ShowVertices;

    public void ToggleDirection() => ShowDirection = !ShowDirection;
}
