# HyperSpace

HyperSpace is a small MonoGame desktop sandbox for understanding and visualizing
four-dimensional Euclidean space. It is not a game at this stage.

The current milestone renders one algorithmically generated tesseract, its eight
real cubical boundary cells, and a small 4D spatial reference grid through a
real 4D perspective pipeline. A minimal side panel starts observable, one-second
4D transform animations and independently controls the visual layers:

```text
UI or direct keyboard/mouse input
-> TransformationAnimator4D update (for panel requests)
-> Tesseract4D or ReferenceGrid4D vertices
-> Transform4D uniform 4D scale, rotation, and translation
-> Camera4D view transformation
-> 4D perspective projection
-> Wireframe3D
-> translucent cell faces + axis-coded edges + W-coded vertex markers
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
| Move the tesseract in X/Y | Hold right mouse button and drag |
| Move the tesseract in Z/W | Hold middle mouse button and drag |
| Zoom the post-projection 3D view | Mouse wheel |
| Change 4D focal distance | `[` / `]` |
| Reset all transforms and cameras | `Space` |
| Exit | `Escape` |

The right-side panel adds these animated object controls while preserving every
direct control above:

| Panel action | Result over 1 second |
| --- | --- |
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
| `SHOW CELLS` | Toggles the eight translucent cubical cell shells |
| `SHOW EDGES` | Toggles all 32 direction-coded tesseract edges |
| `SHOW VERTICES` | Toggles all 16 W-coded vertex markers |

Only one panel animation runs at a time. Transform buttons are disabled until
it finishes; reset buttons remain available and cancel it immediately. A later
click starts a new additive step, so three completed `XW +90` clicks accumulate
to +270 degrees. Keyboard controls remain live during an animation. Scene mouse
gestures are suppressed only while the pointer is over the panel, preventing a
UI click from also orbiting or moving the tesseract.

Mouse movement is relative. Dragging right increases object X or Z; dragging up
increases object Y or W, depending on the held mouse button.

Tesseract edge hue represents the mathematical direction that changes: X, Y, Z,
or W. Brightness still carries the previous camera-space W depth cue: nearer
endpoints are brighter. Vertex hue represents the stable source layer `W-` or
`W+`. These are visual annotations; geometry still comes from 4D projection.

## Visual layers

The scene is deliberately rendered in this order:

```text
background
-> 4D grid and axes
-> translucent cell faces
-> 32 opaque wireframe edges
-> 16 small octahedral vertex markers
-> object/camera debug panel
```

`VisualizationPalette.CellSurfaceAlpha` is the single cell-opacity setting and
defaults to `0.18`. The Display panel legend reads from the exact same palette
as the scene renderer, so its cell, edge, and vertex colors cannot drift from
the actual visualization.

## Project structure

```text
Mathematics/       renderer-independent Vector3D and Vector4D values
Geometry/          tesseract cells/faces/edges, ReferenceGrid4D, and Wireframe3D
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
- `ReferenceGrid4D` contains X/Y/Z coordinate crosses in five W layers and six
  W-parallel rails at non-zero X/Y/Z offsets. The central W axis is not drawn as
  a fake line: under this projection `(0,0,0,w)` correctly collapses to the 3D
  origin. Both the grid and tesseract use `WireframeProjectionPipeline4D`.
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
  normal 3D view/projection using `BasicEffect`. Cells reuse those same projected
  vertex indices; no second or approximate projection path exists.

## Animation model

`TransformationAnimator4D` receives one request and advances it from elapsed
time in `Update`; `Draw` only visualizes the resulting state. Smooth-step easing
makes the start and end observable without blocking the game loop. Rotation and
translation are applied as incremental deltas, so direct keyboard/mouse changes
made during the animation are not overwritten. Uniform scale uses the same
incremental approach with exponential interpolation, making factors 1.25 and
0.8 reciprocal paths.

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
  hue and camera-space W depth sets brightness, not thickness.
- Each square boundary face belongs to two differently colored cells. Both are
  rendered, so their translucent colors intentionally mix on the shared face.
- Transparent triangles use `NonPremultiplied` alpha blending, no depth writes,
  and back-to-front centroid sorting. This is a good approximation for 96 tiny
  triangles, but intersecting/shared transparent faces have no globally perfect
  draw order without order-independent transparency or mesh splitting.
- Vertex markers are small 3D octahedra drawn last without depth testing. This
  makes all sixteen topological vertices readable, but they behave as structural
  annotations rather than physically occluded solid spheres.
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
- The side-panel layout targets the default 1280x720 window. Resizing below
  roughly 720 pixels high can clip the lower legend; no scroll layout exists.
- There is no slicing, physics, collision, gameplay, opaque solid geometry, or
  general-purpose UI framework.

Future slicing should be implemented as a separate visualization strategy. It
must not replace the perspective projection pipeline used by this milestone.
