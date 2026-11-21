# Purgatory toilet puzzle — free-aim urination prototype

The toilet mini-game now uses **mouse aim and actual trajectory hits** rather
than sending an animation automatically into the bowl. The Blender model, Unity
scene and FBX remain local; this GitHub change contains the script and UI code.
Unity Play Mode testing is still required.

## Put the script on the Toilet prefab

1. Pull/open branch `feature/toilet-rising-water-urination-puzzle` (or merge
   PR #10 and pull `main` when ready).
2. Select the imported **Toilet** scene root and add the component
   **ToiletPuzzleInteractable**. Ensure its porcelain bowl has an ordinary
   **non-trigger collider** (a static MeshCollider or hand-sized colliders).
   Collision matters: the urine stream should hit a wall or rim before the water.
   The root's scale may remain at the current Unity preview size (~0.4).
3. Make an empty **WaterAnchor** child placed at the **center of the bowl's
   starting water level**, below the rim. Assign it in the component.
   Leave **Water Surface** empty to create a 12-sided water plane at runtime,
   or assign your own mesh instead. The default water plane has no collider:
   water impacts are computed analytically.
4. Leave **Vial Prefab** empty for the temporary small corked-vial model, or
   assign your custom prefab with a collider. If you like, add a child
   **VialRestPoint** at the final surface position and assign it to control
   where the vial bobs.
5. Player Camera and FirstPersonController are found automatically if not
   assigned. Optional AudioSource + clips: Pee Clip, Water Sound,
   Reveal Clip, Pickup Clip. Everything still works silently with no clips.

## Play and aim

- Enter **Exploration** with the mouse locked for first-person look.
- **Hold P to pee.** The stream originates just below/right of the camera,
  points in the camera's look direction, and follows a **ballistic arc**.
  Move the **mouse** to aim it. From farther away, **aim higher** to compensate
  for gravity. The arc never automatically homes in on the toilet.
- There is **no toilet interaction-distance restriction** for peeing. You can
  attempt a shot from anywhere in the room. Actual scoring still requires a
  clear physical trajectory into the bowl: walls, furniture and porcelain
  block it. The toilet itself must be within reach of the arc.
- The floating prompt shows **IN THE BOWL** when your stream hits the water;
  otherwise it reads **MISS**. It shows how much of the current fill stage
  you have completed.
- Every **1.2 seconds of successfully hitting the bowl** earns one fill stage.
  Misses and time spent peeing elsewhere **do not raise the water**.
  Progress within a fill stage is retained if you release P and try again.
  You can hold continuously to earn several stages or stop and restart
  immediately—no cooldown or encounter gate.
- **Four completed fill stages** raise the water to its final level. A
  placeholder corked vial floats up and bobs at the surface.
- Approach it and aim the reticle at the vial, then **E or Left Click**
  (within 2.75 m by default) to pick it up. The existing CollectiblesTable
  records the **Sealed Vial**. You can keep peeing afterward; the puzzle
  doesn't award more vials.
- Vial pickup is saved across runs by PlayerPrefs and ShopManager. For repeated
  editor testing, toggle **Ignore Saved Completion For Testing** on the Toilet
  script. Its actual clue/lore payload is deliberately undecided;
  `onVialCollected` is available for the next puzzle-chain step.

## Tuning inspector settings

| Property | Default | What it controls |
| --- | --- | --- |
| Stream Launch Speed | 9.5 m/s | Range / how flat the arc looks |
| Stream Gravity | 13 m/s² | More drop at distance; player must aim higher |
| Stream Flight Time | 2.0 s | Max visible trajectory length |
| Bowl Hit Radius X | 0.13 m | Side-to-side scoring radius |
| Bowl Hit Radius Z | 0.16 m | Front-to-back scoring radius |
| Seconds Per Fill Stage | 1.2 s | How long the player must aim accurately |
| Uses To Reveal | 4 | Stages required for the vial |
| Total Water Rise | 0.10 m | Final water height; must stay below rim |
| Stream Width | 0.011 m | Width of procedural LineRenderer |
| Interaction Distance | 2.75 m | Vial pickup only, NOT pee reach |

## First Play Mode check

1. Start near the toilet. Hold P and observe the pale-yellow moving stream.
2. Aim up/down with the mouse. It should naturally arc and land higher/lower.
3. Hit the inside of the bowl for ~1.2 seconds; confirm first water rise.
4. Aim away deliberately for 3 seconds; **water must NOT rise**.
5. Stand farther back and aim slightly ABOVE the toilet; confirm you can still
   land the arc in the bowl.
6. Repeat until all four stages are complete; confirm a vial surfaces.
7. Release P, approach, click the vial and confirm pickup and collectible.
8. Go to CardGame or open settings, radio or shop; no urine interaction should
   be available during those modes.

## Notes / expected prototype limits

- The stream is a stylized **LineRenderer arc with a ripple on water contact**,
  not a fluid simulation, animated character model or custom shader. The
  gameplay trajectory is the same path used to display the stream.
- The bowl region is an elliptical target on the plane of Water Surface. The
  `Bowl Hit Radius X/Z` settings should be **smaller than the visible porcelain
  opening** and may need changing for the imported model's scale.
- When firing, the script raycasts along the arc and stops at nearby non-trigger
  colliders. This makes proper collision meshes important. If all shots read
  MISS even with correct aim, check that there is no solid collider covering
  the whole bowl opening and that WaterAnchor is above the bowl's solid bottom.
- Stream launch is unrestricted by proximity; realistic range is set by speed,
  gravity and obstacles. There is no scripted distance-based auto-hit.
- The first-person player's camera stays free to move with the existing
  mouse-look system while holding P.
- **GitHub source and static checks only:** Unity compilation, actual prefab
  setup and gameplay visuals still need testing inside the project.
