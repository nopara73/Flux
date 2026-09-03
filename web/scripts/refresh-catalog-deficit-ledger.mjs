import { createHash } from "node:crypto";
import { readFile, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

import {
  BROAD_COVERAGE_RESOLUTION_MINUTES,
  CURRENT_CATALOG_REVISION,
  MINIMUM_EXERCISES_PER_BROAD_MODIFIER_PAIR_STATE_PER_GROUP,
  MINIMUM_EXERCISES_PER_FINE_MODIFIER_PAIR_STATE_PER_GROUP,
  MINIMUM_EXERCISES_PER_MUSCULAR_DEMAND_CATEGORY_PER_GROUP,
  findHardFloorCategoryCoverageDeficiencies,
  findMirrorCategoryDeficiencies,
  findMuscularDemandCoverageDeficiencies,
  findWorkoutModifierMaterialityDeficiencies,
  findWorkoutModifierPairCoverageDeficiencies,
  findWorkoutProfileLineupDeficiencies,
} from "../workout.js";

const scriptDirectory = path.dirname(fileURLToPath(import.meta.url));
const webRoot = path.resolve(scriptDirectory, "..");
const repositoryRoot = path.resolve(webRoot, "..");
const catalogPath = path.join(
  repositoryRoot,
  "Flux",
  "Assets",
  "exercises.json",
);
const outputPath = path.join(
  repositoryRoot,
  "docs",
  "catalog-audit",
  "modifier_coverage_deficits_current.json",
);

const catalogSource = await readFile(catalogPath, "utf8");
const catalog = JSON.parse(catalogSource);
const pairwise = findWorkoutModifierPairCoverageDeficiencies(catalog);
const hardFloorCategory =
  findHardFloorCategoryCoverageDeficiencies(catalog);
const muscularDemand = findMuscularDemandCoverageDeficiencies(catalog);
const materiality = findWorkoutModifierMaterialityDeficiencies(catalog);
const mirrorCategory = findMirrorCategoryDeficiencies(catalog);
const distinctLineup = findWorkoutProfileLineupDeficiencies(catalog);

const report = {
  catalogRevision: CURRENT_CATALOG_REVISION,
  catalogRecordCount: catalog.length,
  catalogSha256: createHash("sha256")
    .update(catalogSource.replaceAll("\r\n", "\n"))
    .digest("hex"),
  policy: {
    treatment: "Every enforceable coverage category must have zero deficits; the ledger is diagnostic and cannot authorize catalog debt.",
    broadCoverageResolutionMinutes: BROAD_COVERAGE_RESOLUTION_MINUTES,
    broadModifierPairMinimumPerStatePerGroup:
      MINIMUM_EXERCISES_PER_BROAD_MODIFIER_PAIR_STATE_PER_GROUP,
    fineModifierPairMinimumPerStatePerGroup:
      MINIMUM_EXERCISES_PER_FINE_MODIFIER_PAIR_STATE_PER_GROUP,
    muscularDemandMinimumPerCategoryPerGroup:
      MINIMUM_EXERCISES_PER_MUSCULAR_DEMAND_CATEGORY_PER_GROUP,
  },
  summary: {
    pairwiseDeficiencyCount: pairwise.length,
    pairwiseAffectedGroupCount: affectedGroupCount(pairwise),
    hardFloorCategoryDeficiencyCount: hardFloorCategory.length,
    hardFloorCategoryAffectedGroupCount:
      affectedGroupCount(hardFloorCategory),
    muscularDemandDeficiencyCount: muscularDemand.length,
    muscularDemandAffectedGroupCount: affectedGroupCount(muscularDemand),
    demandZeroDeficiencyCount: muscularDemand.filter((item) =>
      item.muscularDemand === 0).length,
    demandZeroAffectedGroupCount: affectedGroupCount(
      muscularDemand.filter((item) => item.muscularDemand === 0),
    ),
    demandTwoDeficiencyCount: muscularDemand.filter((item) =>
      item.muscularDemand === 2).length,
    demandTwoAffectedGroupCount: affectedGroupCount(
      muscularDemand.filter((item) => item.muscularDemand === 2),
    ),
    materialityDeficiencyCount: materiality.length,
    mirrorCategoryDeficiencyCount: mirrorCategory.length,
    distinctLineupDeficiencyCount: distinctLineup.length,
  },
  pairwise,
  hardFloorCategory,
  muscularDemand,
  materiality,
  mirrorCategory,
  distinctLineup,
};

await writeFile(outputPath, `${JSON.stringify(report, null, 2)}\n`, "utf8");
console.log(`Catalog deficit ledger: ${outputPath}`);

function affectedGroupCount(deficiencies) {
  return new Set(deficiencies.map((deficiency) => deficiency.groupId)).size;
}
