# Blossom Rally: Unity project (Mac M1 ready)

A drift-friendly rally game through a Chinese spring valley. It has cherry blossoms and
falling petals, pastel fog, real-time shadows, lanterns, a pagoda on the mountain, a
lotus lake, a bamboo forest and tea terraces. It's built from the map blueprint and
the 9 Blender cars.

## Open it

1. **Unity Hub → Add → Add project from disk** → select this `UnityProject` folder.
2. If Hub asks about the editor version, choose the Unity you have installed.
   Unity **2021.3 LTS, 2022.3 LTS and Unity 6** are all supported. It uses the built-in
   render pipeline and only built-in modules, so no extra packages are needed.
3. The first import takes a few minutes. When it finishes, the scene
   `Assets/BlossomRally/Scenes/BlossomValley.unity` opens automatically.
   If it doesn't, use the menu **Blossom Rally → Open Game Scene**.
4. If Unity asks to **restart** (to enable keyboard input), click *Restart*.
5. Press **Play**. The valley builds itself in a few seconds, with a loading bar.
   Pick a car and drive.

> Tip: maximise the Game view (**Shift + Space**, or "Maximize on Play") and set its
> aspect to 16:9.

## Controls

| Key | Action |
| --- | --- |
| W / ↑ | Throttle |
| S / ↓ | Brake, then reverse |
| A D / ← → | Steer |
| **Space** | Handbrake: flick it to start a drift |
| 1 – 9 | Switch car on the spot |
| R | Reset to the nearest road |
| C | Camera: chase → cockpit (driver view) → hood → cinematic |
| P | Petals: calm → spring → blossom storm |
| G | Fog density |
| F | Colour grade on / off |
| H | Help overlay |
| Esc / Tab | Back to car select (← → choose, Enter drive) |

**Drifting:** at speed, steer in and tap Space, then hold throttle and counter-steer.
Drift score builds with angle × speed, and the combo grows the longer you hold the
slide. Crashing breaks the combo.

## The cars

| # | Car | Class |
| --- | --- | --- |
| 1 | Brenner S1 | Group B, AWD |
| 2 | Mistral W1 | Group B, RWD wedge |
| 3 | Isar 3R | Group B, RWD |
| 4 | Falcon Mk2 RS | Group 4 |
| 5 | Torino 131 | Group 4 |
| 6 | Falcon Mk1 Rally | Classic RWD |
| 7 | Elster 2T | Classic RWD |
| 8 | Kestrel 9R | Classic RWD, rear-engine |
| 9 | Sorrel A1 | Classic RWD, lightweight |

## The valley (from Map Blueprint v1)

* Start at the **Garage** in Blossom Village. Drive up **Lantern Street** through
  the **花村 Paifang Gate** onto the **main loop**, which crosses the river on two
  **stone arch bridges**.
* Side routes: **Pagoda hairpins** up the mountain to the pagoda lookout, the
  **Terrace climb** (gravel) through the tea terraces, and the **Bamboo run** (dirt)
  through the forest. You can also drive off-road anywhere on the grass.
* Grip changes with the surface: asphalt and paving > gravel > dirt > grass. Off-road
  is more slippery, which makes it easier to drift.

## What is where

```text
Assets/BlossomRally/
  Scenes/BlossomValley.unity      created on first open (or press Play in any scene)
  Scripts/                        GameManager, WorldBuilder, ArcadeCar, CameraRig, PetalWeather,
                                  Atmosphere, PostFX, CarFactory, MaterialLibrary, MapData ...
  Editor/                         import settings + first-open setup
  Resources/Cars/CAR-xxx/         car FBX, livery texture, wheel data (from Blender)
  Resources/Env/                  environment FBX kit + sign/road textures (from Blender)
  Resources/Shaders/              vertex-colour wind, lantern glow, water, sky, particles, glass, post FX
```

## If something looks off

* **Pink or magenta surfaces:** a shader failed to compile on your Unity version.
  Send me the error from the Console.
* **Low frame rate:** in `Atmosphere.cs`, lower `QualitySettings.shadowDistance` (170)
  or `antiAliasing` (4). In `CameraRig.cs`, lower the tree cull distance (400 m).
* **No keyboard response:** Edit → Project Settings → Player → *Active Input Handling*
  = **Both**, then restart Unity.

## How it was checked

All C# was compiled against the Unity 2021.3 reference assemblies with the .NET
compiler: 0 errors, 0 warnings for both the game and editor assemblies. The Unity 6
code paths differ only by Unity 6's renamed physics APIs. The road layout was checked
numerically: every road is at most 10% grade, there are no self-crossings, the street
joins the main loop, and the 2 bridges sit at the river crossings. I could not run
Unity itself here, so the first real play test is yours. Please tell me anything you'd
like changed.
