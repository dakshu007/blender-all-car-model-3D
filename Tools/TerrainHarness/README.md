# Terrain harness (development tool, not shipped)

Evaluates the game's pure world code (WorldField, WorldLayout, every region's non-`.Build.cs` files) outside Unity
against a tiny `UnityEngine` math stub, prints road statistics (length, max grade, cut / fill depth, tightest radius,
height range) and renders a shaded top-down map with contours, tile borders and roads.

    ./run.sh                          # whole world -> out/world.png (4 m / px)
    ./run.sh --region Snow            # one region + 150 m margin -> out/Snow.png (2 m / px)
    ./run.sh --only Snow --region Snow   # same, other regions use placeholders/
    ./run.sh --profile 0 0 0 1500     # height profile along a line
    ./compile_game.sh [Region]        # full game + editor compile against Unity refs + Unity 6.6 API check

Rule for region code: files named `*.Build.cs` may use the engine (GameObjects, meshes, materials); every other
file in `Scripts/World/Regions/<Region>/` must stay pure (Vector2/3, Color, Mathf, Rect, lists) so this harness
can compile it.
