# Purgatory prison toilet puzzle — click-to-use prototype

This Unity script is on feature branch `feature/toilet-rising-water-urination-puzzle`
(PR #10). The Blender FBX and actual Unity scene/prefab still live in your
local project; they are not modified through GitHub.

## Scene hookup

1. Add **ToiletPuzzleInteractable** to your **Toilet** scene-root object.
2. Put a **non-trigger Collider** on the toilet or its porcelain child.
   Clicking the visible mesh through the center crosshair will activate it.
   The collider must be reachable by a raycast and not hidden behind a larger
   wall or room collider. A non-convex MeshCollider is fine on static porcelain.
3. Add a child Empty GameObject **WaterAnchor** and position it at the **center
   of the bowl's lowest water level**. Assign it to **Water Anchor**.
4. Leave **Water Surface** empty to generate a flat 12-sided water placeholder,
   or assign a properly positioned water mesh.
5. Optional: assign **Player Camera** (falls back to Camera.main),
   **Player Controller**, **Vial Prefab** and **Vial Rest Point**. Without a
   Vial Prefab, the script generates a temporary corked vial.
6. Optional: assign an **AudioSource** with clips for Pee, Water,
   Reveal and Pickup. For a real animation, assign **Use Animator** and
   its **Use Animation Trigger** (default `Use`). **Without an Animator**,
   the script still performs a small procedural camera dip.

## Playing

- During **Exploration**, **look at the toilet and left-click**.
- A short use animation begins **immediately**. At that same moment,
  the pale-yellow procedural stream starts pouring toward the water, with
  a little ripple where it lands.
- No separate Hold-P input and **no manual aiming minigame**. One click is
  one use, lasting **1.6 seconds** by default. You can click again as soon as
  it ends; there is no cooldown or required battle between uses.
- The toilet can be clicked **from anywhere it is visible**: unlike the
  collectible pickup, the toilet interaction has no nearby-distance limit.
  There must still be a direct camera raycast to the toilet collider.
- Each completed use raises the water one stage. After **four uses**, a
  corked vial floats up and bobs.
- Walk toward the vial, aim at it, and press **E** or **left-click**
  within 2.75 meters (default) to collect. The collectible appears as a
  **Sealed Vial** in the existing CollectiblesTable.
- The vial is one-time and persists via PlayerPrefs and ShopManager. Peeing
  remains available afterward, but the water level stops rising. To
  replay in the editor without clearing your progress, toggle
  **Ignore Saved Completion For Testing** on the toilet component.

## Useful Inspector values

| Property | Default | Notes |
| --- | --- | --- |
| Pee Duration | 1.6 s | Length of one automatic animation |
| Camera Dip | 0.035 m | Small first-person head movement |
| Camera Tilt | 5 degrees | Camera leans downward and returns |
| Use Animator | none | Optional real use animation |
| Use Animation Trigger | Use | Trigger parameter on Animator |
| Uses To Reveal | 4 | Full uses needed to reveal vial |
| Total Water Rise | 0.10 m | Tune for depth of your bowl |
| Interaction Distance | 2.75 m | Pickup only; no toilet-click range limit |
| Stream Width | 0.011 m | Width of stylized stream |

## Quick Play Mode checks

1. Walk near or look at toilet from across the prison cell.
2. Center the crosshair on the toilet and **left-click**.
3. Confirm the camera dips a little and the stream starts **immediately**,
   then ends after about 1.6 seconds. The water rises.
4. Click four times; confirm the corked vial surfaces.
5. Move close, click vial, and verify **Sealed Vial** in collectibles.
6. Verify clicks do nothing in card mode, radio, shop or settings.

## Prototype notes

- The stream is a simple automatically targeted curved **LineRenderer**,
  with a splash ripple. This is deliberately **not ballistic aiming**:
  users do not have to compensate for gravity.
- A real character-body / hand animation has **not** been authored in this
  code. An optional Animator trigger can play an existing animation, while
  the script adds a slight camera movement even when none is assigned.
- Accurate bowl placement matters. Set **WaterAnchor** inside the opening,
  and adjust **Total Water Rise** until the surface remains inside the toilet.
- Repo code has been structurally reviewed, but **Unity compilation and
  in-editor testing are not complete**. Leave the PR open until tested.
