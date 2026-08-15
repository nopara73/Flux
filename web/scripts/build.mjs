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

for (const file of ["index.html", "styles.css", "app.js", "workout.js"]) {
  await cp(path.join(webRoot, file), path.join(outputRoot, file));
}

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
await copyDirectory(
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

if (!Array.isArray(catalog) || catalog.length !== 345) {
  throw new Error(`Expected 345 exercises, found ${catalog?.length ?? "invalid data"}.`);
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

async function requireFile(file) {
  const information = await stat(file);
  if (!information.isFile() || information.size === 0) {
    throw new Error(`Missing runtime asset: ${file}`);
  }
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
