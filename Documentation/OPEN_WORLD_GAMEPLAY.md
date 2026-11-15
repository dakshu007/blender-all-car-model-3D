# Blossom Rally 0.12: open-world gameplay

A cosy, non-violent open world in the spirit of GTA V, set in the Blossom world of six regions.

## Controls

| Action | Keyboard | Gamepad |
|---|---|---|
| Get out of / into a vehicle (below 12 km/h) | F | Y |
| Interact (shops, doors, job boards, bells...) | E | X (takes a photo when nothing is in range) |
| Walk / sprint / jump | WASD / Shift / Space | left stick / LB or L3 / A |
| Look around on foot | mouse | right stick |
| World map (set a waypoint by clicking) | M | D-pad down |
| Jobs nearby | J | pause menu → Jobs nearby |
| Cycle owned vehicles | [ / ] | |
| Colour grade toggle (moved from F) | K | |
| Car camera (moved from Y) | C | L3 |

## Features

### On foot
- Animated low-poly people with 7 outfits.
- Third-person walking with camera collision.
- The car you leave stays parked; walk back to it to get in.

### Money (¥) and saving
- Autosave, plus "Save game" in the pause menu.
- You start with ¥2,500 and the nine original rally cars.
- Ways to earn:
  - **rally prize money**;
  - **drift and near-miss cash**;
  - **70 jobs** across the six regions: tea delivery, taxi, courier, photo, snow supply runs and desert expeditions. Every job has GPS and a beacon.
- The pay bonus from tea and food is applied to job pay.

### Buying
- The **Jade Motors** showroom in the city sells the 12 new cars (about ¥8.5k–42.5k).
- The **Sea Breeze Scooters** shop on the coast sells the 6 bikes.
- Locked vehicles show their price in the garage.

### Places you can enter
- **Tea and food:**
  - Lakeside Tea House (valley);
  - Jade Leaf Tea Shop (city);
  - Lotus Seafood House (coast);
  - Crescent Spring oasis tea house (canyon).
- **Temples:** Blossom Temple and the Golden Monastery.
- Frost Summit Ski Lodge.
- The two vehicle shops.
- Things to do inside:
  - buy tea and food (job-pay buffs);
  - sit down;
  - use job boards;
  - ring the bell, light incense (shop discount), make a wish (luck) or climb to the viewpoint.

### Motorbikes
- The six bikes:
  - Lingyun SR (sport);
  - Mantis 750 (naked);
  - Peony 125 (scooter);
  - Dune Hare 450 (enduro);
  - Snow Leopard 1100 (adventure tourer);
  - Red Lantern 1800 (cruiser).
- Riding:
  - arcade lean into turns, wheelies and stoppies;
  - they never fall over;
  - a visible rider in the Ride pose;
  - each bike has its own engine sound.

### Living world
- People on foot in every region. They react to fast cars but are never hurt.
- AI traffic:
  - a city bus loop;
  - cyclists;
  - parked cars;
  - fishing boats.

### Graphics
- On High and Ultra:
  - ambient occlusion and better bloom;
  - aerial haze and filmic tone mapping;
  - cinematic depth of field;
  - a richer sky (moon, cirrus, Milky Way);
  - clear-coat car paint and glass reflections;
  - terrain micro-detail and water glitter.
- Low keeps the 200 FPS path.

## Check in Unity (first playtest)
- **Character:**
  - limb axes;
  - door and seat positions on tall vehicles;
  - mouse sensitivity.
- **Bikes:**
  - fork axis and lean look;
  - how the rider sits on each bike.
- **Places:**
  - the log should say `Places: 9/9 places ready`;
  - where the showroom and scooter shop end up;
  - doors on the right side of their buildings.
- **Graphics:**
  - `BR_PostFXPlus` supported on Metal (otherwise it falls back automatically);
  - AO and haze strength.
- **Traffic:**
  - bus turns;
  - frame time on Low.
