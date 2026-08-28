# 4D perspective model

This project uses a pinhole-style perspective camera in four dimensions. It is
one useful visualization model, not the only valid way to display 4D geometry.

## Camera space

Camera4D has a four-dimensional position `c` and an orientation `R`. Its local
`+W` axis is the forward direction. A world point is converted to camera space by:

```text
p_camera = inverse(R) * (p_world - c)
```

`Rotation4D` is orthogonal, so its inverse is computed exactly by applying the
opposite plane rotations in reverse composition order.

The default camera is at `(0, 0, 0, -4)` with identity orientation. The origin
therefore has positive camera-space W depth `4`.

## Projection from 4D to 3D

The camera is the projection center at the camera-space origin. Rays intersect
the three-dimensional hyperplane `W = d`, where `d` is the focal distance. For a
camera-space point `(x, y, z, w)` with valid positive depth:

```text
scale = d / w

x' = x * scale
y' = y * scale
z' = z * scale
```

The result `(x', y', z')` is stored in `Wireframe3D`; it is not drawn directly as
2D coordinates. The renderer later converts it to MonoGame `Vector3`, reuses the
same projected indices for cell faces, and uses a normal 3D camera plus
MonoGame's 3D perspective projection to reach the screen. Tesseract, sampled
hypersphere, simplex, irregular polytope, and the sampled 4D spiral all use this exact path through the
common `IGeometry4D` overload; object selection is not a special projection mode.

For visualization metadata only, each projected vertex also retains its local
source W and transformed world W. The spiral renderer uses world W for its
gradient. Neither value participates in, or modifies, the perspective formula.

Increasing `d` magnifies the projected 3D representation. Moving or rotating
Camera4D changes camera-space W and therefore changes the actual 4D perspective.

## Safety near and behind the camera

The perspective formula is singular at camera-space `W = 0`. The projector uses
a small positive near hyperplane, currently `W = 0.1`:

- `W <= 0.1` is rejected;
- points behind the camera are rejected;
- non-finite input or output is rejected;
- rejected vertices never reach the MonoGame renderer;
- an edge is drawn only when both endpoints are valid.

This is safe but is not full 4D edge clipping. When an edge crosses the near
hyperplane, it disappears until both endpoints are valid. Polygonal faces with
any rejected vertex are also skipped as a whole, so no triangle can cross the
perspective singularity. Proper intersection and clipping can be added later
without changing any source geometry or the 3D renderer.

## Slicing

W slicing is intentionally absent. If introduced later, it should be a separate
visualization mode that produces its own 3D representation alongside, rather
than inside, `WireframeProjectionPipeline4D`.
