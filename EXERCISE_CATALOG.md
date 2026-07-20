# Flux exercise catalog

Flux contains 1,000 distinct standing movements: 100 for each dominant region.
The catalog deliberately mixes ordinary active-range-of-motion and balance
drills with established movements from rehabilitation, yoga, tai chi, qigong,
dance, and martial arts. Domain assignment is only an indexing aid; it is not a
claim that a movement trains only that region.

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
- have exactly one dominant region and its own bundled animated GIF.

The canonical base catalog is maintained in `tools/RealExerciseCatalog.psd1`.
`tools/BilateralExerciseNames.psd1` contains the reviewed replacements that
enforce the no-one-sided rule without changing the 100-per-region structure.
The generator rejects duplicate names, the former synthetic modifier suffixes,
missing motion profiles, unbalanced region counts, and constraint-metadata
violations.

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

Every entry has a moving, looping GIF. The best available demonstrations use
one of four sources:

- visually reviewed human exercise footage, normalized to 256 × 256 and
  mirrored into a left/right cycle only where mirroring is mechanically valid;
- other visually reviewed external anatomical demonstrations;
- visually reviewed Posecode 3D renders, including custom alternating and
  bidirectional sequences;
- purpose-built animated SVG diagrams for motions such as smooth pursuit,
  saccades, blinking, convergence, VOR, head movement, and a conservative set
  of simple whole-body movements.

The remaining GIFs are explicit temporary family-level placeholders. They are
animated and obey the app's simple one-loop interaction, but they are not
claimed to be exact demonstrations. The current high-bar result is **184 exact
demonstrations and 816 still requiring exact media**. The region-by-region
counts and the complete list of all 816 remaining names are maintained in
[`DEMONSTRATION_AUDIT.md`](DEMONSTRATION_AUDIT.md).

Media mappings live in `tools/ExternalExerciseMedia.psd1`,
`tools/PosecodeExerciseMedia.psd1`, and `tools/VerifiedExerciseDemos.psd1`.
Editable sources for the reviewed custom 3D batch live in
`tools/PosecodeSources`.
The external clips are used for this private personal build and are not a
commercial media-clearance record.
