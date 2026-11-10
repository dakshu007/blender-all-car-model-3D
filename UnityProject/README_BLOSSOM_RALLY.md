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
4. The first import also installs the **Input System** package (for gamepads). If Unity asks to
   **restart** or to "enable the new input backends", choose the option that keeps **Both** input
   systems (or click *Restart*). The setup script sets *Active Input Handling = Both* automatically.
5. Press **Play**. The valley builds itself in a few seconds, with a loading bar.
   Pick a car and drive.

> Tip: maximise the Game view (**Shift + Space**, or "Maximize on Play") and set its
> aspect to 16:9.

## Game flow

**Title screen** (a slow flyover of the valley, with music) → **Free Roam** or **Rally Stages & Drift
Challenge** → **Garage** (pick the car on the turntable, choose its colour and pattern) → drive.

## Controls

| Keyboard | Gamepad | Action |
| --- | --- | --- |
| W / ↑ | RT | Throttle |
| S / ↓ | LT | Brake, then reverse |
| A D / ← → | Left stick (analogue) | Steer |
| **Space + steer** | **A or B + steer** | Start a drift (see below) |
| R | Select / View | Reset to the road (in an event: back to the last checkpoint, +2 s) |
| C | Y | Camera: chase → cockpit → hood → cinematic |
| V | LB | Instant replay of the last 30 s |
| O | X | Photo mode |
| G | D-pad → | Weather |
| T | D-pad ← | Time of day |
| P | D-pad ↑ | Petals: calm → spring → blossom storm |
| F | | Colour grade on / off |
| 1 – 9 | | Switch car on the spot (free roam) |
| Tab | | Garage |
| **Esc** | **Start** | Pause menu |
| H | | Help overlay |
| \` or F1 | | FPS counter |

All keyboard bindings can be changed in **Settings → Keyboard controls**. Gamepads (Xbox,
PlayStation, MFi) work through the Input System package, which is installed automatically. Rumble
is used for the road surface, drifting, gear changes, impacts and checkpoints, and can be turned
off in Settings.

**In the garage:** ← → car · ↑ ↓ colour · Q / E pattern · X wash the car · Enter drive · Esc back.
Colours: Factory, Cherry Red, Jade, Imperial Gold, Ink Black, Porcelain, Sky Blue, Plum, Peach Blossom,
Tea Green. Patterns: Plain, Racing Stripes, Two-Tone, Roundels, Blossom Sprigs. Each car remembers
its own livery.

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

**Weather and time of day** (Esc menu, G and T) combine freely and are remembered between sessions.

| Weather | Look |
| --- | --- |
| Clear Spring | Light haze, few clouds, strong sun and crisp shadows |
| Spring Haze | The original soft pink fog with drifting mist (default) |
| Cloudy | Overcast grey-lilac sky, moving clouds, soft shadows, thicker fog, breezier wind |
| Rain | Rain streaks and splashes, soaked glossy roads with puddles and rain rings, tyre spray, wipers and drops on the windscreen |
| After Rain | Sun breaking through, roads still wet and reflective, puddles, fresh mist |
| Fog Off | No fog or mist, light clouds: see across the whole valley |

| Time of day | Look |
| --- | --- |
| Morning | Low warm sun from the east, long shadows |
| Spring Day | The default bright pastel day |
| Golden Sunset | Orange sun on the horizon, rosy fog |
| Blue Hour | Deep blue sky, lanterns and headlights glowing |
| Night | Moonlight, stars, glowing lanterns, headlights and brake lights |

Clouds cast drifting shadows across the valley. Changes fade over about 2.5 seconds.

## What's new in this version

**Driving (rebuilt from scratch).** Tyres grip up to a peak slide angle and then let go gradually.
Weight moves between the wheels under braking, throttle and cornering. Each car has its own engine
torque curve and a 5-speed gearbox (automatic, or manual: LeftShift/E up, LeftCtrl/Q down;
gamepad RB up, R3 down). Brakes are ABS-style, trail braking rotates the car, and the car levels
itself on jumps. Handbrake + steer still starts a drift, and power oversteer, Scandinavian flicks
and lift-off oversteer now come from the physics. **Settings → Driving & assists:** Arcade /
Balanced / Sim, traction control, ABS, stability help, automatic or manual gears.

**Graphics.** Sun shafts, reflective lake and river water with shoreline foam and ripples around
the car, valley mist at dawn and after rain, layered distant mountains, a cloud band with peaks
poking through, lantern light pools and glowing house windows at night, motion blur and speed
lines, dappled light under trees, colour grading per region and time of day, sun flare / lens dirt /
raindrops on the lens, and more surface detail up close. **Settings → Visual effects:** Enhanced
graphics (master switch), Ink & watercolour mode, Motion blur, Lens effects, Slow-motion moments,
Score pop-ups, Reduced camera motion.

**The valley.**
* **Village:** market stalls, lantern and firecracker spans over Lantern Street, stone lions at the
  gate, the bridges and the pagoda, a tea house on the lake, courtyard houses and moon-gate gardens,
  parked bicycles, rickshaws, carts and trucks.
* **Temple:** drum and bell towers that play when you drive past, plus incense burners.
* **Water:** water wheels on the river, terraced rice paddies, brooks with stepping stones.
* **Plants and rock:** willows, cliff pines, plum and peach blossom, rapeseed fields, karst pillars,
  light shafts in the bamboo forest.
* **Small life:** butterflies, dragonflies and fireflies.

**Rally atmosphere.**
* **Stages:** hay bales and tyre walls at tight corners, warning chevrons and 100/50 m boards,
  marshals waving flags, start ramps with start lights, and finish gantries with live timing boards.
* **People:** spectator areas whose crowds cheer and jump back, a service park, and a camera
  helicopter during events.
* **Valley life:** cranes, cats on rooftops, chickens, a water buffalo, farmers, monks, chess
  players, a morning tai chi group, dragon boats with drummers, fishermen and ducks.
* **Night:** fireworks over the lake, plus wind chimes and prayer flags.

**Your car.**
* **Crew:** an animated driver and co-driver who steer, shift, lean with the forces and read pace notes.
* **Damage:** dents, broken headlights, a cracked windscreen and a hanging bumper, plus a bent
  steering pull and power loss.
* **Effects:** kicked-up stones, flapping mud flaps, exhaust flames on backfire, glowing brake
  discs, headlight beams in fog.
* **Glass and details:** windscreen dirt cleared by the wipers, number plates and stickers, working
  gauges, a swinging lucky knot.
* **Repair:** in the garage, **X washes and repairs** the car.

**Game feel.**
* **Camera:** reacts to bumps, jumps, landings and impacts.
* **Slow motion:** a short beat on big air, perfect drifts and the finish line.
* **Pop-ups:** "PERFECT DRIFT!", "BIG AIR!", "NEAR MISS!", "CLEAN SECTION" and "SPEED TRAP".
* **Effects:** water splash sheets, lingering dust, petal blasts, richer rumble and sound layers.
* **Finish:** confetti and a medal stamp at the finish.

**Menus.**
* **Loading:** ink-painting loading screens.
* **Valley map:** an unrolling map scroll for choosing events (title → *Rally Stages*).
* **Garage:** a full workshop with mechanics, plus a **showroom walk-around (V)**.
* **Photo album:** in the title and pause menus.
* **Results:** a cinematic orbit camera on the results screen.

## Events

From the title screen choose **Rally Stages & Drift Challenge**, pick an event, pick a car, drive.

| Event | Road | Gold / Silver / Bronze |
| --- | --- | --- |
| Valley Loop | full main loop, asphalt, ~1.84 km | 61.2 / 70.4 / 82.7 s |
| Pagoda Climb | hairpins up the mountain, ~0.69 km | 51.3 / 59.0 / 69.2 s |
| Tea Terrace Sprint | gravel, ~0.62 km | 44.4 / 51.0 / 59.9 s |
| Bamboo Run | dirt, ~0.68 km | 37.7 / 43.4 / 50.9 s |
| Pagoda Drift Zone | drift points on the hairpins, ~55 s limit | 10,000 / 6,000 / 2,500 points |

* Stages start with a 3-2-1-GO countdown and have red lacquer checkpoint gates (the next one glows
  gold). They show split times against your best (green / red) and give a medal at the finish.
* **Ghost:** beat your time and a translucent ghost of that run races you next time (saved per stage
  and car).
* Reset (R) puts you back at the last checkpoint (+2 s). A "Wrong way" warning shows if you turn round.

## Replay and photo mode

* **Instant replay (V / LB):** the last 30 seconds, with trackside, chase, helicopter, bumper and drift
  cameras. ← → seek, ↑ ↓ speed (1×, ½×, ¼× slow motion), Space pause, C camera, Esc exit.
* **Photo mode (O / X):** freezes time. WASD / arrows fly, Q/E down/up, Shift faster, hold the right
  mouse button to look (or use both sticks), wheel or Z/X zoom, R/F roll.
  * Tab opens options: filters (Natural, Warm Spring, Film, Ink, Pastel Dream, Night Blue), exposure,
    saturation, vignette, depth of field (auto-focus on the car), letterbox and logo.
  * Enter / Space / A takes the picture. It is saved at double resolution to the game's
    `Screenshots` folder, and the path is shown on screen.

## Living valley

* **Birds:** flocks perch near the blossoms and burst into the air when you drive past.
* **Villagers:** at the paifang gate and along Lantern Street, they turn and wave at your car.
* **Sky:** kites fly over the meadows, and paper sky lanterns drift above the lake (much brighter at night).
* **Lake:** koi swim under the surface, with lily pads, lotus flowers and floating lanterns.
* **Waterfall:** where the river leaves the northern hills, with mist and spray.
* **Foam:** water foams against the bridge arches and the pavilion stilts.
* **Your car:**
  * cherry petals swirl up behind it in drifts, and settle on the bonnet, roof and boot when you're
    slow; they blow off at speed;
  * mud and dust build up from the sills (gravel, dirt and grass each leave their own colour), and rain
    slowly washes them off. Press X in the garage to wash the car.
* **Sound:** a procedural guzheng / erhu soundtrack that builds up as your drift combo grows (more
  layers at higher combos).
  * Engine: gear-change pops, crackles on lift-off, and a turbo whistle with blow-off on the Group B cars.
  * Road: gravel crunch, dirt rumble and grass swish.
  * Ambience: birds by day, crickets at night, wind and rain.

## Settings (title screen or Esc)

* **Graphics:** quality preset (Low / Medium / High / Ultra: shadows, anti-aliasing, cull distances,
  grass), grass density, resolution, fullscreen, V-Sync, FPS counter (also \` or F1).
* **Audio:** master, music and effects volume.
* **Controls:** gamepad rumble on/off, and remap every keyboard action.

## Building a Mac app

Menu **Blossom Rally → Build macOS App (Apple Silicon)** (or **Universal**) builds
`Builds/BlossomRally.app` and opens the folder. This needs "Mac Build Support" in Unity Hub; it is
installed by default with a Mac editor.

## Performance

* **Far trees:** each tree type is captured once into a texture at load time. Far trees then use
  4-triangle cut-outs (impostor LODs).
* **Grass:** thinned by the quality preset and the density setting, and fades out before its cull distance.
* **Reflections:** the car reflection probe refreshes every few seconds, time-sliced, on High / Ultra only.
* **Busy scenes:** valley life is merged into a few draw calls and updates less often far from the camera.
* **If the frame rate is low:** lower Quality in Settings first.

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
  Scripts/Audio/                  procedural music, sound effects and ambience
  Scripts/Events/                 rally stages, checkpoints, ghosts, drift challenge
  Scripts/Camera/                 instant replay and photo mode
  Scripts/World/                  birds, villagers, kites, lanterns, koi, lotus, waterfall
  Scripts/Gamepad/                gamepad + rumble bridge (Input System package, own assembly)
  Resources/Shaders/              vertex-colour wind, grass (+ BR_Interact.cginc car interaction),
                                  road (wet / puddles), car paint (livery / dirt), impostor, ghost,
                                  koi, foam, lantern glow, water, cloud + star sky, particles,
                                  glass, post FX (grade, filters, depth of field)
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

All C# compiles against the Unity 2021.3 reference assemblies with the .NET compiler: 0 errors and 0
warnings for the game and editor assemblies. It also compiles with the 2022+ defines and with only the
new Input System enabled. Unity 6 code paths differ only by Unity 6's renamed physics APIs. The
gamepad bridge was compiled against the Input System API surface.

The drift controller was tuned in a 2D replica of the car physics. The road, bridge, stage and
waterfall layouts were checked numerically. The music and sound generators were run offline: no clicks
at loop points, no NaNs, and balanced levels.

The driving physics was tuned on a test bench that runs the game's own driving code: 0–100 km/h in
3.5–6.9 s, 100–0 km/h in 29–32 m, and no spin at full keyboard lock at 120 km/h. Every build is also
checked against Unity's own 6.6 source for engine modules missing from the package list and for
APIs that newer Unity 6 releases turned into hard errors (that check caught two problems before they
reached you).

**Not yet tested in Unity:** the shaders can't be compiled here, and nothing has been played. If
anything renders pink or misbehaves, send me the Console message.
