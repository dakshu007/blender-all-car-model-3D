# Art direction: stylised rally cars

## Reference study (`ArtPipeline/References/`, local only)

Eight screenshots from *Art of Rally* were supplied as **visual inspiration only**.
They are not committed to this public repository. Nothing was extracted, traced or
recreated from them. They group as follows:

| Group | What it shows | Shape language |
| --- | --- | --- |
| 1960s–70s light coupes (2) | Rear-engine fastback coupe, compact sports coupe | Rounded fastback, round headlamps, low roof, narrow track |
| 1970s boxy Group 2/4 saloons (4) | Two- and four-door saloons | Flat sides with a crisp shoulder, upright glasshouse, chrome or black bumpers, quad round aux lamps |
| 1980s (2) | Boxy saloon with boxed arches, mid-engine wedge with a big wing | Box flares, wedge nose, rectangular lamps, rear wing |

### Shared stylistic traits (the rules for every car)

1. **Silhouette first.** Each car must be recognisable as a flat grey shape. Exaggerate
   the wheel size slightly and keep the stance planted.
2. **Chunky, chamfered planes.** Large flat panels joined by small (10–15 mm) chamfers
   and one clear shoulder crease. Avoid soft, inflated toy curvature.
3. **The glasshouse is its own volume.** It tapers inward and sits on the body. Glass is
   dark and only slightly see-through. Pillars are painted or black, with black
   window rubbers.
4. **Matte paint.** Roughness about 0.4–0.5, no metallic flake, no clear-coat gloss.
5. **Few, bold details.** Round aux lamps, mud flaps, mirrors, a wing or vent where
   the class suits it. No tiny bolts.
6. **Wheels are a highlight.** Simple dish or spoke rims in gold, white or silver,
   chunky tyre sidewalls, and gravel tread blocks you can read.
7. **Graphic liveries.** Two or three bold colours on a light or primary base, in
   stripes, bands, chevrons or blocks, plus a number roundel. Liveries must be original.
8. **Soft, warm light** in presentation. Assets must still read under neutral light.

## Shared palette and material library

| Material | Use | Value |
| --- | --- | --- |
| `M_<CAR>_Paint` | Body, flares, bumper, wing (livery atlas) | roughness 0.42, metallic 0 |
| `M_Trim_Black` | Rubbers, bumpers, grille, wells | `#1E1D1F`, rough 0.55 |
| `M_Trim_Grey` | Grille slats, steering | `#4A4744` |
| `M_Chrome` | Lamp rings, mirror glass, exhaust | metallic 1, rough 0.22 |
| `M_Glass_Tint` | Glass | `#0B1013`, alpha 0.86, rough 0.05 |
| `M_Tire_Rubber` | Tyres | `#232120`, rough 0.92 |
| `M_<CAR>_Rim` | Rims | per car (CAR-009 gold `#C9A04A`, metallic 0.85) |
| `M_Brake_Disc` | Discs | `#6A6764`, metallic 0.8 |
| `M_Light_Head` / `_Aux` / `_Tail` / `_Indicator` | Lamps | emissive, low strength |
| `M_Interior_Dark`, `M_<CAR>_Seat`, `M_<CAR>_Cage` | Interior | matte |
| `M_<CAR>_Accent` | Mud flaps, tow hooks | per car |

The shared materials keep the same name and values on every car. Only `Paint`,
`Rim`, `Seat`, `Cage` and `Accent` change per car.

## Consistency rules for the collection

* Same studio (`render_previews.py`): camera rig, lights, ground and AgX look.
* Wheel Ø 0.60–0.68 m. Tyre top at the wheel radius exactly (validator: tyres touch z = 0).
* Bevel width 10–15 mm, smooth-by-angle 32°.
* 15k–35k triangles for a player car (CAR-009: 22,702).
* One 2K livery atlas per car, laid out by the car's spec module.
* No real manufacturer names, badges, sponsor marks or game branding.
