const elements = {
  previous: document.querySelector("#previous"),
  next: document.querySelector("#next"),
  progress: document.querySelector("#progress"),
  name: document.querySelector("#exercise-name"),
  video: document.querySelector("#exercise-video"),
  hold: document.querySelector("#hold-frame"),
  mediaMessage: document.querySelector("#media-message"),
  verdicts: [...document.querySelectorAll("[data-verdict]")],
};

let exercises = [];
let reviewDocument = null;
let index = 0;
let saving = false;
let mirrorNextLoop = false;

elements.previous.addEventListener("click", () => move(-1));
elements.next.addEventListener("click", () => move(1));
for (const button of elements.verdicts) {
  button.addEventListener("click", () => mark(button.dataset.verdict));
}
document.addEventListener("keydown", (event) => {
  if (event.key === "ArrowLeft") move(-1);
  if (event.key === "ArrowRight") move(1);
  const shortcut = { "1": "nothingburger", "2": "unclear", "3": "stupid", "4": "good" }[event.key];
  if (shortcut) mark(shortcut);
});
elements.video.addEventListener("canplay", () => {
  elements.mediaMessage.hidden = true;
  elements.video.play().catch(() => {});
});
elements.video.addEventListener("error", () => {
  elements.mediaMessage.textContent = "Demonstration unavailable";
  elements.mediaMessage.hidden = false;
});
elements.video.addEventListener("ended", () => {
  const exercise = exercises[index];
  if (
    exercise.directionSequence === "None" &&
    ["ScreenLeftThenRight", "ScreenRightThenLeft"].includes(exercise.sideSequence)
  ) {
    mirrorNextLoop = !mirrorNextLoop;
    elements.video.style.transform = mirrorNextLoop ? "scaleX(-1)" : "none";
  }
  elements.video.currentTime = 0;
  elements.video.play().catch(() => {});
});

await start();

async function start() {
  const [catalogResponse, reviewsResponse] = await Promise.all([
    fetch("/api/catalog"),
    fetch("/api/reviews"),
  ]);
  exercises = (await catalogResponse.json()).toSorted((a, b) => a.id - b.id);
  reviewDocument = await reviewsResponse.json();
  const firstUnreviewed = findNextUnreviewed(0);
  const remembered = exercises.findIndex((exercise) => exercise.id === reviewDocument.lastExerciseId);
  index = firstUnreviewed >= 0 ? firstUnreviewed : Math.max(0, remembered);
  render();
}

async function mark(verdict) {
  if (saving || exercises.length === 0) return;
  saving = true;
  setButtonsDisabled(true);
  const exercise = exercises[index];
  reviewDocument.reviews = reviewDocument.reviews.filter((review) => review.exerciseId !== exercise.id);
  reviewDocument.reviews.push({
    exerciseId: exercise.id,
    exerciseName: exercise.name,
    video: exercise.video,
    verdict,
    reviewedAtUtc: new Date().toISOString(),
  });
  reviewDocument.reviews.sort((a, b) => a.exerciseId - b.exerciseId);
  reviewDocument.lastExerciseId = exercise.id;
  reviewDocument.updatedAtUtc = new Date().toISOString();
  try {
    await save();
    const nextUnreviewed = findNextUnreviewed(index + 1);
    index = nextUnreviewed >= 0 ? nextUnreviewed : (index + 1) % exercises.length;
    render();
  } finally {
    saving = false;
    setButtonsDisabled(false);
  }
}

async function move(offset) {
  if (saving || exercises.length === 0) return;
  index = (index + offset + exercises.length) % exercises.length;
  reviewDocument.lastExerciseId = exercises[index].id;
  await save();
  render();
}

function render() {
  const exercise = exercises[index];
  const reviewedCount = exercises.filter(isReviewed).length;
  elements.progress.textContent = `${index + 1} / ${exercises.length} · ${reviewedCount} reviewed`;
  elements.name.textContent = `${exercise.id}. ${exercise.name}`;
  const review = reviewDocument.reviews.find((candidate) => isCurrent(candidate, exercise));
  for (const button of elements.verdicts) {
    button.classList.toggle("selected", button.dataset.verdict === review?.verdict);
  }
  loadMedia(exercise);
}

function loadMedia(exercise) {
  elements.video.pause();
  elements.video.removeAttribute("src");
  elements.video.load();
  elements.video.hidden = false;
  elements.hold.hidden = true;
  elements.mediaMessage.textContent = "Loading…";
  elements.mediaMessage.hidden = false;
  mirrorNextLoop = false;
  elements.video.style.transform = "none";

  if (exercise.presentation === "Still") {
    elements.video.hidden = true;
    elements.hold.hidden = false;
    elements.hold.onload = () => { elements.mediaMessage.hidden = true; };
    elements.hold.onerror = () => { elements.mediaMessage.textContent = "Demonstration unavailable"; };
    elements.hold.src = `/media/exercise_hold_frames/exercise_${String(exercise.id).padStart(4, "0")}.png`;
    return;
  }

  elements.video.src = exercise.directionSequence === "None"
    ? `/media/${exercise.video}`
    : `/media/exercise_direction_videos/exercise_${String(exercise.id).padStart(4, "0")}.mp4`;
  elements.video.load();
}

function findNextUnreviewed(start) {
  for (let offset = 0; offset < exercises.length; offset += 1) {
    const candidate = (start + offset) % exercises.length;
    if (!isReviewed(exercises[candidate])) return candidate;
  }
  return -1;
}

function isReviewed(exercise) {
  return reviewDocument.reviews.some((review) => isCurrent(review, exercise));
}

function isCurrent(review, exercise) {
  return review.exerciseId === exercise.id &&
    review.exerciseName === exercise.name &&
    review.video === exercise.video;
}

async function save() {
  const response = await fetch("/api/reviews", {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify(reviewDocument),
  });
  if (!response.ok) throw new Error(`Save failed: ${response.status}`);
}

function setButtonsDisabled(disabled) {
  for (const button of elements.verdicts) button.disabled = disabled;
}
