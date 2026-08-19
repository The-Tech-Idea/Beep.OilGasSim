Beep Oil and Gas Sim - Godot-safe isometric ground tiles v3

GROUND TILE STANDARD
- PNG size: 256x128
- Projection: 2:1 isometric
- Logical continuous diamond:
  top    (128, 0)
  right  (256, 64)
  bottom (128, 128)
  left   (0, 64)
- PNG raster bounds remain x=0..255 and y=0..127.
- Transparent outside diamond.
- Flat top surface only.
- No slab thickness, no bevel/sidewall, no baked shadow.
- All variants of one environment share the same outer-edge treatment.

GODOT
- Texture Filter: Nearest
- Mipmaps: Off
- Atlas Margin: 0
- Atlas Separation: 0
- Tile size: 256x128
- Isometric half steps: X=128, Y=64
- Use several alternative tiles / probabilities for large uniform areas.
- Avoid placing the exact same alternative in neighboring cells where possible.

WHY A PERFECT TILE CAN STILL LOOK SEPARATE
Geometry seams and perceptual repetition are different problems.
The v3 process removes geometry/rim seams. Repeating the same cracks,
grass clumps, or brightness pattern still exposes the grid visually.
Random alternatives plus a subtle world-space macro-noise/detail overlay
are recommended for large continuous terrain.
