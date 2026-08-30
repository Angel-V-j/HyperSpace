using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using HyperSpace.Diagnostics;
using HyperSpace.Physics;
using HyperSpace.Projection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace HyperSpace.Rendering;

/// <summary>
/// One 32-byte instance per body; the vertex shader performs camera rotation,
/// 4D perspective, color mapping and billboard expansion. No GPU readback.
/// </summary>
internal sealed class NBodyGpuRenderer : IDisposable
{
    private readonly Effect _effect;
    private readonly VertexBuffer _quad;
    private readonly IndexBuffer _indices;
    private readonly VertexBufferBinding[] _bindings = new VertexBufferBinding[2];
    private DynamicVertexBuffer? _instances;
    private ParticleInstance[] _staging = [];
    private Vector4[] _workerMaxima = [];

    public NBodyGpuRenderer(GraphicsDevice graphicsDevice, Effect effect)
    {
        _effect = effect;
        _quad = new VertexBuffer(graphicsDevice, QuadVertex.Declaration, 4, BufferUsage.WriteOnly);
        _quad.SetData(new QuadVertex[]
        {
            new(new Vector2(-1, 1)), new(new Vector2(-1, -1)),
            new(new Vector2(1, 1)), new(new Vector2(1, -1))
        });
        _indices = new IndexBuffer(graphicsDevice, IndexElementSize.SixteenBits, 6, BufferUsage.WriteOnly);
        _indices.SetData(new short[] { 0, 1, 2, 2, 1, 3 });
        _bindings[0] = new VertexBufferBinding(_quad);
    }

    public void Draw(
        GraphicsDevice graphicsDevice,
        IReadOnlyList<PhysicsBody4D> bodies,
        PhysicsBody4D? selectedBody,
        Camera4D camera4D,
        PerspectiveProjector4D projector,
        OrbitCamera3D camera3D,
        NBodyColorMode4D colorMode,
        double pointScale)
    {
        if (bodies.Count == 0)
        {
            return;
        }

        EnsureCapacity(graphicsDevice, bodies.Count);
        var workerCount = ParallelWork.WorkerCountFor(bodies.Count, 2_048);
        if (_workerMaxima.Length < workerCount)
        {
            Array.Resize(ref _workerMaxima, workerCount);
        }
        var cameraPosition = camera4D.Position;
        var selectedId = selectedBody?.Id ?? -1;
        ParallelWork.ForRanges(bodies.Count, 2_048, (workerIndex, start, end) =>
        {
            var maximum = Vector4.Zero;
            for (var index = start; index < end; index++)
            {
                var body = bodies[index];
                var relative = body.Position - cameraPosition;
                var position = new Vector4((float)relative.X, (float)relative.Y,
                    (float)relative.Z, (float)relative.W);
                if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) ||
                    !float.IsFinite(position.Z) || !float.IsFinite(position.W) ||
                    Math.Abs(position.X) > 1e20f || Math.Abs(position.Y) > 1e20f ||
                    Math.Abs(position.Z) > 1e20f || Math.Abs(position.W) > 1e20f)
                {
                    position = new Vector4(0, 0, 0, -1);
                }
                var mass = (float)body.Mass;
                var speed = (float)body.Velocity.Length;
                var acceleration = (float)body.Acceleration.Length;
                var worldW = (float)body.Position.W;
                _staging[index] = new ParticleInstance(position, new Vector4(
                    (float)body.Radius * (body.Id == selectedId ? -1 : 1),
                    mass,
                    speed,
                    acceleration));
                maximum = Vector4.Max(
                    maximum,
                    new Vector4(mass, speed, acceleration, Math.Abs(worldW)));
            }
            _workerMaxima[workerIndex] = maximum;
        });
        var maxima = Vector4.Zero;
        for (var workerIndex = 0; workerIndex < workerCount; workerIndex++)
        {
            maxima = Vector4.Max(maxima, _workerMaxima[workerIndex]);
        }

        _instances!.SetData(_staging, 0, bodies.Count, SetDataOptions.Discard);
        SetParameters(graphicsDevice, camera4D, projector, camera3D, colorMode, pointScale, maxima);
        graphicsDevice.BlendState = BlendState.Opaque;
        graphicsDevice.DepthStencilState = DepthStencilState.Default;
        graphicsDevice.RasterizerState = RasterizerState.CullNone;
        graphicsDevice.SetVertexBuffers(_bindings);
        graphicsDevice.Indices = _indices;
        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            graphicsDevice.DrawInstancedPrimitives(PrimitiveType.TriangleList, 0, 0, 2, bodies.Count);
        }
        graphicsDevice.SetVertexBuffer(null);
    }

    private void SetParameters(
        GraphicsDevice graphicsDevice,
        Camera4D camera4D,
        PerspectiveProjector4D projector,
        OrbitCamera3D camera3D,
        NBodyColorMode4D colorMode,
        double pointScale,
        Vector4 maxima)
    {
        var view = camera3D.View;
        var inverseView = Matrix.Invert(view);
        _effect.Parameters["ViewProjection"].SetValue(view * camera3D.CreateProjection(graphicsDevice.Viewport.AspectRatio));
        _effect.Parameters["BillboardRight"].SetValue(Vector3.Normalize(new Vector3(inverseView.M11, inverseView.M12, inverseView.M13)));
        _effect.Parameters["BillboardUp"].SetValue(Vector3.Normalize(new Vector3(inverseView.M21, inverseView.M22, inverseView.M23)));
        var rotation = camera4D.Orientation;
        _effect.Parameters["RotationCosA"].SetValue(new Vector4((float)Math.Cos(rotation.XY), (float)Math.Cos(rotation.XZ), (float)Math.Cos(rotation.XW), (float)Math.Cos(rotation.YZ)));
        _effect.Parameters["RotationSinA"].SetValue(new Vector4((float)Math.Sin(rotation.XY), (float)Math.Sin(rotation.XZ), (float)Math.Sin(rotation.XW), (float)Math.Sin(rotation.YZ)));
        _effect.Parameters["RotationCosB"].SetValue(new Vector2((float)Math.Cos(rotation.YW), (float)Math.Cos(rotation.ZW)));
        _effect.Parameters["RotationSinB"].SetValue(new Vector2((float)Math.Sin(rotation.YW), (float)Math.Sin(rotation.ZW)));
        _effect.Parameters["Perspective4D"].SetValue(new Vector2((float)projector.FocalDistance, (float)projector.NearPlane));
        _effect.Parameters["PointScale"].SetValue((float)pointScale);
        // HLSL ternaries may be flattened, so every divisor must be nonzero even
        // when its color mode is not selected (e.g. an entirely stationary cloud).
        _effect.Parameters["Maxima"].SetValue(Vector4.Max(maxima, new Vector4(1e-12f)));
        _effect.Parameters["CameraWorldW"].SetValue((float)camera4D.Position.W);
        _effect.Parameters["ColorMode"].SetValue((float)colorMode);
        var (low, high) = colorMode switch
        {
            NBodyColorMode4D.Acceleration => (new Color(70,100,180), new Color(200, 95, 215)),
            NBodyColorMode4D.Mass => (new Color(69, 132, 214), new Color(255, 210, 74)),
            NBodyColorMode4D.Speed => (new Color(86, 215, 167), new Color(255, 91, 119)),
            _ => (VisualizationPalette.PhysicsParticleNegativeW, VisualizationPalette.PhysicsParticlePositiveW)
        };
        _effect.Parameters["ColorLow"].SetValue(low.ToVector4());
        _effect.Parameters["ColorHigh"].SetValue(high.ToVector4());
        _effect.Parameters["SelectedColor"].SetValue(VisualizationPalette.PhysicsParticleSelected.ToVector4());
    }

    private void EnsureCapacity(GraphicsDevice graphicsDevice, int count)
    {
        if (_staging.Length >= count)
        {
            return;
        }

        var capacity = Math.Max(count, Math.Max(512, _staging.Length * 2));
        Array.Resize(ref _staging, capacity);
        _instances?.Dispose();
        _instances = new DynamicVertexBuffer(graphicsDevice, ParticleInstance.Declaration, capacity, BufferUsage.WriteOnly);
        _bindings[1] = new VertexBufferBinding(_instances, 0, 1);
    }

    public void Dispose()
    {
        _instances?.Dispose();
        _indices.Dispose();
        _quad.Dispose();
        // ContentManager owns the effect.
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct QuadVertex(Vector2 corner) : IVertexType
    {
        public static readonly VertexDeclaration Declaration = new(
            new VertexElement(0, VertexElementFormat.Vector2, VertexElementUsage.Position, 0));
        public readonly Vector2 Corner = corner;
        public VertexDeclaration VertexDeclaration => Declaration;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct ParticleInstance(Vector4 relativePosition, Vector4 data) : IVertexType
    {
        public static readonly VertexDeclaration Declaration = new(
            new VertexElement(0, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 0),
            new VertexElement(16, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 1));
        public readonly Vector4 RelativePosition = relativePosition;
        public readonly Vector4 Data = data;
        public VertexDeclaration VertexDeclaration => Declaration;
    }
}
