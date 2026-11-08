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
| **Space + steer** | Start a drift (see below) |
| 1 – 9 | Switch car on the spot |
| R | Reset to the nearest road |
| C | Camera: chase → cockpit (driver view) → hood → cinematic |
| P | Petals: calm → spring → blossom storm |
| G | Weather: Clear Spring → Spring Haze → Cloudy → Fog Off |
| F | Colour grade on / off |
| H | Help overlay |
| **Esc** | Pause menu: weather / fog, petals, colour grade, camera, change car, help |
| Tab | Back to car select (← → choose, Enter drive) |

**Drifting** is a controllable state:

* **Start:** above about 30 km/h, hold **Space** and steer. The tail kicks out in the
  direction you steer.
* **Hold it:** **W** keeps the slide going. Steer **into** the drift for more angle;
  **counter-steer** for less. Holding Space tightens the line.
* **Switch sides:** press Space while steering the other way to flick into the
  opposite drift (good for S-bends).
* **Exit:** counter-steer or lift off the throttle. The car straightens and settles,
  with no pendulum spin.
* The meter at the bottom of the screen shows your drift angle (bar) and the angle your
  inputs are asking for (white tick).
* Slides you didn't start on purpose (gravel, grass, bumps) are caught by the same
  controller, so they stay controllable too.

Drift score builds with angle × speed, and the combo grows the longer you hold the
slide. Crashing breaks the combo.

**Weather** (Esc menu or G) is remembered between sessions:

| Preset | Look |
| --- | --- |
| Clear Spring | Light haze, few clouds, strong sun and crisp shadows |
| Spring Haze | The original soft pink fog with drifting mist (default) |
| Cloudy | Overcast grey-lilac sky, moving clouds, soft shadows, thicker fog, breezier wind |
| Fog Off | No fog or mist, light clouds: see across the whole valley |

Weather changes fade over about 2.5 seconds.

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
  **stone arch bridges**. Each bridge deck is part of the road itself, so there are
  no seams or steps. The bridges have a gentle hump (at most about 10% grade), a stone
  arch over the water, parapets with balusters and red lanterns at the four corners.
* Side routes: **Pagoda hairpins** up the mountain to the pagoda lookout, the
  **Terrace climb** (gravel) through the tea terraces, and the **Bamboo run** (dirt)
  through the forest. You can also drive off-road anywhere on the grass.
* **Grass:** the meadows and road verges are filled with dense, wind-swept grass blades
  (with daisies, buttercups and fallen petals under the cherry trees). Drive through it
  and the grass bends away from the car, shakes in your wake and settles again.
  Clippings fly from the tyres.
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
  Resources/Shaders/              vertex-colour wind, grass (+ BR_Interact.cginc car interaction),
                                  lantern glow, water, cloud sky, particles, glass, post FX
```

## If something looks off

* **Pink or magenta surfaces:** a shader failed to compile on your Unity version.
  Send me the error from the Console.
* **Low frame rate:** in `Atmosphere.cs`, lower `QualitySettings.shadowDistance` (170)
  or `antiAliasing` (4). In `CameraRig.cs`, lower the tree cull distance (400 m) or the
  grass cull distance (130 m). In `WorldBuilder.BuildGrass`, lower the `band` / `meadow`
  densities. The grass blade count is printed in the Console when the valley loads.
* **No keyboard response:** Edit → Project Settings → Player → *Active Input Handling*
  = **Both**, then restart Unity.

## How it was checked

All C# was compiled against the Unity 2021.3 reference assemblies with the .NET
compiler: 0 errors, 0 warnings for both the game and editor assemblies. The Unity 6
code paths differ only by Unity 6's renamed physics APIs. The drift controller was
tuned in a 2D replica of the car physics, covering entry, holding, flicking sides,
lift-off exit, a hairpin and normal grip cornering for both AWD and RWD. The bridge
ramps were checked numerically (at most 10.6% grade, with the deck at least 2.4 m above
the water). The road layout was checked
numerically: every road is at most 10% grade, there are no self-crossings, the street
joins the main loop, and the 2 bridges sit at the river crossings. I could not run
Unity itself here, so the first real play test is yours. Please tell me anything you'd
like changed.
