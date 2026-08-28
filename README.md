# HyperSpace

HyperSpace is a small MonoGame desktop sandbox for understanding and visualizing
four-dimensional Euclidean space. It is not a game at this stage.

The current milestone renders one selected 4D object plus a small 4D spatial
reference grid through a real 4D perspective pipeline. The selectable objects
are an exact tesseract, a sampled hypersphere S3, a regular 4-simplex, a
deterministic irregular 16-cell, and a procedural 4D spiral curve. A minimal side panel switches objects, starts
observable one-second 4D transform animations, and controls the visual layers:

```text
UI or direct keyboard/mouse input
-> TransformationAnimator4D update (for panel requests)
-> selected IGeometry4D or ReferenceGrid4D vertices
-> Transform4D uniform 4D scale, rotation, and translation
-> Camera4D view transformation
-> 4D perspective projection
-> Wireframe3D
-> translucent faces/cells or a W-coded 3D polyline
-> MonoGame 3D view and perspective projection
-> screen
```

## Controls

All rotations are continuous while a key is held.

| Action | Controls |
| --- | --- |
| Move Camera4D in +X / -X | `Q` / `A` |
| Move Camera4D in +Y / -Y | `W` / `S` |
| Move Camera4D in +Z / -Z | `E` / `D` |
| Move Camera4D in +W / -W | `R` / `F` |
| Rotate object in XY | `T` / `Y` |
| Rotate object in XZ | `U` / `I` |
| Rotate object in XW | `O` / `P` |
| Rotate object in YZ | `G` / `H` |
| Rotate object in YW | `J` / `K` |
| Rotate object in ZW | `L` / `;` |
| Rotate Camera4D | Hold `Shift` with any rotation pair |
| Orbit the post-projection 3D view | Hold left mouse button and drag |
| Move the selected object in X/Y | Hold right mouse button and drag |
| Move the selected object in Z/W | Hold middle mouse button and drag |
| Zoom the post-projection 3D view | Mouse wheel |
| Change 4D focal distance | `[` / `]` |
| Reset all transforms and cameras | `Space` |
| Exit | `Escape` |

The right-side panel adds these animated object controls while preserving every
direct control above:

| Panel action | Result over 1 second |
| --- | --- |
| `TESSERACT`, `HYPERSPHERE`, `4-SIMPLEX`, `IRREGULAR`, `4D SPIRAL` | Selects the only rendered object; Camera4D, 3D view, and projection remain unchanged |
| `XY`, `XZ`, `XW`, `YZ`, `YW`, `ZW +90` | Adds a positive 90-degree rotation in that exact 4D coordinate plane |
| `SCALE +` | Multiplies uniform XYZW scale by 1.25 |
| `SCALE -` | Multiplies uniform XYZW scale by 0.8 |
| `+ X` / `- X` | Moves the object by +/-0.75 on world X |
| `+ Y` / `- Y` | Moves the object by +/-0.75 on world Y |
| `+ Z` / `- Z` | Moves the object by +/-0.75 on world Z |
| `+ W` / `- W` | Moves the object by +/-0.75 on world W |
| `RESET OBJECT` | Cancels animation; restores object position, rotation, and scale |
| `RESET CAMERA` | Cancels animation; restores Camera4D, 3D orbit view, and focal distance |
| `SHOW GRID` | Toggles the minor 4D reference-grid lines |
| `SHOW AXES` | Toggles the X/Y/Z axes and offset W rails |
| `SHOW SURFACE` | Toggles translucent sampled faces or boundary-cell faces |
| `SHOW EDGES` | Toggles all topology/parameter-mesh edges |
| `SHOW VERTICES` | Toggles all sampled/topological vertex markers |

When `4D SPIRAL` is selected, the Object panel adds curve-specific controls:

| Curve action | Result |
| --- | --- |
| `r1 -/+`, `r2 -/+` | Changes the pending XY or ZW radius by 0.10 within `[0.10, 3.00]` |
| `k -/+` | Changes the pending ZW angular frequency by 0.25 within `[0.25, 6.00]` |
| `Samples -/+` | Changes the pending sample count by 100 within `[100, 1200]` |
| `REGENERATE` | Replaces only the spiral geometry; object transform and both cameras are preserved |
| `PLAY CURVE` | Restarts at P0 when complete, then reveals the polyline over four seconds |
| `RESET CURVE` | Stops playback and leaves only P0 visible |
| `SHOW CURVE` | Toggles the projected polyline; default ON |
| `SHOW POINTS` | Toggles the numerical samples; default OFF |
| `SHOW DIRECTION` | Toggles the green START octahedron and yellow END/current-tip cube; default ON |

Only one panel animation runs at a time. Transform buttons are disabled until
it finishes; reset buttons remain available and cancel it immediately. A later
click starts a new additive step, so three completed `XW +90` clicks accumulate
to +270 degrees. Keyboard controls remain live during an animation. Scene mouse
gestures are suppressed only while the pointer is over the panel, preventing a
UI click from also orbiting or moving the selected object. Each selectable
object retains its own `Transform4D` and display toggles. Switching cancels an
active object animation, but deliberately does not reset either camera or the
4D projection.

Mouse movement is relative. Dragging right increases object X or Z; dragging up
increases object Y or W, depending on the held mouse button.

Tesseract edge hue represents the mathematical direction that changes: X, Y, Z,
or W. The other objects use their own W-gradient palettes. Edge brightness also
carries camera-space W depth, while face and vertex color uses local source W.
These are visual annotations; geometry still comes from 4D projection.
For the spiral specifically, color uses transformed world-space W: W=0 is the
middle of the cyan-to-pink gradient, so rotations and W translations change the
color from the actual current fourth coordinate.

## Visual layers

The scene is deliberately rendered in this order:

```text
background
-> 4D grid and axes
-> translucent surface or cell faces
-> opaque topology/parameter edges
-> small octahedral sampled/topological vertex markers
-> spiral START/END direction markers
-> object/camera debug panel
```

`VisualizationPalette.CellSurfaceAlpha` is the single surface-opacity setting and
defaults to `0.18`. The Display panel legend reads from the exact same palette
as the scene renderer, so its cell, edge, and vertex colors cannot drift from
the actual visualization.

## Project structure

```text
Mathematics/       renderer-independent Vector3D and Vector4D values
Geometry/          common topology, polytopes, S3, spiral parameters/generator, grid
Scene/             geometry instance state plus curve-prefix playback state
Transformations/   six-plane rotation, object transform, and time-based animator
Projection/        Camera4D, perspective projection, and pipeline orchestration
Rendering/         visual layers, shared palette/options, orbit camera, debug overlay
Input/             direct keyboard and mouse control mapping
UI/                minimal side panel, button state, and transformation commands
Checks/            zero-dependency mathematical and input smoke checks
Content/           the small debug overlay font definition
SandboxGame.cs     application update/draw loop
```

## Mathematical conventions

- Tesseract vertex coordinates are every combination of `-1` and `+1` in
  `(x, y, z, w)`. Two vertices share an edge exactly when their indices differ
  in one coordinate bit. This generates 16 vertices and 32 edges.
- The eight cubical boundary cells are generated by fixing one coordinate and
  sign: `X-`, `X+`, `Y-`, `Y+`, `Z-`, `Z+`, `W-`, and `W+`. Each cell contains
  eight existing tesseract vertices and six algorithmically generated square
  faces. Every vertex belongs to four cells; every one of the 24 unique square
  faces is shared by exactly two cells.
- `Hypersphere4D` samples the 3-sphere with
  `x=r sin(chi) sin(theta) cos(phi)`,
  `y=r sin(chi) sin(theta) sin(phi)`, `z=r sin(chi) cos(theta)`, and
  `w=r cos(chi)`. Thus every generated point satisfies
  `x^2+y^2+z^2+w^2=r^2`. The default configurable resolution is 4 chi, 4 theta,
  and 8 phi intervals: 80 vertices, 272 parameter-mesh edges, and 96 polygonal
  faces across three constant-chi 2-sphere shells.
- `Simplex4D` is a regular pentachoron centered at the origin. Its five vertices
  have equal radius and pairwise dot product `-r^2/4`; combinations generate all
  10 edges, 10 triangular faces, and 5 tetrahedral cells.
- `IrregularPolytope4D` is not a deformed hypercube. It is an asymmetric,
  centered realization of the 4D cross-polytope topology: eight signed-axis
  vertices receive unequal radii and a fixed invertible shear. Combinatorics,
  rather than a fragile runtime convex-hull guess, deterministically generates
  24 edges, 32 triangular faces, and 16 tetrahedral cells.
- `Spiral4DGenerator` samples
  `P(t)=(r1 cos(t), r1 sin(t), r2 cos(k t), r2 sin(k t))`. Every sample therefore
  obeys `x^2+y^2=r1^2` and `z^2+w^2=r2^2`. Consecutive samples alone are joined,
  producing an open polyline with `N` vertices and `N-1` edges. Defaults are
  `r1=1.0`, `r2=0.5`, `k=2.25`, `N=600`, and `t=0..4pi`. The non-integer default
  frequency deliberately keeps START and END distinct; `k=2` over this range
  would retrace a closed curve and place both direction markers together.
- `IGeometry4D` exposes only immutable vertices, edges, faces, cells, name/style,
  and sampling information. `SceneObject4D` composes that topology with a common
  transform and display state. There is no renderer class per shape.
- `ReferenceGrid4D` contains X/Y/Z coordinate crosses in five W layers and six
  W-parallel rails at non-zero X/Y/Z offsets. The central W axis is not drawn as
  a fake line: under this projection `(0,0,0,w)` correctly collapses to the 3D
  origin. Both the grid and every selected object use
  `WireframeProjectionPipeline4D`.
- A `Rotation4D` is the ordered composition `XY -> XZ -> XW -> YZ -> YW -> ZW`.
  Each component is a standard two-dimensional rotation in that coordinate
  plane. The order is explicit because 4D rotations generally do not commute.
- `Rotation4D` directly applies the corresponding 4x4 plane-rotation matrices:
  the selected two axes use the block `[cos -sin; sin cos]`, while the other two
  axes retain the identity. No 3D rotation, W translation, or projection change
  substitutes for a panel rotation.
- Uniform object scale is the 4D matrix `sI`: X, Y, Z and W are all multiplied
  before rotation and translation. It is bounded to `[0.05, 20]` so repeated
  exploratory clicks cannot collapse to zero or overflow.
- Camera4D's local `+W` axis points forward. Camera space is
  `inverseOrientation * (worldPoint - cameraPosition)`.
- The 4D perspective model and its safety behavior are documented in
  `Projection/README.md`.
- `Wireframe3D` is a real intermediate representation. Only after it is built
  are its vertices converted to MonoGame `Vector3` values and passed through a
  normal 3D view/projection using `BasicEffect`. Faces reuse those same projected
  vertex indices; no second or approximate projection path exists.

## Animation model

`TransformationAnimator4D` receives one request and advances it from elapsed
time in `Update`; `Draw` only visualizes the resulting state. Smooth-step easing
makes the start and end observable without blocking the game loop. Rotation and
translation are applied as incremental deltas, so direct keyboard/mouse changes
made during the animation are not overwritten. Uniform scale uses the same
incremental approach with exponential interpolation, making factors 1.25 and
0.8 reciprocal paths.

`CurvePlayback4D` is independent of object transformation animation. It never
mutates or regenerates geometry; it advances a visible sample count and the
renderer draws only the prefix `P0..Pn` from the already projected
`Wireframe3D`. Transform rotations, camera movement, and curve playback can
therefore remain active without separate projection logic.

## Build and run

```powershell
dotnet build HyperSpace.csproj
dotnet run --project HyperSpace.csproj
```

Run the deterministic checks with:

```powershell
dotnet run --project Checks\HyperSpace.MathChecks.csproj
```

## Deliberate limitations

- Edges crossing the 4D near hyperplane are not clipped yet. If either endpoint
  is invalid, the whole edge is skipped safely. This can cause edge popping when
  Camera4D passes through the object, but avoids division by zero and infinities.
- The 3D wireframe uses one-pixel MonoGame line primitives. Edge direction sets
  hue and camera-space W depth sets brightness, not thickness. The spiral is a
  smooth 600-segment approximation, but it is not rendered as a thick tube.
- Polytope boundary faces belong to two differently colored 3D cells. Both are
  rendered, so their translucent colors intentionally mix on a shared face.
- S3 is a three-dimensional manifold in 4D, not a two-dimensional boundary
  skin. The current translucent geometry shows sampled constant-chi 2-sphere
  shells and connects them with parameter edges. This is mathematically honest
  sampling, but not a volumetric tetrahedralization of the whole S3 manifold.
- Transparent triangles use `NonPremultiplied` alpha blending, no depth writes,
  and back-to-front centroid sorting. This is a useful small-mesh approximation,
  but intersecting/shared transparent faces have no globally perfect
  draw order without order-independent transparency or mesh splitting.
- Vertex markers are small 3D octahedra drawn last without depth testing. This
  makes all sampled/topological vertices readable, but they behave as structural
  annotations rather than physically occluded solid spheres. Spiral START and
  END markers follow the same annotation rule.
- The reference grid is intentionally a sparse coordinate frame rather than a
  full 4D lattice. A full lattice becomes visually dense very quickly.
- Rotation angles are an ordered six-plane composition, not a general 4D
  orientation parameterization. This is transparent and sufficient for the
  current experiment, but other representations can be explored later.
- Panel animation requests are not queued. A transform click made while another
  animation is active is intentionally ignored; this keeps the first UI model
  deterministic and makes the active operation unambiguous.
- Cumulative angles are deliberately not wrapped to `[-180, 180]` so the debug
  overlay can show educational sequences such as 90, 180, and 270 degrees.
  Extremely large accumulated angles may eventually lose floating-point
  precision, but that requires impractical sandbox use.
- Camera4D movement keys currently translate along world X/Y/Z/W axes rather
  than camera-local axes.
- The debug font currently uses the Windows `Segoe UI` system font during the
  content build.
- The side-panel layout targets the default 1280x900 window. Resizing below
  roughly 900 pixels high can clip the lower controls; no scroll layout exists.
- Spiral `tStart` and `tEnd` are configurable in `SpiralParameters`, but the
  current compact UI changes only r1, r2, k, and sample count. Parameter edits
  are intentionally pending until `REGENERATE` is pressed.
- There is no slicing, physics, collision, gameplay, opaque solid geometry, or
  general-purpose UI framework.

Future slicing should be implemented as a separate visualization strategy. It
must not replace the perspective projection pipeline used by this milestone.
