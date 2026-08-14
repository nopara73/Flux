# Android and web parity

- Treat the Android app in [`Flux/`](Flux/) as the canonical product contract and keep [`web/`](web/) synchronized with it at all times.
- Update both platforms in the same change whenever workout flow, wording, controls, durations, exercise selection, taxonomy, catalog migration, persistence, sounds, colors, or runtime media changes.
- Keep the web catalog and deployable media sourced from the Android runtime assets. Do not maintain a divergent web-only catalog or duplicate media source of truth.
- Platform-specific responsive layout may adapt to the screen, but the visible product flow, available actions, state transitions, and outcomes must remain equivalent.
- Do not refresh [`web/mobile-parity.json`](web/mobile-parity.json) merely to make the parity check pass. First port and review every affected Android behavior in the web implementation, then refresh the lock.
- Extend the cross-platform contract tests whenever a new shared behavior or migration rule is introduced, so future Android-only changes fail web validation.
- Before considering a shared change complete, run the Android tests, the web tests, and the production web build. If either platform cannot be synchronized and validated, report the work as incomplete and do not publish it.

# Exercise selection guidelines

- Select real, established, worthwhile exercises first; never invent movements, filler, or artificial variations.
- Accept an exercise only when an ordinary person can immediately copy it for 45 seconds from its plain name and final silent demonstration. Any uncertainty means rejection.
- Require feet-only ground contact, zero equipment, ordinary-shoe compatibility, quiet execution, and a 2 m × 2 m maximum area.
- Require exact human media showing the complete natural movement at normal speed. Reject approximate, composited, obscured, mismatched, or misleading demonstrations.
- Allow similar exercises.
- Derive timing, sides, directions, name, and muscle associations from the demonstrated movement—never force the demonstration to fit predetermined metadata.
- Assign muscle groups only after approval; coverage requirements never justify admitting a weak exercise.
- Review the final packaged exercise—including crop, loop, hold frame, and every timer phase—inside an actual workout before promotion.
