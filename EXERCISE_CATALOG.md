# Flux exercise catalog

Flux contains 327 quality-reviewed standing movements. Every dominant region
has at least three choices; the current counts range from 15 to 94. The catalog
mixes ordinary active-range-of-motion and balance drills with established
movements from rehabilitation, yoga, tai chi, qigong, dance, and martial arts.
Domain assignment is only an indexing aid; it is not a claim that a movement
trains only that region.

## Editorial rules

Every catalog entry must:

- be a complete named movement or posture, never a generated tempo/range/side
  suffix used to inflate the count;
- keep all ground contact at the feet;
- remain practical in ordinary shoes or barefoot;
- fit inside a 3 m × 3 m space;
- require no wall, chair, floor exercise, prop, partner, or other equipment;
- avoid jumping, stomping, clapping, vocalization, or another neighbor-disturbing
  action;
- be bilateral/symmetric, or visibly complete both left and right repetitions
  inside one uninterrupted loop; a fixed lead side, supporting side, diagonal,
  hand role, or clockwise-only circle is not allowed;
- have exactly one dominant region and its own bundled H.264 MP4.

Each entry is explicitly classified as a repetition or a hold. A hold is one
bilateral or symmetric position sustained for the full timer; alternating
sides, breath cycles, raise-and-lower actions, flows, and repeated contractions
remain repetitions even when their traditional name contains “hold.” Hold MP4s
loop only on the preview screen. During the countdown the video plays once to
the curated target from `tools/HoldExerciseFrames.psd1` and freezes there.

The canonical base catalog is maintained in `tools/RealExerciseCatalog.psd1`.
`tools/BilateralExerciseNames.psd1` contains the reviewed replacements that
enforce the no-one-sided rule. `tools/VerifiedExerciseDemos.psd1` is the
quality-first allowlist. The generator rejects duplicate names, the former
synthetic modifier suffixes, missing motion profiles, regions with fewer than
three exercises, and constraint-metadata violations.

## Reference families

The catalog is original editorial work, not a copy of any single exercise
library. These references anchor the terminology and movement families used:

- [ACE no-equipment exercise library](https://www.acefitness.org/resources/everyone/exercise-library/equipment/no-equipment/)
- [NHS balance exercises](https://www.nhs.uk/live-well/exercise/balance-exercises/)
- [Hospital for Special Surgery back and neck stretches](https://www.hss.edu/health-library/move-better/back-neck-stretches)
- [Vestibular Disorders Association rehabilitation overview](https://vestibular.org/article/diagnosis-treatment/treatments/vestibular-rehabilitation-therapy-vrt/)
- [Yang Family Tai Chi traditional hand form](https://yangfamilytaichi.com/traditional-hand-form-103/)
- [International Association for Dance Medicine & Science resources](https://iadms.org/research-publications/bulletin-for-dancers-teachers/)
- [World Taekwondo poomsae rules](https://www.worldtaekwondo.org/att_file/documents/Poomsae_Competition_Rules_and_Interpretation_%28In_force_as_of_September_30_2024%29.pdf)

## Animation sources and accuracy

Every entry has an offline 256 × 256 H.264 MP4. Repetition videos loop. A hold
uses one MP4 in two playback modes: it loops during preview, then plays once
from the start and remains on its reviewed final pose during the countdown.
The best available demonstrations use
one of these reviewed sources:

- visually reviewed human exercise footage, normalized to 256 × 256 and
  mirrored into a left/right cycle only where mirroring is mechanically valid;
- other visually reviewed external anatomical demonstrations;
- visually reviewed Posecode 3D renders, including custom alternating and
  bidirectional sequences;
- semantically identical reviewed media copies and deterministic directional
  transforms where reversing the frame order is the exact named movement.

No placeholder media is bundled. The former 673 unverified or comparatively
weak schematic entries were removed. All **327 included demonstrations** meet
the current high bar. Region and source-quality counts are maintained in
[`DEMONSTRATION_AUDIT.md`](DEMONSTRATION_AUDIT.md).

Media mappings live in `tools/ExternalExerciseMedia.psd1`,
`tools/PosecodeExerciseMedia.psd1`, `tools/VerifiedExerciseDemos.psd1`, and
`tools/HoldExerciseFrames.psd1`. Exact reviewed media reused by mechanically
identical catalog entries is tracked in `tools/ExactExerciseMediaCopies.psd1`.
Exact deterministic direction and tempo mappings are tracked in
`tools/ExactExerciseMediaTransforms.psd1`.
Editable sources for the reviewed custom 3D batch live in
`tools/PosecodeSources`.
`tools/Test-ExerciseVideos.ps1` verifies all 327 MP4 containers and compares
the final decoded frame of every hold against its reviewed target image.
The external clips are used for this private personal build and are not a
commercial media-clearance record.
