import { createHash } from "node:crypto";
import { readFile, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

import {
  CURRENT_CATALOG_REVISION,
  findHardFloorCategoryCoverageDeficiencies,
  findMirrorCategoryDeficiencies,
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
    treatment: "Detected deficits are preserved explicitly; no anatomical association or modifier relationship is inflated to satisfy coverage.",
    validatorsChanged: false,
  },
  summary: {
    pairwiseDeficiencyCount: pairwise.length,
    pairwiseAffectedGroupCount: affectedGroupCount(pairwise),
    hardFloorCategoryDeficiencyCount: hardFloorCategory.length,
    hardFloorCategoryAffectedGroupCount:
      affectedGroupCount(hardFloorCategory),
    materialityDeficiencyCount: materiality.length,
    mirrorCategoryDeficiencyCount: mirrorCategory.length,
    distinctLineupDeficiencyCount: distinctLineup.length,
  },
  pairwise,
  hardFloorCategory,
  materiality,
  mirrorCategory,
  distinctLineup,
};

await writeFile(outputPath, `${JSON.stringify(report, null, 2)}\n`, "utf8");
console.log(`Catalog deficit ledger: ${outputPath}`);

function affectedGroupCount(deficiencies) {
  return new Set(deficiencies.map((deficiency) => deficiency.groupId)).size;
}
