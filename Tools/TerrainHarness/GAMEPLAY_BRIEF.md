# Open-world gameplay brief (shared by all gameplay builders)

Blossom Rally is an original, stylised, Chinese-themed open-world driving game: Unity 6.6 (6000.6.8f1), built-in
render pipeline, linear colour, IMGUI UI, and everything generated at runtime from C# (no scene content).
- **World:** a 3 × 2 km map of six regions:
  - Blossom Valley;
  - Jade Harbour City;
  - Lotus Coast;
  - Cloud Peak Mountains;
  - Frost Summit (snow);
  - Red Canyon (desert).

  See `Documentation/OPEN_WORLD.md` and `Scripts/World/Regions/*`.
- **Vehicles and map:** 21 cars (`CarCatalog`), a minimap plus the M world map with GPS (`Menus/*`), 17 rally events and fast travel.
- **The goal now:** turn it into a real open-world game in the spirit of GTA V. Keep the cosy Blossom theme: **no
  violence, no weapons, no crime**. Wanted features:
  - getting out of cars and walking around;
  - explorable tea houses and temples;
  - work to earn money;
  - car showrooms where you buy cars;
  - motorbikes;
  - a living world;
  - much more realistic, detailed graphics.

  Everything must be super cool, clean, readable, and still fast on low-end machines: Low quality targets 200 FPS.

## Shared contracts (in `Scripts/Gameplay/Shared/` — use them, keep their public API stable)
- `PlayerState`: OnFoot, Person, Vehicle, Position, Forward and the OnFootChanged event. Minimap, AI and missions
  must use `PlayerState.Position`, not the car.
- `Interactable`: prompt, detail, radius, onFoot/inVehicle, available and onUse. Add one to anything usable; the
  on-foot module's interaction system shows "E Prompt" and calls Use().
- `Wallet`: money (¥) plus owned vehicle ids. The economy module saves it.
- `PersonFactory.Create(PersonLook, pos, yaw, parent, layer)` returns a `PersonRig` with
  `SetPose(Idle/Walk/Run/Sit/Drive/Ride/Wave/Work/Pray)` and a `speed` field.
  - It is a PLACEHOLDER capsule today. The on-foot module replaces the internals with the real animated character.
  - Code against the API only.
- `GameplayHooks`: OpenShowroom(id), OpenJobBoard(id), OpenShop(ShopDef), Toast(msg) and the MenuOpen flag. Screens
  are owned by the economy module; places call the hooks.

## Rules
- Write in your own folder under `Scripts/Gameplay/<Module>/` (plus the files listed as yours).
  - Prefer your own MonoBehaviours with their own OnGUI or Update, self-registered with
    `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` and `WorldHooks.WorldBuilt` / `WorldHooks.CarSpawned`.
  - Avoid editing shared core files.
  - If you must touch a file another module owns, make a tiny, local edit. Re-read the file right before editing.
- Never edit another module's folder. Five other builders work at the same time. If the compile breaks in a file you
  don't own, wait a minute and retry.
- Compile with `cd Tools/TerrainHarness && ./compile_game.sh`. It must say `compile OK` and `API problems: 0` (Unity
  6.6 API check: GetInstanceID, removed FindObjects overloads and so on are errors).
- Do not commit; the integrator commits. You cannot run Unity, so reason carefully: IMGUI event order, unscaled time
  while paused, null checks, no per-frame GC allocations, and Low quality (`GameSettings.Quality` == 0) stays cheap.
- Art style: low-poly, clean, pastel and vertex-coloured, built with the geometry toolkit:
  - `WorldProps/PropKit.cs` (`PropModel`, `PG`, `Pal`) and `World/ValleyLife.cs` (`LifeGeo`, `LifeBatch`);
  - `MaterialLibrary` and the shaders in `Resources/Shaders`.
- Placement in regions: use `RegionContext`-style logic. Read terrain height with `WorldBuilder.TerrainHeight`
  (`GameManager.Instance.World`), keep clear of roads (`WorldField.EdgeDistance`), avoid existing colliders (Physics
  overlap checks) and region props. `Region.Context.Root` is streamed with the region.
- Input:
  - Keyboard/mouse plus gamepad through `InputReader` and `GamepadBridge`.
  - Interact = **E** / gamepad X (West).
  - Enter or exit a vehicle = **F** / gamepad Y (North): check it doesn't clash with photo or camera actions; rebind
    if needed and say so.
  - Respect `GameplayHooks.MenuOpen` and pause.
- Report at the end (at most about 400 words): what you built, the controls, files with line counts, the compile
  result, and what is left to check in Unity.
