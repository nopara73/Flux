import { createHash } from "node:crypto";
import { readFile, readdir, stat } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const scriptDirectory = path.dirname(fileURLToPath(import.meta.url));
const webRoot = path.resolve(scriptDirectory, "..");
const repositoryRoot = path.resolve(webRoot, "..");
const manifestPath = path.join(webRoot, "mobile-parity.json");
const sourceTargets = [
  "Flux/Flux.csproj",
  "Flux/MainActivity.cs",
  "Flux/Data",
  "Flux/Models",
  "Flux/Services",
  "Flux/Resources/color",
  "Flux/Resources/drawable",
  "Flux/Resources/layout",
  "Flux/Resources/values",
];

const sourceFiles = [];
for (const target of sourceTargets) {
  await collectSourceFiles(path.join(repositoryRoot, target));
}
sourceFiles.sort();

const hash = createHash("sha256");
for (const relativePath of sourceFiles) {
  const contents = await readFile(path.join(repositoryRoot, relativePath), "utf8");
  hash.update(relativePath.replaceAll(path.sep, "/"));
  hash.update("\0");
  hash.update(contents.replaceAll("\r\n", "\n"));
  hash.update("\0");
}

const actual = {
  schemaVersion: 1,
  sourceCount: sourceFiles.length,
  sha256: hash.digest("hex"),
};

if (process.argv.includes("--print")) {
  console.log(JSON.stringify(actual, null, 2));
  process.exit(0);
}

const expected = JSON.parse(await readFile(manifestPath, "utf8"));
if (
  expected.schemaVersion !== actual.schemaVersion ||
  expected.sourceCount !== actual.sourceCount ||
  expected.sha256 !== actual.sha256
) {
  console.error("The mobile UI or workout contract changed without a reviewed web parity update.");
  console.error(`Expected ${JSON.stringify(expected)}`);
  console.error(`Current  ${JSON.stringify(actual)}`);
  console.error("Update the web implementation, then refresh web/mobile-parity.json.");
  process.exit(1);
}

console.log(`Mobile parity locked to ${actual.sourceCount} source files (${actual.sha256.slice(0, 12)}).`);

async function collectSourceFiles(target) {
  const information = await stat(target);
  if (information.isFile()) {
    if (/\.(?:cs|csproj|xml)$/i.test(target)) {
      sourceFiles.push(path.relative(repositoryRoot, target));
    }
    return;
  }

  const entries = await readdir(target, { withFileTypes: true });
  for (const entry of entries) {
    const child = path.join(target, entry.name);
    if (entry.isDirectory()) {
      await collectSourceFiles(child);
    } else if (entry.isFile() && /\.(?:cs|csproj|xml)$/i.test(entry.name)) {
      sourceFiles.push(path.relative(repositoryRoot, child));
    }
  }
}
