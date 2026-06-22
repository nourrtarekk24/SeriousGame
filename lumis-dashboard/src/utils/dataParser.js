export function groupFilesByChild(fileList) {
  const children = {};

  Array.from(fileList).forEach((file) => {
    const path  = file.webkitRelativePath || file.name;
    const parts = path.split("/");
    const fileName = parts[parts.length - 1];

    if (!fileName.endsWith(".json")) return;
    if (!fileName.startsWith("game1_") && !fileName.startsWith("game2_")) return;

    if (parts.length < 2) return;

    const sessionFolder = parts[parts.length - 2];
    const childName = extractChildName(sessionFolder);

    if (!children[childName]) {
      children[childName] = { name: childName, files: [], lastSession: "" };
    }
    children[childName].files.push(file);

    const datePart = sessionFolder.split("_").slice(-2).join(" ");
    if (datePart > children[childName].lastSession)
      children[childName].lastSession = datePart;
  });

  return Object.values(children).sort((a, b) => a.name.localeCompare(b.name));
}

function extractChildName(folderName) {
  const parts = folderName.split("_");
  if (parts.length <= 2) return folderName;
  return parts.slice(0, parts.length - 2).join(" ");
}

function readFile(file) {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload  = (e) => resolve(e.target.result);
    reader.onerror = reject;
    reader.readAsText(file);
  });
}

export async function parseChildData(child) {
  const g1Sessions = [];
  const g2Sessions = [];

  for (const file of child.files) {
    try {
      const text = await readFile(file);
      const data = JSON.parse(text);
      if (file.name.startsWith("game1_")) g1Sessions.push(data);
      if (file.name.startsWith("game2_")) g2Sessions.push(data);
    } catch (e) {
      console.warn("Could not parse:", file.name);
    }
  }

  const byTime = (a, b) => (a.timestamp || "").localeCompare(b.timestamp || "");
  g1Sessions.sort(byTime);
  g2Sessions.sort(byTime);

  return {
    name:          child.name,
    lastSession:   child.lastSession,
    totalSessions: g1Sessions.length + g2Sessions.length,
    g1Sessions,
    g2Sessions,
  };
}

export function computeG1Stats(sessions) {
  if (!sessions.length) return null;

  const hintsUsed = total(sessions, (s) => s.hintsUsed || 0);

  let hint1Used = 0, hint2Used = 0;
  sessions.forEach(s => {
    (s.rounds || []).forEach(r => {
      const h = r.hintsUsed || 0;
      if (h >= 1) hint1Used++;
      if (h >= 2) hint2Used++;
    });
  });

  if (hint1Used === 0 && hint2Used === 0) {
    hint1Used = total(sessions, (s) => s.hint1Used || 0);
    hint2Used = total(sessions, (s) => s.hint2Used || 0);
  }
  const avgRT     = average(sessions, (s) => s.avgResponseTimeSec || 0);

  let totalRounds   = 0;
  let perfectRounds = 0;
  let hasRoundData  = false;

  sessions.forEach(s => {
    if (s.rounds && s.rounds.length > 0) {
      hasRoundData = true;
      s.rounds.forEach(r => {
        totalRounds++;
        if ((r.wrongTaps || 0) === 0) perfectRounds++;
      });
    }
  });

  if (!hasRoundData) {
    sessions.forEach(s => {
      totalRounds++;
      if ((s.totalWrong || 0) === 0) perfectRounds++;
    });
  }

  const perfectionRate = totalRounds > 0 ? Math.round((perfectRounds / totalRounds) * 100) : 0;

  const distinctLevelsPassed = [0, 1, 2, 3].filter(lvl =>
    sessions.some(s => s.currentLevel === lvl && s.levelPassed)
  ).length;

  const first   = sessions[0];
  const last    = sessions[sessions.length - 1];
  const firstRT = first.avgResponseTimeSec  || 0;
  const lastRT  = last.avgResponseTimeSec   || 0;
  const rtTrend = Math.round((lastRT - firstRT) * 10) / 10;

  const accuracyOverTime = sessions.map((s, i) => {
    let sTotal = 0, sPerfect = 0;
    if (s.rounds && s.rounds.length > 0) {
      s.rounds.forEach(r => {
        sTotal++;
        if ((r.wrongTaps || 0) === 0) sPerfect++;
      });
    } else {
      sTotal = 1;
      sPerfect = (s.totalWrong || 0) === 0 ? 1 : 0;
    }
    return {
      label:    "S" + (i + 1),
      accuracy: sTotal > 0 ? Math.round((sPerfect / sTotal) * 100) : 0,
      date:     s.timestamp ? s.timestamp.slice(0, 10) : "",
    };
  });

  const levelStats = [0, 1, 2, 3].map(lvl => {
    const lvlSessions = sessions.filter(s => s.currentLevel === lvl);
    if (!lvlSessions.length) return { level: lvl, played: false };

    const best = lvlSessions.reduce((a, b) => {
      if ((b.starsEarned || 0) !== (a.starsEarned || 0))
        return (b.starsEarned || 0) > (a.starsEarned || 0) ? b : a;
      return (a.hintsUsed || 0) <= (b.hintsUsed || 0) ? a : b;
    });

    const attemptTrend = lvlSessions.map((s, i) => {
      const rounds = (s.rounds || []).map(r => ({
        roundNumber:  r.roundNumber || 0,
        fruitsShown:  r.fruitsShown || 1,
        wrongTaps:    r.wrongTaps   || 0,
        hints:        r.hintsUsed   || 0,
        passed:       r.passed      || false,
        responseTime: Math.round((r.responseTimeSec || 0) * 10) / 10,
      }));
      return {
        attempt:          i + 1,
        passed:           s.levelPassed     || false,
        stars:            s.starsEarned     || 0,
        hints:            s.hintsUsed       || 0,
        difficultyMode:   s.difficultyMode  || "adaptive",
        adaptiveChanges:  s.adaptiveChanges || "",
        finalGrid:        s.finalGrid       || "",
        finalDelay:       s.finalDelay      || 0,
        rounds,
      };
    });

    const gridMap = {};
    lvlSessions.forEach(s => {
      (s.rounds || []).forEach(r => {
        const key = `${s.finalGrid || "?"} · ${r.fruitsShown || 1} fruit · ${s.finalDelay || 0}s delay`;
        if (!gridMap[key]) gridMap[key] = { perfect: 0, total: 0 };
        gridMap[key].total++;
        if ((r.wrongTaps || 0) === 0) gridMap[key].perfect++;
      });
    });

    return {
      level:     lvl,
      played:    true,
      attempts:  lvlSessions.length,
      stars:     best.starsEarned || 0,
      passed:    lvlSessions.some(s => s.levelPassed),
      hints:     best.hintsUsed || 0,
      grid:      best.finalGrid   || "—",
      delay:     best.finalDelay  || 0,
      maxFruits: Math.max(...lvlSessions.map(s => Math.max(s.finalFruitsR0 || 0, s.finalFruitsR1 || 0))),
      attemptTrend,
      gridMap,
    };
  });

  const gridMapAll = {};
  sessions.forEach(s => {
    (s.rounds || []).forEach(r => {

      let grid  = s.finalGrid   || "?";
      let delay = s.finalDelay  || 0;
      if (r.adaptiveStateSnapshot) {
        const gridMatch  = r.adaptiveStateSnapshot.match(/Grid[:\s](\d+x\d+)/i);
        const delayMatch = r.adaptiveStateSnapshot.match(/Delay[:\s]([\d.]+)s/i);
        if (gridMatch)  grid  = gridMatch[1];
        if (delayMatch) delay = parseFloat(delayMatch[1]);
      }
      const fruits = r.fruitsShown || 1;
      const key = `${grid.replace("x","×")} · ${fruits} fruit${fruits > 1 ? "s" : ""} · ${delay}s delay`;
      if (!gridMapAll[key]) gridMapAll[key] = { perfect: 0, total: 0 };
      gridMapAll[key].total++;
      if ((r.wrongTaps || 0) === 0) gridMapAll[key].perfect++;
    });
  });
  const gridPerformance = Object.entries(gridMapAll)
    .map(([config, v]) => ({
      config,
      accuracy: v.total > 0 ? Math.round((v.perfect / v.total) * 100) : 0,
      attempts: v.total,
    }))
    .sort((a, b) => b.attempts - a.attempts);

  const maxFruitsReached = Math.max(0, ...sessions.map(s => Math.max(s.finalFruitsR0 || 0, s.finalFruitsR1 || 0)));
  const maxDelayReached  = Math.max(0, ...sessions.map(s => s.finalDelay || 0));

  return {
    perfectionRate,
    perfectRounds,
    totalRounds,
    hasRoundData,
    hintsUsed, hint1Used, hint2Used,
    avgRT:    Math.round(avgRT * 10) / 10,
    rtTrend,
    passed:   distinctLevelsPassed,
    accuracyOverTime,
    levelStats,
    gridPerformance,
    maxFruitsReached,
    maxDelayReached,
    sessionCount: sessions.length,
  };
}

export function computeG1StatsByMode(sessions, mode) {
  if (!sessions || !sessions.length) return null;
  const filtered = sessions.filter(s =>
    (s.difficultyMode || "adaptive").toLowerCase() === mode.toLowerCase()
  );
  if (!filtered.length) return null;
  return computeG1Stats(filtered);
}

const EMOTIONS = ["Happy", "Sad", "Fear", "Angry", "Surprised", "Disgusted"];

export function computeG2Stats(sessions) {
  if (!sessions.length) return null;

  const hintsUsed = total(sessions, (s) => s.hintsUsed || 0);

  const distinctLevelsPassed = EMOTIONS.filter((_, i) =>
    sessions.some(s => s.currentLevel === i && s.levelPassed)
  ).length;

  const sessionsPassed    = sessions.filter(s => s.levelPassed).length;
  const sessionsFirstTry  = sessions.filter(s => s.levelPassed && (s.hintsUsed || 0) === 0).length;
  const firstTryRate      = sessionsPassed > 0 ? Math.round((sessionsFirstTry / sessionsPassed) * 100) : 0;

  const avgRT = average(sessions, s => s.avgResponseTimeSec || 0);

  const first  = sessions[0];
  const last   = sessions[sessions.length - 1];
  const firstRT = first.avgResponseTimeSec || 0;
  const lastRT  = last.avgResponseTimeSec  || 0;
  const rtTrend = Math.round((lastRT - firstRT) * 10) / 10;

  const accuracyOverTime = sessions.map((s, i) => ({
    label:    "S" + (i + 1),
    accuracy: s.levelPassed
      ? ((s.hintsUsed || 0) === 0 ? 100 : 50)
      : 0,
    emotion:  s.emotionTested || "",
  }));

  const emotionStats = EMOTIONS.map((emotion, i) => {
    const lvlSessions = sessions.filter(s => s.currentLevel === i);
    if (!lvlSessions.length) return { emotion, played: false };

    const totalHints   = total(lvlSessions, s => s.hintsUsed || 0);
    const passedCount  = lvlSessions.filter(s => s.levelPassed).length;
    const avgRTEmotion = average(lvlSessions, s => s.avgResponseTimeSec || 0);

    const best = lvlSessions.reduce((a, b) =>
      (b.starsEarned || 0) > (a.starsEarned || 0) ? b : a
    );

    const attemptTrend = lvlSessions.map((s, idx) => ({
      attempt:  idx + 1,
      passed:   s.levelPassed  || false,
      stars:    s.starsEarned  || 0,
      hints:    s.hintsUsed    || 0,
      hint1:    s.hint1Used    || 0,
      hint2:    s.hint2Used    || 0,
      wrongAnswers: s.wrongAnswers || 0,
      responseTime: Math.round((s.avgResponseTimeSec || 0) * 10) / 10,
    }));

    const latestAttempt = lvlSessions[lvlSessions.length - 1];
    const latestPassed  = latestAttempt.levelPassed || false;
    const latestHints   = latestAttempt.hintsUsed   || 0;

    return {
      emotion,
      played:       true,
      attempts:     lvlSessions.length,
      passedCount,
      totalHints,
      stars:        best.starsEarned || 0,
      passed:       latestPassed,
      latestHints,
      avgRT:        Math.round(avgRTEmotion * 10) / 10,
      attemptTrend,
    };
  });

  const strongEmotions = emotionStats
    .filter(e => e.played && e.passed && e.latestHints === 0)
    .map(e => e.emotion);
  const weakEmotions = emotionStats
    .filter(e => e.played && (!e.passed || e.latestHints >= 2))
    .map(e => e.emotion);

  const arabicToEn = {
    "سَعِيد":"Happy","سعيد":"Happy","حَزِين":"Sad","حزين":"Sad",
    "خَائِف":"Fear","خائف":"Fear","غَاضِب":"Angry","غاضب":"Angry",
    "مُتَفَاجِئ":"Surprised","متفاجئ":"Surprised",
    "مُشْمَئِزّ":"Disgusted","مشمئز":"Disgusted",
    "مُحَايِد":"Neutral","محايد":"Neutral",
  };
  const toEn = (n) => arabicToEn[n?.trim()] || n;

  const confusionMap = {};
  sessions.forEach(s => {
    (s.confusionPairs || []).forEach(p => {
      const key = `${toEn(p.correctEmotion)} → ${toEn(p.selectedEmotion)}`;
      confusionMap[key] = (confusionMap[key] || 0) + (p.count || 1);
    });
  });
  const confusionPairs = Object.entries(confusionMap)
    .map(([pair, count]) => ({ pair, count }))
    .sort((a, b) => b.count - a.count);

  let hintTrendText  = "→ Stable";
  let hintTrendClass = "trend-flat";
  if (sessions.length >= 2) {
    const half     = Math.ceil(sessions.length / 2);
    const avgFirst = sessions.slice(0, half).reduce((s, x) => s + (x.hintsUsed || 0), 0) / half;
    const avgLast  = sessions.slice(half).reduce((s, x)  => s + (x.hintsUsed || 0), 0) / (sessions.length - half);
    const diff     = Math.round((avgLast - avgFirst) * 10) / 10;
    if (diff < -0.2)     { hintTrendText = "↑ Fewer hints needed";  hintTrendClass = "trend-up"; }
    else if (diff > 0.2) { hintTrendText = "↓ More hints needed";   hintTrendClass = "trend-down"; }
  }

  return {
    firstTryRate,
    sessionsFirstTry,
    sessionsPassed,
    hintsUsed,
    avgRT:          Math.round(avgRT * 10) / 10,
    hintTrendText,
    hintTrendClass,
    passed:         distinctLevelsPassed,
    accuracyOverTime,
    emotionStats,
    confusionPairs,
    strongEmotions,
    weakEmotions,
    sessionCount:   sessions.length,
  };
}

function pct(correct, totalVal) {
  return totalVal > 0 ? Math.round((correct / totalVal) * 100) : 0;
}

function total(arr, fn) {
  return arr.reduce((sum, item) => sum + fn(item), 0);
}

function average(arr, fn) {
  if (!arr.length) return 0;
  return total(arr, fn) / arr.length;
}