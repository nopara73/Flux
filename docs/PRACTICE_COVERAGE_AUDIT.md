# Flux movement-practice coverage audit

Generated from the runtime catalog and the supplementary movement-practices DAG. The DAG is used for provenance, discovery, and diversity review; Flux capacities remain the scheduling taxonomy.

## Outcome

- Runtime catalog: **246** exercises (**10** added in this pass).
- Exact practice-node provenance: **151**; family-only: **15**; domain-only: **76**; intentionally unmapped: **4**.
- Primary DAG coverage: **7/18** domains. Including honest cross-links: **11/18** domains and **23/71** families.
- Newly represented families: **3** — Bipedal locomotion cultures, Endurance & conditioning methods, Pacific dance traditions.
- All additions passed the standing, feet-only, zero-equipment, shoe-agnostic, 3 m × 3 m, quiet, non-jumping, bilateral/alternating, and exact-human-media rules.

## Added from weak places

| Exercise | Primary capacity | Practice branch | Why it survived review |
|---|---|---|---|
| Tandem Walk | Balance | `gait_retraining` | Traveling heel-to-toe gait materially narrows the base of support and demands precise foot placement. |
| Side Shuffle | Stamina | `aerobics` | Continuous lateral acceleration and deceleration in an athletic stance add quiet frontal-plane conditioning. |
| Backward Walking | Stepping | `backward_walking` | Backward initiation, clearance, and weight acceptance add a genuinely absent direction of gait. |
| Walking with Horizontal Head Turns | Balance | `vestibular_rehabilitation` | Head turns create a meaningful self-generated visual and vestibular disturbance while gait continues. |
| Hula Kāholo | Stepping | `hula` | Side-close-side-tap weight transfer in a sustained bent-knee stance adds lateral gait skill and leg endurance. |
| Bhangra Chaal | Stamina | `bhangra` | Large alternating knee and heel actions with overhead arm coordination make a vigorous whole-body round. |
| Samba no Pé Basic | Stamina | `samba` | Continuous syncopated triple-foot action and weight transfer provide compact whole-body conditioning. |
| Low-Impact Skater Step | Stamina | `aerobics` | Wide alternating lateral steps and a low stance create systemic effort without a jump or flight phase. |
| High-Knee March | Stamina | `running` | Reciprocal knee drive and contralateral arm action provide a quiet running-mechanics and endurance drill. |
| Wing Chun Juen Ma Turning Stance | Balance | `wing_chun` | Repeated left-centre-right weight shifts and stance pivots train controlled turning and lower-body endurance. |

## Strong primary-capacity pools

The frozen 236-record capacity audit remains the baseline. Every addition above is a high-confidence Keep with a clear primary stimulus, so it qualifies as a strong representative.

| Capacity | Frozen baseline | Added primary exercises | Current strong pool | Minimum met |
|---|---:|---:|---:|:---:|
| Balance | 34 | 3 | 37 | Yes |
| Strength | 61 | 0 | 61 | Yes |
| Stamina | 8 | 5 | 13 | Yes |
| Stepping | 12 | 2 | 14 | Yes |
| Mobility | 31 | 0 | 31 | Yes |

## Practice-label concentration

| Catalog practice | Exercises |
|---|---:|
| Bodyweight conditioning | 61 |
| Standing mobility and movement practice | 51 |
| Karate | 17 |
| Ballet | 16 |
| Stretching | 16 |
| Balance training | 9 |
| Belly dance | 9 |
| Bharatanatyam | 8 |
| Self-resistance | 8 |
| Boxing | 7 |
| Tai Chi | 7 |
| Qigong | 6 |
| Yoga | 6 |
| Ninja hand-seal coordination | 4 |
| Capoeira | 2 |
| Low-impact aerobics | 2 |
| Taekwondo | 2 |
| Wing Chun | 2 |
| Backward walking | 1 |
| Bhangra | 1 |
| Functional Range Conditioning | 1 |
| Fundamental movement skills | 1 |
| Gait retraining | 1 |
| Hula | 1 |
| Jazz dance | 1 |
| Odissi | 1 |
| Pilates | 1 |
| Running drills | 1 |
| Samba | 1 |
| Sumo | 1 |
| Vestibular rehabilitation | 1 |

## Domain coverage

| DAG domain | Baseline primary | Current primary | Current all paths |
|---|---:|---:|---:|
| Acrobatics, circus & object manipulation | 0 | 0 | 2 |
| Animal-partnered movement | 0 | 0 | 0 |
| Aquatic & underwater movement | 0 | 0 | 0 |
| Combat, martial & weapon cultures | 37 | 38 | 38 |
| Dance & rhythmic movement | 35 | 38 | 40 |
| Digital, mediated & emerging movement cultures | 0 | 0 | 0 |
| Games, sport & contest | 0 | 0 | 0 |
| Locomotion, travel & terrain skills | 0 | 2 | 2 |
| Military drill, tactical & service movement | 0 | 0 | 0 |
| Movement education & pedagogy | 1 | 1 | 1 |
| Outdoor, adventure & survival movement | 0 | 0 | 0 |
| Physical culture, conditioning & exercise | 146 | 148 | 148 |
| Play, improvisation & movement exploration | 0 | 0 | 2 |
| Ritual, devotional & ceremonial movement | 0 | 0 | 16 |
| Somatics, mind–body & internal arts | 13 | 13 | 20 |
| Theatre, mime & movement performance | 0 | 0 | 8 |
| Therapeutic, rehabilitative & adaptive movement | 0 | 2 | 15 |
| Work, craft & subsistence movement | 0 | 0 | 0 |

## Editorial conclusions

- The largest remaining weakness is not raw catalog size but the nearly half of legacy records whose labels still collapse into generic fitness or mobility buckets. Recovering their true lineage requires source-by-source verification, not name guessing.
- Aquatic, animal-partnered, digital, weapon, grappling, climbing, circus/object, and most occupational/outdoor branches remain deliberately empty because their defining stimulus depends on water, equipment, partners, floor/hand contact, impact, or more space.
- Dry-land swimming pantomimes, generic arm circles, long-court ghosting, rail-assisted gait footage, hidden-feet demonstrations, and ambiguous one-sided martial footage were reviewed and rejected in this pass.
- The next defensible discovery targets are Western somatics, actor movement training, compact natural movement, and additional gait-retraining patterns—but only when exact full-body human footage survives the same constraints.

## Method

- `contains` edges determine primary ancestry. `contains` plus `cross-link` edges determine all-path coverage. Characterization/classification edges are excluded.
- A practice-level mapping names a concrete DAG practice; family/domain mappings are explicitly reported as less specific. The four Naruto hand-seal records remain unmapped rather than being falsely assigned to ninja obstacle training.
- The row-level mapping is in [`practice_coverage_audit.csv`](practice_coverage_audit.csv); the reviewed catalog delta is in [`catalog_additions_2026-08-02.csv`](catalog_additions_2026-08-02.csv).
