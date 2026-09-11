import { createHash } from "node:crypto";
import { readFile, writeFile } from "node:fs/promises";
import { fileURLToPath } from "node:url";
import { CURRENT_CATALOG_REVISION, DEFAULT_WORKOUT_MODIFIERS, SUPPORTED_MINUTES,
  WORKOUT_MODIFIERS, WORKOUT_MODIFIER_VALIDATION_PROFILES, SUPPORTED_WORKOUT_MODIFIER_MASK,
  getWorkoutAvailability } from "../workout.js";

export function createAvailabilityReport(catalog, source) {
  const physicalProfiles = [...new Set([...WORKOUT_MODIFIER_VALIDATION_PROFILES,
    DEFAULT_WORKOUT_MODIFIERS, DEFAULT_WORKOUT_MODIFIERS | WORKOUT_MODIFIERS.Insect,
    DEFAULT_WORKOUT_MODIFIERS | WORKOUT_MODIFIERS.Wall,
    SUPPORTED_WORKOUT_MODIFIER_MASK & ~WORKOUT_MODIFIERS.Light])];
  const profiles = [...new Set(physicalProfiles.flatMap((profile) => [profile, profile | WORKOUT_MODIFIERS.Light]))]
    .sort((left, right) => left - right);
  const contexts = profiles.flatMap((profile) => SUPPORTED_MINUTES.map((minutes) => {
    const availability = getWorkoutAvailability(catalog, minutes, profile);
    return { profile, minutes, canStart: availability.canStart,
      requiresAcceptance: availability.requiresAcceptance, resolutionMinutes: availability.resolutionMinutes,
      selectedGroups: availability.groups.map((group) => group.id),
      missingGroups: availability.missingGroups, regions: availability.regions };
  }));
  return {
    schemaVersion: 1, policy: "primary-targets-v1", catalogRevision: CURRENT_CATALOG_REVISION,
    catalogRecordCount: catalog.length,
    catalogSha256: createHash("sha256").update(source.replaceAll("\r\n", "\n")).digest("hex"),
    meaning: "Availability, not a quota. Limited contexts require explicit consent; blocked contexts cannot start.",
    summary: { contexts: contexts.length,
      fullScope: contexts.filter((context) => context.canStart && !context.requiresAcceptance).length,
      limitedScope: contexts.filter((context) => context.canStart && context.requiresAcceptance).length,
      blocked: contexts.filter((context) => !context.canStart).length },
    contexts,
  };
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
  const source = await readFile(new URL("../../Flux/Assets/exercises.json", import.meta.url), "utf8");
  const report = createAvailabilityReport(JSON.parse(source), source);
  await writeFile(new URL("../../docs/catalog-audit/workout_availability_current.json", import.meta.url),
    `${JSON.stringify(report, null, 2)}\n`);
  console.log(JSON.stringify(report.summary));
}
