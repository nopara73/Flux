import { createHash } from "node:crypto";
import { cp, mkdir, readFile, readdir, rm, stat, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const scriptDirectory = path.dirname(fileURLToPath(import.meta.url));
const webRoot = path.resolve(scriptDirectory, "..");
const repositoryRoot = path.resolve(webRoot, "..");
const outputRoot = path.resolve(webRoot, "dist");

if (!outputRoot.startsWith(`${webRoot}${path.sep}`)) {
  throw new Error("Refusing to build outside the web project.");
}

await rm(outputRoot, { recursive: true, force: true });
await mkdir(outputRoot, { recursive: true });

const workoutSource = await readFile(path.join(webRoot, "workout.js"), "utf8");
const workoutOutputName = fingerprintedName("workout", "js", workoutSource);
await writeFile(path.join(outputRoot, workoutOutputName), workoutSource, "utf8");

const appSource = await readFile(path.join(webRoot, "app.js"), "utf8");
const fingerprintedAppSource = appSource.replace(
  'from "./workout.js";',
  `from "./${workoutOutputName}";`,
);
if (fingerprintedAppSource === appSource) {
  throw new Error("Could not fingerprint the workout module import.");
}
const appOutputName = fingerprintedName("app", "js", fingerprintedAppSource);
await writeFile(path.join(outputRoot, appOutputName), fingerprintedAppSource, "utf8");

const stylesSource = await readFile(path.join(webRoot, "styles.css"), "utf8");
const stylesOutputName = fingerprintedName("styles", "css", stylesSource);
await writeFile(path.join(outputRoot, stylesOutputName), stylesSource, "utf8");

const indexSource = await readFile(path.join(webRoot, "index.html"), "utf8");
const fingerprintedIndex = indexSource
  .replace('./styles.css', `./${stylesOutputName}`)
  .replace('./app.js', `./${appOutputName}`);
if (fingerprintedIndex === indexSource ||
    !fingerprintedIndex.includes(stylesOutputName) ||
    !fingerprintedIndex.includes(appOutputName)) {
  throw new Error("Could not fingerprint the web shell references.");
}
await writeFile(path.join(outputRoot, "index.html"), fingerprintedIndex, "utf8");

await copyInto(
  path.join(repositoryRoot, "Flux", "Assets", "exercises.json"),
  path.join(outputRoot, "data", "exercises.json"),
);
await copyInto(
  path.join(repositoryRoot, "tools", "flux_appicon.svg"),
  path.join(outputRoot, "assets", "flux_appicon.svg"),
);
await copyDirectory(
  path.join(repositoryRoot, "Flux", "Assets", "exercise_videos"),
  path.join(outputRoot, "assets", "exercise_videos"),
);
await copyOptionalDirectory(
  path.join(repositoryRoot, "Flux", "Assets", "exercise_direction_videos"),
  path.join(outputRoot, "assets", "exercise_direction_videos"),
);
await copyDirectory(
  path.join(repositoryRoot, "Flux", "Assets", "exercise_hold_frames"),
  path.join(outputRoot, "assets", "exercise_hold_frames"),
);
await copyDirectory(
  path.join(repositoryRoot, "Flux", "Resources", "raw"),
  path.join(outputRoot, "audio"),
);
await writeFile(path.join(outputRoot, ".nojekyll"), "", "utf8");

const catalog = JSON.parse(
  await readFile(path.join(outputRoot, "data", "exercises.json"), "utf8"),
);

if (!Array.isArray(catalog) || catalog.length !== 429) {
  throw new Error(`Expected 429 exercises, found ${catalog?.length ?? "invalid data"}.`);
}

for (const exercise of catalog) {
  await requireFile(path.join(outputRoot, "assets", exercise.video));

  if (exercise.mode === "Hold") {
    await requireFile(
      path.join(
        outputRoot,
        "assets",
        "exercise_hold_frames",
        `exercise_${String(exercise.id).padStart(4, "0")}.png`,
      ),
    );
  }

  if (exercise.directionSequence !== "None") {
    await requireFile(
      path.join(
        outputRoot,
        "assets",
        "exercise_direction_videos",
        `exercise_${String(exercise.id).padStart(4, "0")}.mp4`,
      ),
    );
  }
}

const assetVersions = {};
for (const file of await walk(path.join(outputRoot, "assets"))) {
  const relativePath = path
    .relative(path.join(outputRoot, "assets"), file)
    .split(path.sep)
    .join("/");
  assetVersions[relativePath] = createHash("sha256")
    .update(await readFile(file))
    .digest("hex");
}
await writeFile(
  path.join(outputRoot, "data", "asset-versions.json"),
  `${JSON.stringify(assetVersions, null, 2)}\n`,
  "utf8",
);

const outputFiles = await walk(outputRoot);
const forbiddenGifs = outputFiles.filter((file) => file.toLowerCase().endsWith(".gif"));
if (forbiddenGifs.length > 0) {
  throw new Error(`The web build must not contain GIF intermediates: ${forbiddenGifs[0]}`);
}

const outputBytes = (
  await Promise.all(outputFiles.map(async (file) => (await stat(file)).size))
).reduce((total, size) => total + size, 0);

console.log(
  `Built ${outputFiles.length} files (${(outputBytes / 1024 / 1024).toFixed(2)} MiB), ` +
    `${catalog.length} exercises, 0 GIFs.`,
);

async function copyInto(source, destination) {
  await mkdir(path.dirname(destination), { recursive: true });
  await cp(source, destination);
}

async function copyDirectory(source, destination) {
  await mkdir(path.dirname(destination), { recursive: true });
  await cp(source, destination, { recursive: true });
}

async function copyOptionalDirectory(source, destination) {
  try {
    await copyDirectory(source, destination);
  } catch (error) {
    if (error?.code !== "ENOENT") {
      throw error;
    }
    await mkdir(destination, { recursive: true });
  }
}

async function requireFile(file) {
  const information = await stat(file);
  if (!information.isFile() || information.size === 0) {
    throw new Error(`Missing runtime asset: ${file}`);
  }
}

function fingerprintedName(stem, extension, content) {
  const fingerprint = createHash("sha256").update(content).digest("hex").slice(0, 12);
  return `${stem}.${fingerprint}.${extension}`;
}

async function walk(directory) {
  const entries = await readdir(directory, { withFileTypes: true });
  const files = [];
  for (const entry of entries) {
    const target = path.join(directory, entry.name);
    if (entry.isDirectory()) {
      files.push(...(await walk(target)));
    } else if (entry.isFile()) {
      files.push(target);
    }
  }
  return files;
}
