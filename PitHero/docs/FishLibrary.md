# Fish Library

Everything a Fishing Pond can yield: ten fish, smallest to largest, and three kinds of junk. Monsters
with the Fishing job catch them (see "How fishing works" below). The table lives in
`PitHero/Config/FishingConfig.cs`.

## Fish and prices

Gold grows by the same factor from one fish to the next: 50 G × 20^(k/9) for k = 0..9 (about ×1.39
per step), rounded to 5 G. The smallest is worth 50 G and the largest 1000 G. A fish is sold the moment
it lands (gold source `fish_catch`); junk is worth nothing.

| # | Catch | Gold | Marbles | Sprite (px) | Look |
|---|---|---|---|---|---|
| 1 | Minnow | 50 | 40 | 13 × 12 | Slender silver-gray, thin dark side stripe |
| 2 | Bluegill | 70 | 32 | 17 × 15 | Round olive and blue-purple body, orange belly |
| 3 | Perch | 100 | 25 | 18 × 19 | Golden yellow with dark bars, orange lower fins |
| 4 | Rainbow Trout | 135 | 20 | 22 × 22 | Silvery olive with a pink side stripe and speckles |
| 5 | Largemouth Bass | 190 | 15 | 24 × 24 | Olive green, dark side stripe, open mouth |
| 6 | Golden Koi | 265 | 11 | 25 × 27 | Bright orange-gold, whiskers |
| 7 | Channel Catfish | 370 | 8 | 30 × 31 | Slate blue-gray, pale belly, long whiskers |
| 8 | Northern Pike | 515 | 5 | 30 × 32 | Long olive body with pale spots, duck-bill snout |
| 9 | Lake Sturgeon | 720 | 3 | 45 × 42 | Steel gray with bony plates, shovel snout |
| 10 | Alligator Gar | 1000 | 2 | 52 × 55 | Armored olive body, long toothy snout |
| – | Old Can | 0 | 13 | 18 × 20 | Rusty dented can, torn label |
| – | Old Bottle | 0 | 13 | 22 × 22 | Green glass bottle, cork stopper |
| – | Old Boot | 0 | 13 | 20 × 18 | Worn brown leather boot |
| | **Total** | | **200** | | 161 fish, 39 junk (19.5%) |

**Marble bag.** Every catch draws one marble from a 200-marble shuffle bag (`FishingService`, one bag
shared by all fishers), without replacement, so every 200 catches contain exactly the composition
above: two Alligator Gars, three Lake Sturgeons, 39 pieces of junk. The bag refills when empty and is
never saved (shuffle-bag policy, `docs/ShuffleBagSystem.md`): a load or a replay seek starts a fresh bag.

**Value.** One full bag is worth 24,900 G, so 124.5 G per catch on average. A fisher waits a random
30–60 sim seconds per cast (rolled each cast, 45 on average) plus the 1.5 s reel, so about 77 catches per
real hour at 1x (one in-game hour is 60 sim seconds): one fisher earns about 160 G per in-game hour and
a full crew of four about 38,500 G per real hour at 1x — well below the
Digging Field's DigBot. Tune with `GameConfig.FishingCastSecondsMin/Max` (changing them changes income:
bump `SimulationVersion`) or the marble counts.

## How fishing works

- **Who:** monsters with the Fishing job, at most `GameConfig.FishingMaxWorkersPerShift` = 4 day-shift and
  4 night-shift fishers, and only while a Fishing Pond is placed (the Fishing Pole artifact unlocks the
  pond). Fishing runs around the clock; the day and night crews swap at 6 AM / 10 PM like farmers. The
  Monsters window greys the Fishing button out ("Requires a Fishing Pond", "Fishing crew full") and the
  `SetMonsterJob` handler enforces the same rule (`FishingJobRules`). With job automation on,
  `FishingJobDemandEvaluator` staffs one fisher per reachable spot as the lowest-priority job.
- **Where:** four spots, one per edge of the pond (left, bottom, right, top), each on the pond's centre
  line just outside the water (`Util/FishingPondSpots`). A blocked or off-map spot is skipped. Monsters
  only have left/right art, so the top and bottom fishers face right and cast diagonally. The top fisher sits down on the top rocks (inside the walled footprint; it steps back to its stand tile when it gets up). Each spot casts to a fixed point picked from the pond art in open water, clear of the rock ring and lily pads (`FishingPondSpots.GetBobberPosition`); redo those points if the pond art changes.
- **Work loop:** `FishingTaskCoordinator` spawns fishers from the monster house, `FishingMonsterStateMachine`
  claims a spot, walks there, sits (animation paused) and casts. Each cast waits a random 30–60 sim
  seconds, then the catch is drawn, paid and the reel plays for `FishingReelSeconds` (1.5 s): the catch
  is pulled out of the water with a `Splash` particle burst and the watering sound, then arcs to the
  fisher's hands (`FishCatchEffectComponent`, cosmetic) where its "Name (+N G)" label shows, with the
  pay-gold sound. Each catch counts one Fishing task toward the monster's Fishing level.
- **Pole and line:** `FishingLineRenderer` draws them with Nez `DrawLine` (a 2 px brown pole, a light
  sagging line, a red-and-white bobber that bobs on the sim tick). In replays they are recorded as
  `Line` frame ops (`ReplayFrameFormatVersion` 16).
- **Moving the pond** re-seats the fishers at the new spots.

## Art

- Fish source: `Art/PitHero/Fish.aseprite` (outside this repo), 64 × 64 canvas, one frame per fish in the
  order above, each frame tagged with the fish's name. Junk source: `Art/PitHero/FishingJunk.aseprite`,
  32 × 32, tags `JunkCan`, `JunkBottle`, `JunkShoe`.
- Game textures: `Content/Textures/Actors/Fish/<Name>.png` and `Content/Textures/Actors/FishingJunk/<Name>.png`,
  trimmed to the item, packed into the Actors atlas (the packer also makes `Fish` and `FishingJunk`
  animations from the folders; the code only uses the individual sprites). All fish face right in a
  leaping "caught" pose; a catch flying back to a fisher on its left is flipped.
- Generated with Retro Diffusion `rd_pro__default` (two takes each). Fish: seed 4127 (5311 for the
  catfish), the Largemouth Bass as the style reference for the other nine. Junk: seed 7741 with no
  reference (with the bass as a reference the bottle and boot came out as fish).
- Every pixel is DB32. Most items were snapped to the nearest DB32 color; the Channel Catfish and Lake
  Sturgeon were snapped within a gray-blue ramp (`000000 222034 323c39 306082 696a6a 9badb7 cbdbfc
  ffffff` and `000000 222034 323c39 595652 696a6a 9badb7 cbdbfc`), because the plain snap turned them
  purple and peach. Cream bellies become peach (`eec39a`), the closest warm light in DB32.
