# HyperSpace

HyperSpace is a small MonoGame desktop sandbox for understanding and visualizing
four-dimensional Euclidean space. It is not a game at this stage.

The current milestone renders one selected 4D object plus a small 4D spatial
reference grid through a real 4D perspective pipeline. The selectable objects
are an exact tesseract, a sampled hypersphere S3, a regular 4-simplex, a
deterministic irregular 16-cell, a procedural 4D spiral curve, and a sampled
quaternion Julia set. A separate first physics experiment adds independent 4D
point particles, fixed-step integration, configurable XYZW gravity, and collision
with the hyperplane W=0. The 4D Gravity Lab extends that same physics world with
pairwise inverse-cube attraction, a static central-mass preset, configurable
true-4D initial conditions, and a reprojectable trajectory. Existing geometry
remains static and unchanged. A third `N-BODY` view adds deterministic random
clouds of 2 to 20,000 finite-radius 4D bodies, selectable gravity quality,
spatial-hash collision detection, and momentum-preserving aggregation.

Both scene geometry and physics state converge on the same projection path:

```text
UI or direct keyboard/mouse input
-> TransformationAnimator4D update (for panel requests)
-> selected IGeometry4D or ReferenceGrid4D vertices
-> Transform4D uniform 4D scale, rotation, and translation

Physics panel -> PhysicsWorld4D fixed steps -> particle Vector4D positions
Physics plane toggle -> finite lattice whose every source vertex has W=0
Gravity Lab -> central/orbiter positions + stored Vector4D trajectory
N-Body Lab -> generated bodies + optional selected-body Vector4D trajectory

all 4D positions
-> Camera4D view transformation
-> 4D perspective projection
-> Wireframe3D
-> translucent faces/cells, lines, or camera-facing 3D particle markers
-> MonoGame 3D view and perspective projection
-> screen
```

## Controls

All rotations are continuous while a key is held.

### Keyboard shortcuts

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
| Change 4D focal distance | `[` / `]` |
| Reset all transforms and cameras | `Space` |
| Exit | `Escape` |

Camera translation moves the 4D observer in world X, Y, Z, or W. Camera4D
rotation changes its orientation in one of the six independent 4D planes.
The same rotation keys without `Shift` rotate only the selected object's
`Transform4D`; they do not rotate the camera or alter any physics-body position.
The bracket keys change the 4D perspective focal distance, while the mouse
wheel changes only the later 3D viewing distance.

### Mouse controls

| Action | Mouse gesture |
| --- | --- |
| Orbit the post-projection 3D view | Hold left mouse button and drag |
| Select a visible N-Body particle | Left-click without dragging in the N-Body scene |
| Move the selected object in X/Y | Hold right mouse button and drag |
| Move the selected object in Z/W | Hold middle mouse button and drag |
| Zoom the post-projection 3D view | Mouse wheel |

Dragging right increases object X or Z; dragging up increases object Y or W,
depending on the held mouse button. Scene gestures are ignored while the
pointer is over the right-side panel. N-Body selection is the only scene click
action and is available only in the N-Body view; a drag remains a 3D orbit.

Implementation: `Input/SandboxInputController.cs`, `SandboxGame.cs`,
`Rendering/NBodyScreenPicker.cs`.

### UI controls

#### Particles

The `PHYSICS` button opens a compact panel dedicated to simulation. The initial
state is enabled but paused, so a newly spawned particle can be inspected before
time advances. Direct object/camera keyboard and mouse controls remain active.

| Physics panel action | Result |
| --- | --- |
| `PHYSICS ON/OFF` | Enables or disables physics updates without deleting bodies |
| `PLAY`, `PAUSE`, `STEP` | Runs, pauses, or executes exactly one 1/60-second physics step |
| `TIME -/+` | Selects simulation-only time scale from 0.1, 0.25, 0.5, 1, 2, 3, 4, 6, 8, or 16 |
| Gravity X/Y/Z/W `-/+` | Changes one gravity component by 0.5 |
| `ZERO G`, `Y GRAVITY`, `W GRAVITY`, `Y + W GRAV` | Applies a repeatable gravity preset |
| Velocity X/Y/Z/W `-/+` | Changes that component of the next particles' initial velocity by 1 |
| `SPAWN 1`, `SPAWN 10`, `CLEAR` | Creates deterministic point particles or clears the physics world |
| `COLLISIONS ON/OFF` | Toggles collision with W=0 |
| Restitution `-/+` | Changes the normal bounce coefficient by 0.1 in `[0, 1]` |
| `PLANE ON/OFF` | Shows or hides a finite projected lattice in the true W=0 hyperplane |

Particle positions are not baked into a 3D animation. Each update takes the
current `Vector4D` positions from `PhysicsWorld4D`, sends them through Camera4D
and the common 4D perspective projector, and only then renders small depth-tested
3D billboards. Non-selected particle hue reflects transformed W depth; the most
recently spawned particle is yellow and is the one reported in the debug panels.

Implementation: `Physics/PhysicsWorld4D.cs`, `Physics/Hyperplane4D.cs`,
`UI/TransformationControlPanel.cs`, `Rendering/WireframeRenderer3D.cs`.

#### Gravity Lab

The Physics screen has separate `PARTICLES`, `GRAV LAB`, and `N-BODY` views. Entering Gravity
Lab for the first time creates one static central mass and one dynamic orbiter,
then leaves the simulation paused. `RESET EXP` recreates both bodies from the
currently displayed pending initial conditions and clears the trail.

| Gravity Lab action | Result |
| --- | --- |
| `PHYSICS ON/OFF` | Enables or disables all physics updates without deleting the experiment |
| `PLAY`, `PAUSE`, `STEP` | Runs, pauses, or advances one fixed 1/60-second step |
| `TIME -/+` | Changes the shared simulation time scale immediately |
| `PAIR GRAV ON/OFF` | Enables or disables pairwise 4D central gravity for all physics bodies |
| `G -/+` | Changes the sandbox gravitational constant by 0.01 in `[0, 0.25]` |
| `Softening -/+` | Changes epsilon by 0.05 in `[0.05, 2]` |
| `Central mass -/+` | Changes the pending static source mass by 100 in `[100, 5000]` |
| Orbiter X/Y/Z/W `-/+` | Changes one pending initial position component by 0.5 |
| Velocity X/Y/Z/W `-/+` | Changes one pending initial velocity component by 0.25 |
| `LOW`, `MEDIUM`, `HIGH` | Sets fixed Y velocities 1.20, 1.75, or 2.30; none promises an orbit |
| `XY ONLY`, `XY + W` | Compares `(0,V,0,0)` with `(0,V,0,Vw)`; default added `Vw` is 0.75 |
| `TRAIL ON/OFF`, `CLEAR TRAIL` | Controls the projected trail without changing physics state |
| Trail length `-/+` | Changes the bounded 4D history capacity by 250 in `[100, 5000]` |
| `FIELD ON/OFF` | Toggles the projected central-to-orbiter reference segment |
| `RESET EXP` | Pauses and recreates the experiment from pending settings |

`PLAY`, `PAUSE`, `STEP`, and time scale are shared with the original particle
view. Gravity Lab reset deliberately sets uniform gravity to zero and disables
the W=0 collision boundary. Otherwise those older experiments would add an
external acceleration or an artificial W>=0 constraint to the orbital result.
Pending central mass, initial position, and velocity values affect the bodies
only after `RESET EXP`. Gravity strength, softening, play state, time scale,
trail visibility, field visibility, and trail capacity affect the current
experiment immediately. Global keyboard/mouse camera and object controls remain
available; they change visualization transforms, not the stored body physics.

Implementation: `Physics/GravityLab4D.cs`, `Physics/GravitySystem4D.cs`,
`Physics/PhysicsWorld4D.cs`, `UI/TransformationControlPanel.cs`.

#### N-Body Lab

The `N-BODY` view starts with a paused reproducible random cloud. `Count` and
`Seed` are signed-integer text fields; press `Enter`, `APPLY`, or `GENERATE` to
validate them. Counts are clamped to `[2, 20000]`, while invalid text restores
the previous valid value.

| N-Body action | Result |
| --- | --- |
| `Count`, `APPLY` | Validates/clamps the pending count to `[2, 20000]`; does not replace the current system until generation |
| `Seed`, `APPLY`, `RANDOM` | Sets or randomizes the pending signed 32-bit seed; does not regenerate automatically |
| `GENERATE`, `RESET` | Builds the configured random cloud, or rebuilds identical initial conditions from the same settings and seed; generation finishes paused |
| `PLAY`, `PAUSE`, `STEP` | Runs, pauses, or advances exactly one fixed step; `PLAY` also enables an OFF world |
| `TIME -/+` | Changes the simulation time scale immediately without changing camera or generation settings |
| Position X/Y/Z/W `-/+` | Changes one independent spawn half-range by 1; applies on generation |
| Speed min/max `-/+` | Changes the random 4D speed-magnitude interval by 0.1; applies on generation |
| Mass min/max `-/+` | Changes the uniform simulation-mass interval by 0.5; applies on generation |
| Radius `-/+` | Changes `k` in `radius = k mass^(1/4)` by 0.01; applies on generation |
| Point `-/+` | Changes only the current rendered marker scale by 0.25; physics radii do not change |
| `G -/+`, `Softening -/+` | Changes current mutual-gravity strength or numerical softening immediately and preserves it for regeneration |
| `GRAVITY ON/OFF` | Immediately enables/disables mutual gravity; integration and aggregation continue |
| `MERGE ON/OFF` | Immediately enables/disables 4D collision aggregation independently of gravity |
| `EXACT`, `MEAN FIELD` | Requests direct pair gravity or global-COM approximation; exact automatically uses mean field above 1000 bodies |
| `COLOR W/MASS/SPEED` | Selects point color without changing physics |
| Left-click a visible marker | Selects that projected 4D body; no modifier is required and left-drag remains 3D orbit |
| `TRAIL OFF/SELECTED` | Stores no history, or one bounded 1000-position `Vector4D` trail for the current selected body |

The default is 500 bodies, seed 1337, XYZW ranges +/-10, speed 0..1, mass
1..10, `k=0.08`, `G=0.060`, softening 0.25, with gravity and aggregation ON.
The selected object mesh is hidden in this view so it does not obscure the
point cloud; the common reference grid and both cameras remain available.

Picking uses the same pipeline as rendering: body `Vector4D` position,
Camera4D, 4D perspective projection, the 3D orbit camera, and finally screen
coordinates. The click tolerance follows marker size with a six-pixel minimum.
If projected markers coincide, the comparison is deterministic: screen
distance, 3D screen depth, 4D camera depth, then body ID. Hidden, clipped, and
off-screen bodies cannot be selected. The selected marker is yellow and 1.8
times its normal marker radius.

Changing selection resets the selected-body trail immediately and records only
the new target. If the selected body is absorbed during aggregation, selection
transfers to the deterministic surviving body and the trail switches to that
survivor; no dead-body reference is retained. The diagnostics report selected
body P/V/A, total mass/momentum, requested/effective gravity mode, merge count,
collision interval, physics-step time, and simulation step rate.

Implementation: `Physics/NBodyLab4D.cs`, `Physics/PhysicsWorld4D.cs`,
`Physics/AggregationCollisionSystem4D.cs`, `Rendering/NBodyScreenPicker.cs`,
`Rendering/WireframeRenderer3D.cs`, `SandboxGame.cs`.

##### N-Body performance diagnostics

Entering the N-Body view also shows a read-only `N-BODY PERFORMANCE` panel in
the scene. Every phase displays the most recently completed frame and the
average of a bounded 60-frame window. The profiler uses high-resolution
`Stopwatch` timestamps around whole phases; it does not time individual bodies
or pair interactions and does not write per-frame logs.

| Measurement | Exact scope |
| --- | --- |
| Physics total | All fixed physics steps executed for the presented frame |
| Gravity | Exact pairwise or mean-field acceleration calculation only |
| Collision | Aggregation spatial-hash build, neighbor candidates, and 4D overlap tests; merge calls are excluded |
| Aggregation | Actual inelastic merges plus removal/selection cleanup |
| Integration | Semi-implicit velocity/position integration and the existing W=0 plane check; that plane is disabled by N-Body defaults |
| Trails | The synchronous selected-trail fixed-step callback |
| Prep 4D->3D | CPU preparation of the projected object, grid, N-Body points/trail, and picking data |
| N-body draw CPU | CPU billboard creation and MonoGame draw submission for N-Body points/trail |
| UI update | Right-panel input/layout update |
| Update/Render/Frame total | CPU wall time for the corresponding MonoGame phases; multiple catch-up Updates accumulate into one presented frame |

Scheduling diagnostics keep wall-clock frame elapsed time, MonoGame scheduler
`GameTime`, and simulated time advanced by fixed steps separate. This matters
when MonoGame caps elapsed scheduler input after a long stall. The panel also
reports the fixed timestep, remaining simulation accumulator, steps per
presented frame, both wall-clock and scheduler step rates, time scale, logical
processor count, and the physics thread. The current implementation is
single-threaded and executes physics on the MonoGame main thread.

`N-body draw CPU` and `Render CPU` are CPU preparation/submission measurements,
not GPU execution time. The current renderer has no GPU timestamp-query/fence
instrumentation, so GPU time is deliberately reported as unavailable rather
than inferred. Requested and effective gravity modes remain separate, making an
`EXACT` request above 1,000 bodies visibly report the existing `MEAN FIELD`
fallback.

Implementation: `Diagnostics/PerformanceProfiler.cs`,
`Physics/PhysicsWorld4D.cs`, `Physics/AggregationCollisionSystem4D.cs`,
`Physics/NBodyLab4D.cs`, `Rendering/DebugOverlayRenderer.cs`, `SandboxGame.cs`.

#### Object modes

The right-side panel adds these animated object controls while preserving every
direct control above:

| Panel action | Result over 1 second |
| --- | --- |
| `TESSERACT`, `HYPERSPHERE`, `4-SIMPLEX`, `IRREGULAR`, `4D SPIRAL`, `4D FRACTAL` | Selects the only rendered object; Camera4D, 3D view, and projection remain unchanged |
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

Implementation: `UI/TransformationControlPanel.cs`, `SandboxGame.cs`,
`Transformations/Transform4D.cs`, `Transformations/TransformationAnimator4D.cs`.

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

When `4D FRACTAL` is selected, its compact panel controls one quaternion Julia
dataset. Parameter edits are pending until `GENERATE`; the currently displayed
dataset remains valid while the new one is computed:

| Fractal action | Result |
| --- | --- |
| `C.a/b/c/d -/+` | Changes one component of the pending quaternion constant by 0.05 within `[-1.5, 1.5]` |
| `Max iterations -/+` | Changes the escape iteration limit by 4 within `[4, 128]` |
| `Escape radius -/+` | Changes the escape radius by 0.25 within `[1, 8]` |
| `Resolution -/+` | Changes every grid axis by 2 within `[6, 20]`; total work is `N^4` |
| `PRESET 1/2/3` | Selects one tested sampling-friendly quaternion constant |
| `GENERATE` | Starts a new incremental scan in batches of 512 samples per update |
| `CANCEL` | Stops the pending scan and keeps the last completed dataset |
| `RESET` | Restores default parameters/view settings and starts a default scan |
| `COLOR BY W` | Uses transformed world W for cyan/neutral/pink hue and W magnitude for intensity |
| `COLOR BY ITER` | Uses escape time; fast escapes are blue, slow escapes orange, and bounded points pale |
| `SHOW W SLICE` | Debug-only local-W filter; normal mode projects the full 4D dataset |
| `SLICE W -/+` | Moves the debug slice by 0.25 within the current sample bounds |
| `POINT 1/2/3` | Cycles the camera-facing 3D marker size |
| `SHOW POINTS` | Toggles the fractal point cloud; default ON |

Selecting an empty fractal slot automatically starts the default scan. The
panel and debug overlay report progress, sample counts, and elapsed time. No
worker thread is required: each small batch finishes inside one `Update`, then
input and rendering resume before the next batch.

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
-> quaternion Julia camera-facing 3D point markers
-> object/camera debug panel
```

`VisualizationPalette.CellSurfaceAlpha` is the single surface-opacity setting and
defaults to `0.18`. The Display panel legend reads from the exact same palette
as the scene renderer, so its cell, edge, and vertex colors cannot drift from
the actual visualization.

## Project structure

```text
Mathematics/       Vector3D/Vector4D values and minimal quaternion algebra
Geometry/          shapes plus quaternion Julia parameters, samples, and generator
Scene/             geometry instance state plus curve-prefix playback state
Transformations/   six-plane rotation, object transform, and time-based animator
Projection/        Camera4D, perspective projection, and pipeline orchestration
Physics/           fixed-step bodies/world, gravity labs, seeded clouds, spatial collision/aggregation
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
- `Quaternion4D` represents `q = a + bi + cj + dk`; it is separate from the
  spatial `Vector4D` because quaternion multiplication is not component-wise.
  General multiplication follows the Hamilton rules, including `ij=k` and
  `ji=-k`. The optimized square used by the generator is
  `(a^2-b^2-c^2-d^2, 2ab, 2ac, 2ad)`.
- `QuaternionJuliaGenerator4D` evaluates `q(n+1)=q(n)^2+C` at every point of a
  uniform four-dimensional grid. A sample escapes when `|q|^2` exceeds the
  configured squared escape radius; surviving `MaxIterations` marks it bounded.
  Non-finite magnitude or components are treated as escaped. The default grid
  is `12^4 = 20,736` points over `[-1.5, 1.5]` on X/Y/Z/W, with 24 iterations,
  radius 4, and `C=(-0.35, 0.15, 0.10, 0)`.
- The generated dataset retains both escaped and bounded samples. This is
  intentional: keeping only bounded points would make escape-iteration coloring
  degenerate because every retained point would have the same iteration count.
  Fast escaped samples are visually dimmed, not mislabeled as members of the
  bounded Julia region.
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

## 4D physics model

- `PhysicsBody4D` contains 4D position, velocity, acceleration, positive mass,
  collision radius, alive state, static flag, and an identifier. Its kinetic energy
  is `E = 0.5 m (vx^2 + vy^2 + vz^2 + vw^2)`.
- Each fixed step uses semi-implicit Euler in all four coordinates without a
  W special case: `v(n+1) = v(n) + a dt`, followed by
  `p(n+1) = p(n) + v(n+1) dt`. The fixed `dt` is 1/60 second.
- `PhysicsWorld4D` accumulates elapsed simulation time and executes whole fixed
  steps. Time scale multiplies only the accumulated simulation time. Pause
  clears the partial accumulator; STEP calls exactly one fixed update while the
  UI keeps the world paused.
- Gravity is the acceleration vector assigned to every particle. Its default is
  `(0, -9.8, 0, 0)`, and X, Y, Z, and W are treated identically. Mass is retained
  for kinetic energy and future force work; uniform gravity is mass-independent.
- `Hyperplane4D` represents the normalized equation `n dot p + d = 0`, with the
  positive half-space considered valid. The current collision boundary is
  `n=(0,0,0,1), d=0`, hence W=0 and W>=0 is valid.
- A penetrated point is projected back to the boundary with
  `p = p - n (n dot p + d)`. Only inward normal velocity is reflected:
  `v = v - (1+e)(v dot n)n`, where restitution `e` is clamped by the UI to
  `[0,1]`. All three tangential components remain unchanged. For W=0 this is
  exactly `vW = -e vW`.
- `HyperplaneGrid4D` is a finite visualization of the infinite W=0 boundary. It
  contains X-, Y-, and Z-directed lattice lines, all with source W exactly zero,
  and uses the same transform/camera/projector as the particles and scene. It is
  not an arbitrary post-projection 3D plane.
- Particle spawning is deterministic and capped at 500 bodies. Clearing and
  respawning produces the same initial positions, while the configured initial
  XYZW velocity is copied exactly without random jitter.

## 4D Gravity Lab

In `n` spatial dimensions, flux through a hypersphere grows as `r^(n-1)`. The
Newtonian-like central-field generalization used here therefore has force
magnitude `1/r^3` for `n=4`, not the `1/r^2` law of three-dimensional space.
For two bodies with `r = p2-p1`, the unsoftened vector law is:

```text
F1 = G m1 m2 r / |r|^4
F2 = -F1

a1 = G m2 r / |r|^4
a2 = -G m1 r / |r|^4
```

`GravitySystem4D` evaluates every unordered body pair in O(N^2). Both dynamic
bodies receive mass-weighted opposite acceleration, so internal forces are equal
and opposite and isolated two-body momentum is conserved to floating-point
roundoff. A static body remains a source but is not integrated; fixing it is an
external constraint, so momentum conservation is not expected for that preset.

The singular denominator is softened without a force clamp:

```text
r2Effective = |r|^2 + epsilon^2
a1 = G m2 r / r2Effective^2
```

At distances much larger than epsilon this approaches `G m2 r/|r|^4`, whose
magnitude is `G m2/|r|^3`. At coincidence the vector is exactly zero and remains
finite because no direction is mathematically preferred. Softening changes the
near-field law, so close encounters are experiments in the softened model, not
the exact singular inverse-cube model. Defaults are `G=0.05`, `epsilon=0.25`,
central mass 1000, radius 4, and simulation mass/length/time units with no claim
of correspondence to real-world SI gravity.

The central preset is static at `(0,0,0,0)`. The orbiter has mass 1 and fully
configurable pending `Vector4D` position and velocity. Nothing calculates or
corrects velocity to guarantee a circular orbit. LOW/MEDIUM/HIGH are literal
fixed values, and `XY+W` adds a real W velocity before reset.

`Trajectory4D` stores the orbiter position after every completed fixed step as
original `Vector4D` data, up to a configurable bounded capacity. Every frame the
current trail is rebuilt as sequential 4D edges and passed through the same
Camera4D and perspective projector as all scene data. Changing XW/YW/ZW camera
orientation therefore reprojects the complete history without changing a single
stored point. Trail hue is cyan through neutral to pink based on the point's
actual world W coordinate.

With the defaults and fixed `dt=1/60`, deterministic 60-s diagnostics currently
produce these observations:

| Initial velocity | Observed radial range/result |
| --- | --- |
| LOW `(0,1.20,0,0)` | `r min 0.269`, then `r final/max 136.689`: close plunge and escape-like ejection |
| MEDIUM `(0,1.75,0,0)` | `r` stayed in `[1.956,4.002]`, final `2.412`: bounded over this finite observation only |
| HIGH `(0,2.30,0,0)` | `r final/max 88.623`: escape-like trajectory |
| XY+W `(0,1.75,0,0.75)` | real fourth-dimensional motion; `W final -12.071` after 60 s |

The medium result is not labeled a stable orbit. An inverse-cube central field
has very different radial stability from the familiar inverse-square problem,
and softening plus numerical integration also changes close-range behavior.

## 4D N-Body Gravity and Aggregation

`NBodyGenerator4D` uses a small SplitMix64 sequence, so generation is independent
of changes to `System.Random`. Position is uniform in the configured 4D box.
Velocity direction comes from four independent normal components normalized to
the unit 3-sphere `S3`, then multiplied by a uniform speed magnitude; this avoids
the directional bias of normalizing a uniform hypercube sample. Mass is uniform
in the configured interval and radius is `r = k m^(1/4)`, matching the scaling
of four-dimensional hypersphere volume.

Initial overlap rejection and runtime collision candidates use a uniform 4D
spatial hash. A cell width of twice the largest current radius guarantees that
an overlapping pair is in the same or an adjacent cell on every axis. The 81
neighboring 4D cells are checked, and the final test always uses complete XYZW
distance. Generation aborts safely after 256 failed attempts for one body and
keeps the previous system intact.

An overlapping pair merges without creating a new body. The more massive body
survives; equal masses use lower identifier as the deterministic tie-breaker.
For total mass `M=m1+m2`:

```text
pNew = (m1 p1 + m2 p2) / M
vNew = (m1 v1 + m2 v2) / M
rNew = k M^(1/4)
```

These vector equations include X, Y, Z, and W. They conserve total mass, linear
momentum, and center of mass for the isolated collision, but deliberately do not
conserve translational kinetic energy. The request's first numerical example
contains an arithmetic typo: `(0.25*2 + 1*(-1))/1.25` is `-0.4`, not `-0.6`.
The implementation and checks use `-0.4`.

Exact gravity visits each unordered pair and is permitted through 1000 bodies.
Above that threshold an `EXACT` request is visibly reported as effective
`MeanFieldApproximate`; it never silently evaluates roughly 200 million pairs.
The O(N) approximation collapses all other mass to its 4D center of mass for
each body. It preserves broad global attraction but not local pair structure or
exact total momentum. This is a deliberate first responsive quality mode, not
Barnes-Hut and not a claim of astrophysical fidelity.

Runtime collision checks use the same spatial hash. They run every fixed step
through 1000 bodies, every second step through 5000, and every fourth step above
5000; integration and gravity still use the fixed 1/60-second timestep. A body
can therefore overlap briefly before a large-system collision pass. The UI and
overlay report the interval, requested/effective gravity mode, active count,
mass, momentum, last/total/rate of merges, average speed, largest mass, maximum
absolute W, physics time, simulation steps/second, and render FPS.

A local deterministic check run on the development machine observed these
single-threaded debug measurements; they are diagnostics, not portable targets:

| Bodies | Gravity | Generation | Average fixed-step work |
| ---: | --- | ---: | ---: |
| 100 | exact | about 0.8 ms | about 2.0 ms |
| 1,000 | exact | about 8.2 ms | about 129 ms |
| 20,000 | mean field, collision every 4 steps | about 103 ms | about 37 ms |

The 1000-body exact result is intentionally exposed as expensive. Select mean
field when interaction responsiveness matters more than pairwise accuracy.

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

Fractal generation is likewise independent of rendering. A
`QuaternionJuliaGeneration4D` owns only the deterministic grid cursor, sampled
results, progress, and timer. `SandboxGame.Update` advances 512 points per frame
and publishes one immutable `QuaternionJuliaSet4D` only when complete. The
published `Vector4D` points then use the same `Transform4D`, `Camera4D`,
`PerspectiveProjector4D`, and `WireframeProjectionPipeline4D` as every other
object. The renderer consumes the resulting `Wireframe3D`; it never iterates
quaternions or rotates already projected points.

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

- N-body bodies have collision radii and aggregate as 4D hypersphere-like
  particles. They still have no angular state, rigid-body contact response,
  geometry collisions, constraints, fluids, or gameplay.
- Hyperplane collision uses end-of-step penetration correction and velocity
  reflection. It does not yet solve the exact time of impact and integrate the
  unused fraction of a step, so a fast bounce ends that step on the boundary.
- To prevent a debugger pause from causing an unbounded catch-up loop, one game
  update executes at most eight physics steps and discards older excess backlog.
  Normal deterministic runs at the configured update rate are unaffected, but
  the simulation deliberately loses elapsed real time after a long stall.
- The W=0 lattice is only a finite visual sample of an infinite three-dimensional
  hyperplane in 4D. It does not change collision mathematics and can be hidden.
- The last spawned body is the selected debug body; body picking and selection
  controls are deferred.
- Exact mutual gravity is O(N^2) and capped at 1000 bodies. The large-system
  mean-field mode is only a global-COM approximation; there is no Barnes-Hut tree.
- No central/orbiter collision or capture radius exists. A close approach passes
  through the softened field and may emerge as a high-speed flyby.
- The overlay reports kinetic energy but makes no conservation claim during
  inelastic merges. It intentionally does not reuse the incorrect 3D potential
  `-Gm1m2/r`; no total-energy claim is made yet.
- Semi-implicit Euler is deterministic and momentum-symmetric here, but it is
  not a symplectic high-accuracy orbital integrator. Long-run energy drift and
  sensitivity near the softened core remain expected numerical limitations.
- `Trajectory4D` removes the oldest list element at capacity. With the current
  maximum of 5000 points this simple O(N) shift is acceptable; a ring buffer can
  replace it if trails become substantially larger.

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
- Uniform fractal sampling grows as `N^4`. The UI is deliberately capped at
  `20^4 = 160,000` samples. This point cloud is useful for exploration but can
  miss features thinner than its grid spacing and is not a reconstructed 4D
  boundary surface or adaptive sampler.
- The fractal renderer draws all sampled points so escape-time coloring remains
  meaningful. Bounded points are visually distinct and quick escapes are dim,
  but dense settings can still obscure interior structure. Depth-tested
  billboard quads are a practical first 3D representation, not true spheres.
- `SHOW W SLICE` filters samples by their original local W using half a grid
  interval of tolerance. It is only a comparison/debug view; when it is off,
  the complete 4D dataset goes through perspective projection.
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
- There is no fractal surface reconstruction, complex-object gravity, gameplay,
  opaque solid geometry, or general-purpose UI framework.

Any future general slicing strategy should remain separate from the perspective
projection pipeline. The current fractal W slice is deliberately a narrow debug
filter, not the sandbox's primary visualization mode.
