# Prison toilet puzzle — Unity hookup (prototype)

This script intentionally does not change the Blender FBX or Unity scene/prefab:
those remain local to the project. The script is ready to attach after pulling
the feature branch. Asset art, sound clips, and the vial's final puzzle clue
can be changed later.

## Scene setup (about five minutes)

1. Select your imported **Toilet** GameObject in the prison cell. Keep it at
   the size/position you were using in the screenshot (currently around 0.4
   scale). Add **ToiletPuzzleInteractable** to that top-level GameObject.
2. Ensure the bowl has a **non-trigger collider** that the player's view ray
   can hit. A MeshCollider on the porcelain model is easiest for a static
   low-poly prop. You can also make a BoxCollider for the toilet body.
   If you put a collider on a different, non-toilet parent, the script will
   not recognize it — keep the collider on the Toilet or a child.
3. Right-click Toilet in the Hierarchy -> Create Empty. Name it
   **WaterAnchor**. Move it into the **interior of the bowl**, about where
   the initial water surface should be (well below the rim, centered over the
   drainage hole). Assign it to the **Water Anchor** field of the script.
4. Leave **Water Surface** and **Vial Prefab** empty for now. At runtime,
   the script will create a 12-sided flat water mesh and a corked-vial placeholder.
   If the default water disk clips through the porcelain, adjust WaterAnchor
   slightly or replace it with a manually shaped oval water mesh.
5. Optionally assign **Player Camera** and **Player Controller**. If empty,
   the script finds Camera.main and the first FirstPersonController.
6. Optional: add an **AudioSource** and assign Pee Clip, Water Sound,
   Reveal Clip, and Pickup Clip. These are intentionally empty by default:
   the visible urine arc and water animation still work without any audio.
7. Optional: create **VialRestPoint** (another empty GameObject near the
   center/top of the bowl). Place it a little above the *final* water height
   and assign it to **Vial Rest Point** to control where the collectible floats.

## Controls and puzzle progression

- In **Exploration**, point the center of the screen at the toilet. Press
  **E** to use it. Each use plays a roughly **1.6-second** visible arc from
  the first-person camera into the toilet, with an expanding splash ripple.
  There is no cooldown or battle requirement; you can use it again as soon
  as the previous animation ends.
- **Use 1:** water rises slightly.
- **Use 2:** water rises again; a gurgling message appears.
- **Use 3:** water rises; a rattling message appears.
- **Use 4:** water reaches its final height; a corked vial rises and bobs.
- Aim at the vial, then **Left Click** or press **E** to collect it.
  The existing CollectiblesTable displays a new **Sealed Vial** collectible.
- You can still use the toilet after the reveal; water caps at maximum height
  and the vial does not respawn after pickup.
- Vial pickup persists through PlayerPrefs, independently of the current
  scene. The clue itself is intentionally left open for a future puzzle chain.

## Key inspector settings

- **Interaction Distance:** 2.75 meters
- **Pee Duration:** 1.6 seconds
- **Total Water Rise:** 0.10 meters (adjust to keep water below the rim)
- **Uses To Reveal:** 4 (set to 1 temporarily for fast visual testing)
- **Stream Width:** 0.011 meters; generated stylized pale-yellow line
- **Water Material / Stream Material:** optional; defaults are placeholders
- **Ignore Saved Completion For Testing:** allows repeated vial playtests without deleting saved collectibles
- **On Vial Collected:** UnityEvent hook for future puzzles. No listener is
  required to pick up the reward.

## Notes and limits

- The stream is **a stylized procedurally animated LineRenderer with a
  splash ripple**, not a simulated liquid mesh or character-body animation.
  It moves with the player camera while the use animation plays, landing in
  the water. It is the first visual prototype; we can upgrade to a
  particle stream or hand animation after the art-style pass.
- The auto-water mesh is intentionally basic. Unity 6 URP unlit + sprite
  shader fallbacks are used for temporary materials.
- Leave toilet water and vial colliders disabled/independent of the body
  so the body can still be targeted for pressing E.
- If E shows no prompt, confirm GameState=Exploration, cursor is
  locked, PlayerController is enabled, radio/shop/settings are closed,
  the toilet has a collider, and WaterAnchor is assigned.
- Scripts were source-reviewed, but **Unity compilation, prefab hookup,
  interaction, and play-mode visuals have not been tested yet**.
