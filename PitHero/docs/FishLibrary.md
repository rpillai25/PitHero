# Fish Library

The ten fish a Fishing Pond can yield, smallest to largest. Fishing itself (the monster job, catch
odds, timing) is not implemented yet; this is the catalog and art the later phases build on.

## Fish and prices

Gold grows by the same factor from one fish to the next: 50 G × 20^(k/9) for k = 0..9 (about ×1.39
per step), rounded to 5 G. The smallest is worth 50 G and the largest 1000 G.

| # | Fish | Gold | Sprite (px) | Look |
|---|---|---|---|---|
| 1 | Minnow | 50 | 13 × 12 | Slender silver-gray, thin dark side stripe |
| 2 | Bluegill | 70 | 17 × 15 | Round olive and blue-purple body, orange belly |
| 3 | Perch | 100 | 18 × 19 | Golden yellow with dark bars, orange lower fins |
| 4 | Rainbow Trout | 135 | 22 × 22 | Silvery olive with a pink side stripe and speckles |
| 5 | Largemouth Bass | 190 | 24 × 24 | Olive green, dark side stripe, open mouth |
| 6 | Golden Koi | 265 | 25 × 27 | Bright orange-gold, whiskers |
| 7 | Channel Catfish | 370 | 30 × 31 | Slate blue-gray, pale belly, long whiskers |
| 8 | Northern Pike | 515 | 30 × 32 | Long olive body with pale spots, duck-bill snout |
| 9 | Lake Sturgeon | 720 | 45 × 42 | Steel gray with bony plates, shovel snout |
| 10 | Alligator Gar | 1000 | 52 × 55 | Armored olive body, long toothy snout |

## Art

- Source: `Art/PitHero/Fish.aseprite` (outside this repo), 64 × 64 canvas, one frame per fish in the
  order above, each frame tagged with the fish's name.
- Game textures: `Content/Textures/Actors/Fish/<Name>.png`, one per fish, trimmed to the fish. All
  face right, in a leaping "caught" pose. They are not in the Actors atlas yet: repack Actors (and bump
  `ReplayFrameFormatVersion`, since its rects change) when the first code uses them. The packer turns
  the `Fish` subfolder into an animation named `Fish`; the individual sprites keep their file names.
- Generated with Retro Diffusion `rd_pro__default` (two takes per fish, seed 4127, 5311 for the catfish),
  the Largemouth Bass as the style reference for the other nine.
- Every pixel is DB32. Most fish were snapped to the nearest DB32 color; the Channel Catfish and Lake
  Sturgeon were snapped within a gray-blue ramp (`000000 222034 323c39 306082 696a6a 9badb7 cbdbfc
  ffffff` and `000000 222034 323c39 595652 696a6a 9badb7 cbdbfc`), because the plain snap turned them
  purple and peach. Cream bellies become peach (`eec39a`), the closest warm light in DB32.
