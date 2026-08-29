import { createHash } from "node:crypto";
import { mkdir, readFile, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

import {
  findHardFloorCategoryCoverageDeficiencies,
  findMirrorCategoryDeficiencies,
  findWorkoutModifierMaterialityDeficiencies,
  findWorkoutModifierPairCoverageDeficiencies,
  findWorkoutProfileLineupDeficiencies,
} from "../workout.js";

const scriptDirectory = path.dirname(fileURLToPath(import.meta.url));
const webRoot = path.resolve(scriptDirectory, "..");
const repositoryRoot = path.resolve(webRoot, "..");
const catalogPath = path.join(repositoryRoot, "Flux", "Assets", "exercises.json");
const outputPath = path.join(
  repositoryRoot,
  "docs",
  "catalog-audit",
  "modifier_coverage_deficits_2026-08-29.json",
);

const catalogBytes = await readFile(catalogPath);
const catalog = JSON.parse(catalogBytes.toString("utf8"));
const pairwise = findWorkoutModifierPairCoverageDeficiencies(catalog);
const hardFloorCategory = findHardFloorCategoryCoverageDeficiencies(catalog);
const materiality = findWorkoutModifierMaterialityDeficiencies(catalog);
const mirrorCategory = findMirrorCategoryDeficiencies(catalog);
const distinctLineup = findWorkoutProfileLineupDeficiencies(catalog);

const report = {
  auditDate: "2026-08-29",
  catalogRecordCount: catalog.length,
  catalogSha256: createHash("sha256").update(catalogBytes).digest("hex"),
  policy: {
    treatment: "Detected deficits are preserved explicitly; no anatomical association or modifier relationship is inflated to satisfy coverage.",
    validatorsChanged: false,
  },
  summary: {
    pairwiseDeficiencyCount: pairwise.length,
    pairwiseAffectedGroupCount: new Set(pairwise.map((item) => item.groupId)).size,
    hardFloorCategoryDeficiencyCount: hardFloorCategory.length,
    hardFloorCategoryAffectedGroupCount: new Set(
      hardFloorCategory.map((item) => item.groupId),
    ).size,
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

await mkdir(path.dirname(outputPath), { recursive: true });
await writeFile(outputPath, `${JSON.stringify(report, null, 2)}\n`);
console.log(`Deficit report: ${outputPath}`);
console.log(JSON.stringify(report.summary));
