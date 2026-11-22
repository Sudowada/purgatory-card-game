# Purgatory — toilet mini-game (majority hit, close-range aim assist, animated stream)

Implementation: `Assets/Scripts/ToiletPuzzleInteractable.cs`.
Keep the existing Toilet GameObject, imported Blender meshes, WaterAnchor,
FirstPersonController and collectibles. The streamed art is generated in code.

## Unity Inspector setup

1. Select the scene's **Toilet** GameObject and confirm that it has
   **ToiletPuzzleInteractable** enabled.
2. **Player Camera** = `FPCamera`, **Player Controller** = your Capsule/player.
   **Toilet Model Root** = the parent of the actual FBX porcelain mesh parts,
   normally `Toilet`.
3. Leave **Interaction Collider = None** for initial testing. Keep
   **Auto Add Click Colliders** on, which repairs/imports non-trigger colliders
   on the porcelain meshes, and **Debug Clicks** on. If preferred, assign
   one valid non-trigger collider to Interaction Collider as with ShopWindow.
4. **Water Anchor** must be a transform placed **at the middle of the bowl's
   starting water height**, below the rim. You may leave **Water Surface**,
   **Water Material**, and **Vial Prefab** empty for procedural placeholders.
5. Optional: assign **Use Animator** (the `Use` trigger) and the audio clips.
   Without them, a small camera dip animates while peeing.

## Game flow

- In **Exploration**, put the fixed center crosshair over the toilet and
  **left-click**. The use animation starts at once.
- After **0.4 seconds** (`Pee Start Delay`), the visible pee stream and
  optional audio begin; mouse look stays active, but walking is paused.
- **When close and looking down into the toilet**, a gentle assist guides
  the stream almost straight into the bowl. It's active only when close,
  angled downward, pointed toward the water, and no wall is in the way.
  Aim away or stand farther back and the normal gravity-curved shot returns.
- On every frame while the **stream is flowing**, hitting any part of the
  toilet porcelain **or** the bowl water adds to successful contact time.
  Peeing onto a wall, floor, or empty space does not count.
- At the end of the use, success requires at least **60% of the stream's
  active duration on the toilet**. The 0.4-second lead-in does not count
  against the player. A successful use raises the water one stage; a failed
  use does not. The Console logs the exact percentage if Debug Clicks is on.
- Four successful uses reveal the floating sealed vial. Get close and click
  or press E with the crosshair over the vial to collect it.
- The stream is a layered, pulsing, mildly irregular low-poly ribbon with
  a brighter interior, moving chunky droplets, splash flecks on porcelain,
  and a ripple when it hits the water. It is a stylized game effect,
  **not a physical fluid simulation or Cloverpit asset**.

## Tunable fields

| Setting | Starting value | Meaning |
| --- | --- | --- |
| Pee Duration | 2.5 seconds | Total use animation |
| Pee Start Delay | 0.4 seconds | Delay before stream and pee sound |
| Required Toilet Hit Fraction | **0.60** | 60% or more active contact = success |
| Close Aim Distance | **1.9 m** | Max camera-to-bowl distance for assisted shot |
| Close Aim Downward | **0.30** | Camera must point sufficiently downward |
| Close Aim Alignment | **0.77** | View direction must point toward bowl |
| Stream Launch Speed | 9.5 m/s | Long-range shot velocity |
| Stream Gravity | 9 m/s² | Long-range stream curvature |
| Stream Flight Time | 1.6 seconds | Maximum displayed ballistic arc |
| Droplet Flow Speed | **3.2** | Rate at which pooled pixel droplets travel |
| Stream Wobble | **0.004 m** | How lively the line looks |
| Stream Width | 0.011 m | Line and droplet sizing |
| Uses To Reveal | 4 | Successful uses to float the vial |
| Total Water Rise | 0.10 m | Final water rise, keep inside rim |

## Test checklist

1. Clear Unity Console, enter Play Mode in Exploration. Click the toilet with
   the center crosshair. Expect `[ToiletClick] Toilet hit ...`.
2. The camera dips immediately; the stream begins ~0.4 seconds later.
   Confirm the liquid is visibly flowing, with small dots/droplets moving
   down the line and occasional flecks at its landing point.
3. Stand close and look down toward the water. The pee should run almost
   directly into the toilet, with a water ripple and **IN THE BOWL** on-screen.
4. Turn away while still peeing: the stream should follow your mouse and
   should no longer auto-target the toilet.
5. Aim on the porcelain for most of the active 2.1 seconds. The HUD should
   show `HITTING TOILET` and percentage rising; at the end the Console should
   show `Pee complete: toilet contact ... required 60% — SUCCESS`.
   The water level increases by one stage.
6. Aim at a wall/floor for most of the duration; expected **MISS** and no
   additional water stage.
7. Repeat for 4 successful sessions; verify the corked vial appears and
   can be collected with E or left-click nearby.
8. Enter CardGame/settings/shop or open radio: toilet use should be blocked.
   When pee finishes, camera look and movement should resume as normal.

## Troubleshooting

- **Toilet click sometimes hits Wall_Right**: check the wall's oversized
  Collider in Scene view. The first raycast hit must belong to the toilet.
  Keep Interaction Collider = None to use the porcelain root/children fallback.
- **`GameManager.IsInputBlocked`**: newer toilet code allows environmental
  interaction during Exploration despite the card-game input lock. If you
  still see that old toilet error, pull latest main and recompile.
- **No water material**: not required; an unlit 12-sided placeholder is
  created from WaterAnchor.
- **No contact despite good aim**: first check that Toilet Model Root is
  the FBX parent and the water anchor is inside the bowl. For close shots,
  increase Close Aim Distance slightly or lower Close Aim Alignment.
  A wall collider between camera and bowl will intentionally block assist.
- **Water still doesn't rise**: inspect the `Pee complete` percentage.
  You need at least 60% of *stream-on* time landing on toilet or water,
  not 60% of total animation (the pre-stream delay is excluded).
- **Stream appears oversized/too noisy**: reduce Stream Width, Stream Wobble,
  or Droplet Flow Speed. Particles are reused and set to Ignore Raycast.
- **Testing**: source-level checks have been run; Unity compilation and
  in-scene Play Mode still must be verified locally.
