# Player 3D visual trial

Local 2D checkpoint: `3e87502`; backup branch: `codex/backup-before-player3d-20261121`.
Trial branch: `codex/player3d-trial`. No remote push performed.

Open `Assets/Scenes/Player3DTrial.unity` and enter Play to generate the prototype.
The trial scene is a copy of Level03, not included in build settings. Original scenes and Player prefab are unchanged.
`Player3DTrialVisual` builds a simplified orange-beanie / green-vest / blue-backpack character from 3D primitives.
It uses transform joints and procedural pose blending for running and jumping, with basic attack / spray presentation.
The existing 2D controller, Animator events, hitboxes, Rigidbody2D, enemy sprites and environment remain in use.
Disable the component during Play to compare the original 2D presentation. Re-enable to return to 3D.

Verified: 24 mesh parts, supported shader and materials, original player sprites hidden, orthographic camera,
2D physics retained, run and airborne poses differ, facing mirrors correctly and visual scale remains fixed.

Limitations: this is a technical prototype, not a finished skinned character or a complete replacement animation library.
Roll, hurt, death, ledge and wall interactions still require dedicated 3D poses; full combat and three-level regression are not completed.
No assertion of final visual quality or production readiness.
