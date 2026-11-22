# Purgatory prison toilet puzzle — click-to-use prototype

The toilet puzzle is on `main` after PR #10. This debugging improvement
is proposed separately in PR #12. The Blender FBX and actual Unity scene/prefab still live in your
local project; they are not modified through GitHub.

## Scene hookup

1. Add **ToiletPuzzleInteractable** to your **Toilet** scene-root object.
2. Keep **Auto Add Click Colliders** enabled on the toilet script.
   It adds non-trigger MeshColliders at runtime to imported toilet mesh parts
   that do not have a collider. Existing colliders remain untouched.
   An opaque room/wall collider in front of the toilet will still block clicks.
   If your model already has appropriate colliders, you can turn this off.
   Enable **Debug Clicks** to log the hit collider, hierarchy path, or blocked
   interaction reason in Play Mode. A one-time **[ToiletPuzzle] Started** log
   appears even with debugging disabled. If it is missing, check the component
   is attached and enabled on the toilet in the currently loaded scene.
3. **Toilet Model Root** is optional. If the toilet FBX's Torus/Cylinder/Cube
   meshes are NOT children of the GameObject with ToiletPuzzleInteractable,
   drag the parent of those meshes into this field, so the click test knows
   that those meshes are part of the toilet. Otherwise leave it empty.
4. Add a child Empty GameObject **WaterAnchor** and position it at the **center
   of the bowl's lowest water level**. Assign it to **Water Anchor**.
5. Leave **Water Surface** empty to generate a flat 12-sided water placeholder,
   or assign a properly positioned water mesh.
6. Optional: assign **Player Camera** (falls back to Camera.main),
   **Player Controller**, **Vial Prefab** and **Vial Rest Point**. Without a
   Vial Prefab, the script generates a temporary corked vial.
7. Optional: assign an **AudioSource** with clips for Pee, Water,
   Reveal and Pickup. For a real animation, assign **Use Animator** and
   its **Use Animation Trigger** (default `Use`). **Without an Animator**,
   the script still performs a small procedural camera dip.

## Playing

- During **Exploration**, **look at the toilet and left-click**.
  Just like the radio: with a locked cursor, click the center crosshair;
  with an unlocked cursor, click the toilet mesh under the mouse pointer.
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
   The imported model needs mesh renderers (and the script's automatic collider
   setup enabled) so the click ray has a solid surface to detect.
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
