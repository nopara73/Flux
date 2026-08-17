# Android and web parity

- Treat the Android app in [`Flux/`](Flux/) as the canonical product contract and keep [`web/`](web/) synchronized with it at all times.
- Update both platforms in the same change whenever workout flow, wording, controls, durations, exercise selection, taxonomy, catalog migration, persistence, sounds, colors, or runtime media changes.
- Keep the web catalog and deployable media sourced from the Android runtime assets. Do not maintain a divergent web-only catalog or duplicate media source of truth.
- Platform-specific responsive layout may adapt to the screen, but the visible product flow, available actions, state transitions, and outcomes must remain equivalent.
- Do not refresh [`web/mobile-parity.json`](web/mobile-parity.json) merely to make the parity check pass. First port and review every affected Android behavior in the web implementation, then refresh the lock.
- Extend the cross-platform contract tests whenever a new shared behavior or migration rule is introduced, so future Android-only changes fail web validation.
- Before considering a shared change complete, run the Android tests, the web tests, and the production web build. If either platform cannot be synchronized and validated, report the work as incomplete and do not publish it.
- Treat `https://nopara73.github.io/Flux/` as the public web deployment; localhost URLs are previews only. After publishing, verify the completed GitHub Pages run and confirm that the public URL serves the current catalog and hashed assets before reporting the release as deployed.

# Exercise selection guidelines

- Select real, established, worthwhile exercises first; never invent movements, filler, or artificial variations.
- Accept an exercise only when an ordinary person can immediately copy it for 45 seconds from its plain name and final silent demonstration. Any uncertainty means rejection.
- Require feet-only ground contact, zero equipment, ordinary-shoe compatibility, and a 2 m × 2 m maximum area. Quiet execution is required whenever the default-on Silence modifier is enabled; established naturally noisy movements may be admitted only when their sound is the ordinary result of execution and they remain excluded by Silence.
- Require exact human media showing the complete natural movement at normal speed. Reject approximate, composited, obscured, mismatched, or misleading demonstrations.
- Keep non-locomotor demonstrations planted in place. Advancing, retreating, or crossing the room is acceptable only when travel is an intrinsic, plainly named part of the exercise.
- Allow similar exercises.
- Derive timing, sides, directions, name, and muscle associations from the demonstrated movement—never force the demonstration to fit predetermined metadata.
- Assign muscle groups only after approval; coverage requirements never justify admitting a weak exercise.
- For every pair of workout modifiers and every workout muscle bucket, require at least five approved exercises under each of the four real UI states: on/on, on/off, off/on, and off/off. An off modifier relaxes its predicate; it never means "require incompatible." Separately require every modifier to exclude a material, anatomically broad candidate set both alone and when either member of a pair is already enabled. Keep this validation quadratic in the number of modifiers; do not replace it with an exponential all-modifier quota or an independent per-modifier approximation.
- Review the final packaged exercise—including crop, loop, hold frame, and every timer phase—inside an actual workout before promotion.
