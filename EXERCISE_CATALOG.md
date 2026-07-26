# Flux exercise catalog

Flux contains 214 human-demonstrated standing movements. Exercises are selected
for their movement quality first and assigned to the closest dominant region
only afterward. Full-body movements remain eligible even when several regions
contribute; the region is an indexing aid, not a claim that the exercise trains
only that body part. The generator and app require every region to retain at
least three choices.

The catalog mixes low-impact compound strength and conditioning with ordinary
active-range-of-motion and balance drills, rehabilitation movements, Pilates,
yoga, tai chi, qigong, boxing, dance, and martial arts.

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
human-only allowlist. The generator rejects non-human direct demonstrations,
derivatives that do not trace to reviewed human footage, duplicate names, the
former synthetic modifier suffixes, missing motion profiles, regions with fewer
than three exercises, and constraint-metadata violations.

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
Every retained demonstration uses one of these reviewed sources:

- visually reviewed human exercise footage, normalized to 256 × 256 and
  mirrored into a left/right cycle only where mirroring is mechanically valid;
- semantically identical copies of reviewed human footage;
- deterministic directional transforms of reviewed human footage where
  reversing the frame order is the exact named movement.

No placeholder, synthetic, schematic, anatomical, or 3D media is bundled. All
**214 included demonstrations** show an actual person. Region and source-quality
counts are maintained in
[`DEMONSTRATION_AUDIT.md`](DEMONSTRATION_AUDIT.md).

Media mappings live in `tools/ExternalExerciseMedia.psd1`,
`tools/VerifiedExerciseDemos.psd1`, and `tools/HoldExerciseFrames.psd1`.
Exact reviewed human media reused by mechanically
identical catalog entries is tracked in `tools/ExactExerciseMediaCopies.psd1`.
Exact deterministic direction and tempo mappings are tracked in
`tools/ExactExerciseMediaTransforms.psd1`.
`tools/Test-ExerciseVideos.ps1` verifies all 214 MP4 containers and compares
the final decoded frame of every hold against its reviewed target image.
The external clips are used for this private personal build and are not a
commercial media-clearance record.
