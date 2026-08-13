# Android and web parity

- Treat the Android app in [`Flux/`](Flux/) as the canonical product contract and keep [`web/`](web/) synchronized with it at all times.
- Update both platforms in the same change whenever workout flow, wording, controls, durations, exercise selection, taxonomy, catalog migration, persistence, sounds, colors, or runtime media changes.
- Keep the web catalog and deployable media sourced from the Android runtime assets. Do not maintain a divergent web-only catalog or duplicate media source of truth.
- Platform-specific responsive layout may adapt to the screen, but the visible product flow, available actions, state transitions, and outcomes must remain equivalent.
- Do not refresh [`web/mobile-parity.json`](web/mobile-parity.json) merely to make the parity check pass. First port and review every affected Android behavior in the web implementation, then refresh the lock.
- Extend the cross-platform contract tests whenever a new shared behavior or migration rule is introduced, so future Android-only changes fail web validation.
- Before considering a shared change complete, run the Android tests, the web tests, and the production web build. If either platform cannot be synchronized and validated, report the work as incomplete and do not publish it.

# Exercise selection guidelines

- Follow the canonical muscle-group taxonomy and roll-ups in [`Flux/Services/MassGroupingTaxonomy.cs`](Flux/Services/MassGroupingTaxonomy.cs).
- Schedule every workout resolution from smallest to largest estimated muscle mass.
- Select worthwhile exercises first; assign muscle groups afterward.
- Reject trivial gestures and ineffective dance steps.
- Use real named movements or postures; do not invent count-inflating variants.
- Allow similar exercises when a real variation meaningfully changes stance, leverage, range of motion, balance demand, contraction, tempo, or training emphasis; reject only true duplicates and cosmetic renames.
- Draw broadly from any movement practices.
- Keep all ground contact at the feet; exclude floor, seated, kneeling, and hand-supported work. Contact by any body part other than the feet is transiently allowed, meaning: it must be momentary.
- Require zero equipment, including walls, chairs, partners, and props.
- Keep every movement practical both barefoot and in ordinary shoes.
- Keep every movement inside a 3 m × 3 m space.
- Keep movements quiet; exclude jumping, stomping, clapping, and vocalization.
- Use symmetric or naturally alternating movement when it flows cleanly; otherwise require the explicit 20-second first side, 5-second change, 20-second second side protocol.
- Above 30 minutes, upgrade selected sided exercises to 45 seconds per side with a 15-second change before allocating repeated sets; rank within each tier by prior keep, then muscle mass.
- Treat unequal worker/resister, over/under, or interlocked limb roles as sided even when both limbs exert force; swap roles with the 20-5-20 protocol.
- Assign every canonical group the exercise meaningfully trains; mere involvement does not qualify.
- Assign one primary scheduling group representing the clearest intended stimulus.
- Let an exercise compete only in a roll-up containing its primary group and where it trains at least half of the roll-up's canonical leaves.
- Maintain at least 10 such selectable primary-owned exercises in every roll-up at every supported resolution.
- Require an accurate human demonstration for every exercise; exclude synthetic or approximate media.
- Make the plain exercise name and silent demonstration sufficient to copy at a glance; reject movements that depend on narration, hidden effort, specialist terminology, or memorized choreography.
- For stance-defined movements, keep both feet and the full body relationship readable throughout; reject partial-body camera cuts, postage-stamp subjects, and ambiguous group framing.
- Prefer a reviewed still image when one settled posture communicates the whole exercise more clearly than looping video.
- Make hold demonstrations settle and remain on the final position during the timer.
