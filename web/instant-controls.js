(() => {
  const storageKey = "flux.workout.state.v1";
  const durationOptions = Object.freeze([3, 5, 7, 10, 15, 20, 30, 45, 60, 90]);
  const modifierFlags = Object.freeze({
    insect: 1,
    silence: 2,
    mirror: 4,
    tallMirror: 8,
    hardFloor: 16,
    wall: 32,
    soleWallContact: 64,
    upperBodyClothing: 128,
    light: 256,
  });
  const feedbackDurationMs = 2_040;
  const elements = {
    dial: document.getElementById("duration-dial"),
    value: document.getElementById("duration-value"),
    decrease: document.getElementById("duration-decrease"),
    increase: document.getElementById("duration-increase"),
    range: document.getElementById("duration-range"),
    labels: [...(document.getElementById("duration-labels")?.children ?? [])],
    begin: document.getElementById("begin-workout"),
    upperBodyClothing: document.getElementById("upper-body-clothing-modifier"),
    hardFloor: document.getElementById("hard-floor-modifier"),
    insect: document.getElementById("insect-modifier"),
    silence: document.getElementById("silence-modifier"),
    light: document.getElementById("light-workout-modifier"),
    lightCountdown: document.getElementById("light-workout-countdown"),
    wall: document.getElementById("wall-modifier"),
    mirror: document.getElementById("mirror-modifier"),
    feedback: document.getElementById("modifier-feedback"),
    status: document.getElementById("status"),
  };
  if (!elements.dial || !elements.value || !elements.decrease ||
      !elements.increase || !elements.range || !elements.begin ||
      !elements.upperBodyClothing ||
      !elements.insect || !elements.silence || !elements.light ||
      !elements.lightCountdown || !elements.wall ||
      !elements.mirror ||
      !elements.feedback) {
    return;
  }

  const persistedSetup = readPersistedSetup();
  let selectedMinutes = persistedSetup?.selectedMinutes ??
    (Number(elements.value.textContent) || 10);
  let selectedModifiers = persistedSetup?.selectedModifiers ??
    readInitialModifiers();
  let lightDaysRemaining = persistedSetup?.lightDaysRemaining ?? 3;
  let selectionChanged = false;
  let startQueued = false;
  let handlers = null;
  let feedbackTimer = null;
  let activeWorkoutSetup = false;

  elements.decrease.addEventListener("click", () => stepDuration(-1));
  elements.increase.addEventListener("click", () => stepDuration(1));
  elements.range.addEventListener("input", () => {
    selectDurationByIndex(Number(elements.range.value));
  });
  elements.begin.addEventListener("click", requestStart);
  elements.upperBodyClothing.addEventListener("click", () =>
    toggleModifier("upperBodyClothing"));
  elements.hardFloor?.addEventListener("click", () =>
    toggleModifier("hardFloor"));
  elements.insect.addEventListener("click", () => toggleModifier("insect"));
  elements.silence.addEventListener("click", () => toggleModifier("silence"));
  elements.light.addEventListener("click", () => toggleModifier("light"));
  elements.wall.addEventListener("click", cycleWallEquipment);
  elements.mirror.addEventListener("click", cycleMirrorEquipment);

  renderDuration();
  renderModifiers();
  performance.mark?.("flux-controls-ready");

  const controller = {
    get selectedMinutes() {
      return selectedMinutes;
    },
    get selectedModifiers() {
      return selectedModifiers;
    },
    get selectionChanged() {
      return selectionChanged;
    },
    get lightDaysRemaining() {
      return lightDaysRemaining;
    },
    get startQueued() {
      return startQueued;
    },
    connect(nextHandlers) {
      handlers = nextHandlers;
      notifySelection(false);
      if (startQueued) {
        handlers?.startRequested?.();
      }
    },
    setSelection(minutes, modifiers) {
      if (!durationOptions.includes(minutes) || !Number.isInteger(modifiers)) {
        return;
      }
      selectedMinutes = minutes;
      selectedModifiers = modifiers;
      renderDuration();
      renderModifiers();
      notifySelection(false);
    },
    setLightDaysRemaining(daysRemaining) {
      if (!Number.isInteger(daysRemaining) ||
          daysRemaining < 0 || daysRemaining >= 4) {
        return;
      }
      if (lightDaysRemaining === daysRemaining) {
        return;
      }
      lightDaysRemaining = daysRemaining;
      renderModifiers();
    },
    setActiveWorkoutSetup(enabled) {
      activeWorkoutSetup = enabled === true;
      document.getElementById("duration-screen")?.classList.toggle(
        "active-workout-setup",
        activeWorkoutSetup,
      );
      renderDuration();
    },
    markReady() {
      if (!startQueued) {
        elements.begin.disabled = false;
      }
    },
    consumeStartRequest() {
      const queued = startQueued;
      startQueued = false;
      return queued;
    },
    cancelStartRequest() {
      startQueued = false;
      elements.begin.disabled = false;
    },
    fail(message) {
      startQueued = false;
      elements.begin.disabled = true;
      elements.feedback.classList.remove("show");
      elements.feedback.hidden = false;
      elements.feedback.textContent = message;
      if (elements.status) {
        elements.status.textContent = message;
      }
    },
  };
  window.fluxStartupControls = controller;

  function readInitialModifiers() {
    let modifiers = 0;
    for (const [name, element] of [
      ["upperBodyClothing", elements.upperBodyClothing],
      ["hardFloor", elements.hardFloor],
      ["insect", elements.insect],
      ["silence", elements.silence],
      ["light", elements.light],
    ]) {
      if (element?.getAttribute("aria-pressed") === "true") {
        modifiers |= modifierFlags[name];
      }
    }
    const mirrorEquipment = elements.mirror.dataset.mirrorEquipment;
    if (mirrorEquipment === "compact") {
      modifiers |= modifierFlags.mirror;
    } else if (mirrorEquipment === "tall") {
      modifiers |= modifierFlags.mirror | modifierFlags.tallMirror;
    }
    const wallEquipment = elements.wall.dataset.wallEquipment;
    if (wallEquipment === "soles-stay-off") {
      modifiers |= modifierFlags.wall;
    } else if (wallEquipment === "soles-may-touch") {
      modifiers |= modifierFlags.wall | modifierFlags.soleWallContact;
    }
    return modifiers;
  }

  function readPersistedSetup() {
    try {
      const raw = JSON.parse(localStorage.getItem(storageKey));
      if (!raw || typeof raw !== "object") {
        return null;
      }
      const selectedMinutes = durationOptions.includes(raw.lastWorkoutMinutes)
        ? raw.lastWorkoutMinutes
        : 10;
      const storedModifiers = Number.isInteger(raw.lastWorkoutModifiers)
        ? raw.lastWorkoutModifiers & ~modifierFlags.light
        : readInitialModifiers();
      const lightDaysRemaining = getTrainingDaysUntilLightWorkout(raw);
      return {
        selectedMinutes,
        selectedModifiers: isLightWorkoutDue(raw)
          ? storedModifiers | modifierFlags.light
          : storedModifiers,
        lightDaysRemaining,
      };
    } catch {
      return null;
    }
  }

  function isLightWorkoutDue(state) {
    return getTrainingDaysUntilLightWorkout(state) === 0;
  }

  function getTrainingDaysUntilLightWorkout(state) {
    const today = localCalendarDayNumber(Date.now());
    const completedDays = new Set((Array.isArray(state.workoutHistory)
      ? state.workoutHistory
      : [])
      .filter((session) => session?.status === "Completed")
      .map((session) => localCalendarDayNumber(
        Number(session.startedAtUnixMilliseconds) > 0
          ? session.startedAtUnixMilliseconds
          : session.endedAtUnixMilliseconds,
      ))
      .filter((day) => day !== null));
    for (const timestamp of Array.isArray(
      state.legacyCompletedTrainingDayUnixMilliseconds,
    ) ? state.legacyCompletedTrainingDayUnixMilliseconds : []) {
      const day = localCalendarDayNumber(timestamp);
      if (day !== null) {
        completedDays.add(day);
      }
    }
    let consecutivePriorDays = 0;
    for (let day = today - 1; completedDays.has(day); day -= 1) {
      consecutivePriorDays += 1;
    }
    return 3 - consecutivePriorDays % 4;
  }

  function localCalendarDayNumber(timestamp) {
    if (!Number.isSafeInteger(timestamp) || timestamp <= 0) {
      return null;
    }
    const localTime = new Date(timestamp);
    if (Number.isNaN(localTime.getTime())) {
      return null;
    }
    return Math.trunc(Date.UTC(
      localTime.getFullYear(),
      localTime.getMonth(),
      localTime.getDate(),
    ) / 86_400_000);
  }

  function stepDuration(direction) {
    if (activeWorkoutSetup) {
      return;
    }
    const index = durationOptions.indexOf(selectedMinutes);
    selectDurationByIndex(Math.max(
      0,
      Math.min(durationOptions.length - 1, index + direction),
    ));
  }

  function selectDurationByIndex(index) {
    if (activeWorkoutSetup) {
      return;
    }
    const minutes = durationOptions[index];
    if (minutes === undefined || minutes === selectedMinutes) {
      return;
    }
    selectedMinutes = minutes;
    selectionChanged = true;
    renderDuration(true);
    notifySelection(true);
  }

  function renderDuration(animate = false) {
    const index = durationOptions.indexOf(selectedMinutes);
    const progress = `${(index / (durationOptions.length - 1)) * 100}%`;
    elements.value.value = String(selectedMinutes);
    elements.value.textContent = String(selectedMinutes);
    elements.value.setAttribute(
      "aria-label",
      `${selectedMinutes} minutes selected`,
    );
    elements.range.value = String(index);
    elements.range.style.setProperty("--range-progress", progress);
    elements.range.setAttribute(
      "aria-valuetext",
      `${selectedMinutes} minutes. Options: ${durationOptions.join(", ")} minutes`,
    );
    elements.decrease.disabled = activeWorkoutSetup || index === 0;
    elements.increase.disabled = activeWorkoutSetup ||
      index === durationOptions.length - 1;
    elements.range.disabled = activeWorkoutSetup;
    elements.begin.setAttribute(
      "aria-label",
      activeWorkoutSetup
        ? "Resume workout"
        : `Start a ${selectedMinutes} minute workout`,
    );
    elements.labels.forEach((label, labelIndex) => {
      label.classList.toggle("selected", labelIndex === index);
    });
    if (animate) {
      elements.dial.classList.remove("pulse");
      requestAnimationFrame(() => elements.dial.classList.add("pulse"));
    }
  }

  function toggleModifier(name) {
    const flag = modifierFlags[name];
    selectedModifiers ^= flag;
    selectionChanged = true;
    renderModifiers();
    showFeedback(modifierFeedbackLabel(name));
    notifySelection(true);
  }

  function cycleMirrorEquipment() {
    const hasMirror = (selectedModifiers & modifierFlags.mirror) !== 0;
    const hasTallMirror = (selectedModifiers & modifierFlags.tallMirror) !== 0;
    selectedModifiers &= ~(modifierFlags.mirror | modifierFlags.tallMirror);
    if (!hasMirror) {
      selectedModifiers |= modifierFlags.mirror | modifierFlags.tallMirror;
    } else if (hasTallMirror) {
      selectedModifiers |= modifierFlags.mirror;
    }
    selectionChanged = true;
    renderModifiers();
    showFeedback(mirrorFeedbackLabel());
    notifySelection(true);
  }

  function cycleWallEquipment() {
    const hasWall = (selectedModifiers & modifierFlags.wall) !== 0;
    const solesMayTouch =
      (selectedModifiers & modifierFlags.soleWallContact) !== 0;
    selectedModifiers &=
      ~(modifierFlags.wall | modifierFlags.soleWallContact);
    if (!hasWall) {
      selectedModifiers |=
        modifierFlags.wall | modifierFlags.soleWallContact;
    } else if (solesMayTouch) {
      selectedModifiers |= modifierFlags.wall;
    }
    selectionChanged = true;
    renderModifiers();
    showFeedback(wallFeedbackLabel());
    notifySelection(true);
  }

  function renderModifiers() {
    renderBinaryModifier(elements.upperBodyClothing, "upperBodyClothing");
    renderBinaryModifier(elements.hardFloor, "hardFloor");
    renderBinaryModifier(elements.insect, "insect");
    renderBinaryModifier(elements.silence, "silence");
    renderBinaryModifier(elements.light, "light");

    const hasWall = (selectedModifiers & modifierFlags.wall) !== 0;
    const solesMayTouch =
      (selectedModifiers & modifierFlags.soleWallContact) !== 0;
    const wallEquipment = !hasWall
      ? "none"
      : solesMayTouch
        ? "soles-may-touch"
        : "soles-stay-off";
    elements.wall.setAttribute("aria-pressed", String(hasWall));
    elements.wall.dataset.wallEquipment = wallEquipment;
    elements.wall.setAttribute("title", wallFeedbackLabel());
    elements.wall.setAttribute(
      "aria-label",
      wallEquipment === "none"
        ? "Wall equipment: no wall available"
        : wallEquipment === "soles-stay-off"
          ? "Wall equipment: wall available; no feet on wall"
          : "Wall equipment: wall available; feet on wall allowed",
    );

    const hasMirror = (selectedModifiers & modifierFlags.mirror) !== 0;
    const hasTallMirror = (selectedModifiers & modifierFlags.tallMirror) !== 0;
    const equipment = !hasMirror ? "none" : hasTallMirror ? "tall" : "compact";
    elements.mirror.setAttribute("aria-pressed", String(hasMirror));
    elements.mirror.dataset.mirrorEquipment = equipment;
    elements.mirror.setAttribute("title", mirrorFeedbackLabel());
    elements.mirror.setAttribute(
      "aria-label",
      equipment === "none"
        ? "Mirror equipment: no mirror available"
        : `Mirror equipment: ${equipment} mirror available`,
    );
  }

  function renderBinaryModifier(element, name) {
    if (!element) {
      return;
    }
    const enabled = (selectedModifiers & modifierFlags[name]) !== 0;
    element.setAttribute("aria-pressed", String(enabled));
    element.setAttribute("title", modifierFeedbackLabel(name));
    if (name === "hardFloor") {
      element.dataset.hardFloor = enabled ? "hard" : "soft";
      element.setAttribute(
        "aria-label",
        enabled
          ? "Floor surface: hard and slippery floor"
          : "Floor surface: stable soft floor",
      );
    }
    if (name === "upperBodyClothing") {
      element.setAttribute(
        "aria-label",
        enabled
          ? "Upper-body clothing: worn"
          : "Upper-body clothing: not worn",
      );
    }
    if (name === "silence") {
      element.setAttribute(
        "aria-label",
        enabled
          ? "Quiet exercise filter: quiet exercises only"
          : "Quiet exercise filter: noisy exercises allowed",
      );
    }
    if (name === "light") {
      elements.lightCountdown.textContent = String(lightDaysRemaining);
      elements.lightCountdown.hidden = enabled;
      const scheduleDescription = lightDaysRemaining === 0
        ? "Automatic light mode is due today."
        : `${lightDaysRemaining} training day${lightDaysRemaining === 1
          ? ""
          : "s"} until automatic light mode.`;
      element.setAttribute(
        "aria-label",
        enabled
          ? "Workout intensity: light workout"
          : `Workout intensity: regular workout. ${scheduleDescription}`,
      );
    }
  }

  function modifierFeedbackLabel(name) {
    const enabled = (selectedModifiers & modifierFlags[name]) !== 0;
    if (name === "upperBodyClothing") {
      return `upper-body clothing ${enabled ? "ON" : "OFF"}`;
    }
    if (name === "hardFloor") {
      return `hard floor ${enabled ? "ON" : "OFF"}`;
    }
    if (name === "insect") {
      return `insect mode ${enabled ? "ON" : "OFF"}`;
    }
    if (name === "light") {
      return `light mode ${enabled ? "ON" : "OFF"}`;
    }
    return enabled ? "noisy exercises DISABLED" : "noisy exercises ENABLED";
  }

  function wallFeedbackLabel() {
    if ((selectedModifiers & modifierFlags.wall) === 0) {
      return "equipment OFF: wall";
    }
    return (selectedModifiers & modifierFlags.soleWallContact) !== 0
      ? "equipment ON: wall"
      : "equipment ON: wall · no feet on wall";
  }

  function mirrorFeedbackLabel() {
    if ((selectedModifiers & modifierFlags.mirror) === 0) {
      return "equipment OFF: mirror";
    }
    return (selectedModifiers & modifierFlags.tallMirror) !== 0
      ? "equipment ON: tall mirror"
      : "equipment ON: compact mirror";
  }

  function showFeedback(message) {
    clearTimeout(feedbackTimer);
    elements.feedback.classList.remove("show");
    elements.feedback.hidden = false;
    elements.feedback.textContent = message;
    void elements.feedback.offsetWidth;
    elements.feedback.classList.add("show");
    feedbackTimer = setTimeout(() => {
      elements.feedback.classList.remove("show");
      elements.feedback.hidden = true;
      feedbackTimer = null;
    }, feedbackDurationMs);
  }

  function notifySelection(userInitiated) {
    handlers?.selectionChanged?.(
      { selectedMinutes, selectedModifiers },
      userInitiated,
    );
  }

  function requestStart() {
    if (startQueued) {
      return;
    }
    startQueued = true;
    elements.begin.disabled = true;
    handlers?.startRequested?.();
  }
})();
