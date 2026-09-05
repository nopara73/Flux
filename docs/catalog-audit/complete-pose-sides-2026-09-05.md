# Complete-pose side audit

Tandem Walk (32) keeps the same crossed-arm order while the legs alternate.
Revision 71 already made it two-sided, alongside Standing Diagonal Head Turns
(483) and Diagonal Finger Tracking (493). This follow-up checks the same blind
spot beyond the reported exercise: side timing must follow the complete pose,
not just the primary trained limb.

## Scope and corrections

Screened all 354 revision-71 records labeled `Continuous` or `Alternating` from
the packaged videos, then examined 26 candidate clips in greater detail.
Revision 72 corrects these eleven records:

| IDs | Demonstrated asymmetry | Correction |
| --- | --- | --- |
| 307 | One hand presses the forehead throughout neck flexion. | Repeat with the other hand; the bilateral neck-extension member 310 remains single. |
| 490, 491, 492, 495, 499, 501 | The target thumb/finger stays on the same arm. | Give each distinct exercise both arm positions. The linked 491/501 sequence becomes four blocks. |
| 520 | The mirror is held with one hand above and one below. | Mirror the hold for the second block. |
| 528 | Abdominals-and-thighs posing retains one lead leg. | Shown and opposite lead-stance blocks. |
| 958 | The side-bend clip only bends toward screen-left. | Two mirrored blocks; correct the misleading name to Standing Overhead Side Bend. |
| 561 | The tiptoe-running head-spot clip rotates only clockwise. | Clockwise and counterclockwise blocks using the existing direction-segment mechanism and an exact horizontal mirror. |

No movement was invented, no original demonstration was replaced, and no
anatomy, demand, compatibility, identity, or quota rule was changed. The new
direction asset is generated from the existing human clip at normal speed.

## Cases deliberately left unchanged

Interlaced fingers alone do not make a bilateral exercise one-sided: 216, 237,
238, 255, 310, and 740 keep their existing timing. The self-hug (398) already
exchanges upper/lower arm roles, and tutting (522) does likewise. Ballet calf
raises (562) use parallel feet rather than a fixed crossed lead stance. Other
already-alternating or bilateral clips remain unchanged.

## Persistence and verification

Android and web use the same generated catalog. Their revision-72 migration
invalidates only affected saved placements/progress, preserving scores, phase
feedback, valid Keeps, and completed workout history. Regression tests pin the
complete side sequences, linked members, direction coverage, negative cases,
and feedback-preserving migration.

The full Android suite (712 tests), web suite (298 tests), production web build,
and Debug Android build pass. All enforceable catalog deficit counts remain
zero. Catalog and direction-media regeneration reproduce byte-for-byte.

Packaged playback is checked in actual synthetic 60-minute workouts, including
Tandem Walk, every corrected member, both sides/directions, the neck hold
frames, preparation, intermediate rests, subsequent sets, and the final Keep
opportunity. Videos play at normal speed; only timer transitions are accelerated.
