using System;
using System.Diagnostics;
using System.IO;
using HyperSpace.Mathematics;
using HyperSpace.Physics;
using HyperSpace.Projection;
using HyperSpace.Rendering;
using HyperSpace.Transformations;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

/// <summary>Opt-in real DesktopGL smoke/throughput test; never part of normal startup.</summary>
internal sealed class RenderingBenchmarkGame : Game
{
    private const int WarmupFrames = 20;
    private const int SampleFrames = 120;
    private const int Width = 960;
    private const int Height = 720;
    private static readonly Color Background = new(8, 11, 22);
    private readonly PhysicsWorld4D _world = new();
    private readonly NBodyLab4D _lab;
    private readonly Camera4D _camera4D = new();
    private readonly PerspectiveProjector4D _projector = new();
    private readonly OrbitCamera3D _camera3D = new();
    private readonly WireframeProjectionPipeline4D _pipeline = new();
    private readonly Transform4D _transform = new();
    private Vector4D[] _positions = [];
    private WireframeRenderer3D? _cpuRenderer;
    private NBodyGpuRenderer? _gpuRenderer;
    private bool _smokeDone;
    private int _mode;
    private int _frame;
    private long _measuredStartedAt;
    private double _physicsMs;
    private double _projectionMs;
    private double _renderMs;

    public RenderingBenchmarkGame()
    {
        _ = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = Width,
            PreferredBackBufferHeight = Height,
            GraphicsProfile = GraphicsProfile.HiDef,
            SynchronizeWithVerticalRetrace = false
        };
        IsFixedTimeStep = false;
        Content.RootDirectory = Path.Combine(AppContext.BaseDirectory, "Content");
        Window.Title = "HyperSpace - GPU validation / 20k benchmark";
        _lab = new NBodyLab4D(_world);
        _lab.Settings.TryApplyBodyCount("20000", out _);
        _lab.SetGravityMode(GravityMode4D.MeanFieldApproximate);
        if (!_lab.GenerateSystem()) throw new InvalidOperationException(_lab.LastGenerationMessage);
        _camera4D.MoveWorld(new Vector4D(0, 0, 0, -14));
        _camera4D.Rotate(RotationPlane4D.XW, 0.24);
        _camera4D.Rotate(RotationPlane4D.YW, -0.16);
        _camera4D.Rotate(RotationPlane4D.ZW, 0.11);
        _camera4D.Rotate(RotationPlane4D.XY, 0.12);
        _camera4D.Rotate(RotationPlane4D.XZ, -0.09);
        _camera4D.Rotate(RotationPlane4D.YZ, 0.05);
    }

    protected override void LoadContent()
    {
        _cpuRenderer = new WireframeRenderer3D(GraphicsDevice);
        var particleEffect = Content.Load<Effect>("NBodyParticles");
        _gpuRenderer = new NBodyGpuRenderer(GraphicsDevice, particleEffect);
        Console.WriteLine($"GPU adapter={GraphicsAdapter.DefaultAdapter.Description} profile={GraphicsDevice.GraphicsProfile}");
    }

    protected override void Update(GameTime gameTime)
    {
        if (!_smokeDone) return;
        if (_frame == WarmupFrames) _measuredStartedAt = Stopwatch.GetTimestamp();
        var startedAt = Stopwatch.GetTimestamp();
        _world.StepOnce();
        if (_frame >= WarmupFrames) _physicsMs += Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
    }

    protected override void Draw(GameTime gameTime)
    {
        if (!_smokeDone)
        {
            RunVisualComparison();
            _smokeDone = true;
            return;
        }

        GraphicsDevice.Clear(Background);
        var startedAt = Stopwatch.GetTimestamp();
        var projectionMs = DrawParticles(_mode == 1, NBodyColorMode4D.WDepth);
        if (_frame >= WarmupFrames)
        {
            _projectionMs += projectionMs;
            _renderMs += Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds - projectionMs;
        }
        _frame++;
        if (_frame < WarmupFrames + SampleFrames) return;

        var frameMs = Stopwatch.GetElapsedTime(_measuredStartedAt).TotalMilliseconds / SampleFrames;
        Console.WriteLine($"RENDER mode={(_mode == 0 ? "CPU_REFERENCE" : "GPU_INSTANCED")} bodies={_world.Bodies.Count} physics={_physicsMs / SampleFrames:F3} projectionCpu={_projectionMs / SampleFrames:F3} renderCpu={_renderMs / SampleFrames:F3} frameWall={frameMs:F3} ms");
        if (_mode == 1)
        {
            Exit();
            return;
        }
        _mode = 1;
        _frame = 0;
        _physicsMs = _projectionMs = _renderMs = 0;
        _lab.ResetSystem();
    }

    private double DrawParticles(bool gpu, NBodyColorMode4D colorMode)
    {
        if (gpu)
        {
            _gpuRenderer!.Draw(GraphicsDevice, _world.Bodies, _world.SelectedBody,
                _camera4D, _projector, _camera3D, colorMode, 1);
            return 0;
        }

        var startedAt = Stopwatch.GetTimestamp();
        if (_positions.Length != _world.Bodies.Count) _positions = new Vector4D[_world.Bodies.Count];
        for (var index = 0; index < _positions.Length; index++) _positions[index] = _world.Bodies[index].Position;
        var projected = _pipeline.Project(_positions, Array.Empty<HyperSpace.Geometry.Edge>(), _transform, _camera4D, _projector);
        var projectionMs = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
        _cpuRenderer!.DrawPhysicsParticles(GraphicsDevice, projected, _world.Bodies,
            _world.SelectedBody, null, null, _camera3D, true, colorMode, 1);
        return projectionMs;
    }

    private void RunVisualComparison()
    {
        using var target = new RenderTarget2D(GraphicsDevice, Width, Height, false,
            SurfaceFormat.Color, DepthFormat.Depth24);
        foreach (var colorMode in Enum.GetValues<NBodyColorMode4D>())
        {
            var reference = Capture(false, colorMode);
            var actual = Capture(true, colorMode);
            var visible = 0;
            var unmatched = 0;
            for (var index = 0; index < actual.Length; index++)
            {
                if (actual[index] == Background) continue;
                visible++;
                var x = index % Width;
                var y = index / Width;
                var matched = false;
                for (var dy = -1; dy <= 1 && !matched; dy++)
                    for (var dx = -1; dx <= 1 && !matched; dx++)
                    {
                        var rx = x + dx;
                        var ry = y + dy;
                        if (rx < 0 || rx >= Width || ry < 0 || ry >= Height) continue;
                        var expected = reference[ry * Width + rx];
                        matched = Math.Abs(expected.R - actual[index].R) <= 3 &&
                            Math.Abs(expected.G - actual[index].G) <= 3 && Math.Abs(expected.B - actual[index].B) <= 3;
                    }
                if (!matched) unmatched++;
            }
            Console.WriteLine($"VISUAL mode={colorMode} pixels={visible} unmatchedWithin1px={unmatched} ({100.0 * unmatched / Math.Max(1, visible):F3}%)");
            if (visible < 1000 || unmatched > visible * 0.03)
                throw new InvalidOperationException("GPU particles do not match the CPU reference rendering.");
        }
        using var screenshot = File.Create(Path.Combine(AppContext.BaseDirectory, "gpu-smoke.png"));
        target.SaveAsPng(screenshot, Width, Height);

        // Near-plane rejection must also remain safe in the actual GPU path.
        _camera4D.MoveWorld(new Vector4D(0, 0, 0, 18));
        _ = Capture(true, NBodyColorMode4D.WDepth);
        _camera4D.MoveWorld(new Vector4D(0, 0, 0, -18));

        Color[] Capture(bool gpu, NBodyColorMode4D mode)
        {
            GraphicsDevice.SetRenderTarget(target);
            GraphicsDevice.Clear(Background);
            DrawParticles(gpu, mode);
            GraphicsDevice.SetRenderTarget(null);
            var pixels = new Color[Width * Height];
            target.GetData(pixels); // Verification only; the live renderer never reads back.
            return pixels;
        }
    }

    protected override void UnloadContent()
    {
        _gpuRenderer?.Dispose();
        _cpuRenderer?.Dispose();
        _lab.Dispose();
        base.UnloadContent();
    }
}
