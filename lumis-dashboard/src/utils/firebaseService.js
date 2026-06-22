import config from "../config/config";

const PROJECT_ID = "lumiplay-c871d";
const BASE_URL   = `https:

function safeId(name, registeredAt = null) {
  const clean = (name || "unknown").trim().toLowerCase().replace(/ /g, "_");
  if (registeredAt) {
    const suffix = registeredAt.replace(/-/g, "").substring(0, 8);
    return clean + "_" + suffix;
  }
  return clean;
}

export async function saveReportToFirebase(childName, content, lang) {
  const encoded  = encodeURIComponent(safeId(childName));
  const reportId = "report_" + Date.now();
  const url      = `${BASE_URL}/players/${encoded}/reports/${reportId}`;
  const now      = new Date();

  const body = {
    fields: {
      childName: { stringValue: childName },
      content:   { stringValue: content   },
      lang:      { stringValue: lang      },
      date:      { stringValue: now.toLocaleDateString("en-GB", { day:"2-digit", month:"short", year:"numeric" }) },
      time:      { stringValue: now.toLocaleTimeString("en-GB", { hour:"2-digit", minute:"2-digit" }) },
      createdAt: { stringValue: now.toISOString() },
    }
  };

  const res = await fetch(url, {
    method:  "PATCH",
    headers: { "Content-Type": "application/json" },
    body:    JSON.stringify(body),
  });

  if (!res.ok) {
    console.error("[Firebase] Failed to save report:", res.status);
    return null;
  }
  return { id: reportId, childName, content, lang, date: body.fields.date.stringValue, time: body.fields.time.stringValue };
}

export async function fetchReportsFromFirebase(childName) {
  const encoded = encodeURIComponent(safeId(childName));
  const url     = `${BASE_URL}/players/${encoded}/reports`;
  const res     = await fetch(url);
  if (!res.ok) return [];

  const data = await res.json();
  const docs = data.documents || [];

  return docs.map(doc => {
    const f  = doc.fields || {};
    const id = docId(doc.name);
    return {
      id:        id,
      childName: getString(f.childName) || childName,
      content:   getString(f.content)   || "",
      lang:      getString(f.lang)      || "en",
      date:      getString(f.date)      || "",
      time:      getString(f.time)      || "",
      createdAt: getString(f.createdAt) || "",
    };
  }).sort((a, b) => b.createdAt.localeCompare(a.createdAt));
}

export async function deleteReportFromFirebase(childName, reportId) {
  const encoded = encodeURIComponent(safeId(childName));
  const url     = `${BASE_URL}/players/${encoded}/reports/${reportId}`;
  const res     = await fetch(url, { method: "DELETE" });
  return res.ok;
}

export async function fetchAllChildren() {

  const url = `https:

  const body = {
    structuredQuery: {
      from: [{ collectionId: "players" }],
      orderBy: [{ field: { fieldPath: "playerName" }, direction: "ASCENDING" }],
    },
  };

  console.log("[Firebase] Running query for players...");
  const res = await fetch(url, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });

  console.log("[Firebase] Response status:", res.status);
  if (!res.ok) {
    const errText = await res.text();
    throw new Error("Firestore query error: " + res.status + " " + errText);
  }

  const results = await res.json();
  console.log("[Firebase] Raw results:", JSON.stringify(results).slice(0, 600));

  const docs = (results || [])
    .filter((r) => r.document)
    .map((r) => r.document);

  console.log("[Firebase] Documents found:", docs.length);

  const children = docs.map((doc) => {
    const f = doc.fields || {};
    const name = getString(f.playerName) || docId(doc.name);
    return {
      name,
      gender:          getString(f.gender) || getString(f.Gender) || "unknown",
      registeredAt:    getString(f.registeredAt) || "",
      lastSession:     "",
      files:           [],
      g1Count:         getInt(f.g1Count),
      g2Count:         getInt(f.g2Count),
      emTotalAttempts: getInt(f.emTotalAttempts),
      emCorrect:       getInt(f.emCorrect),
      emResults:       getString(f.emResults) || "",
      emImprovements:  getString(f.emImprovements) || "",
    };
  }).sort((a, b) => a.name.localeCompare(b.name));

  await Promise.all(children.map(async (child) => {
    try {
      const sessions = await fetchSessionCounts(child.name);
      child.g1Count = sessions.g1Count;
      child.g2Count = sessions.g2Count;
      child.emCount = sessions.emCount;
      child.lastSession = sessions.lastTimestamp;
    } catch (e) {

    }
  }));

  return children;
}

async function querySessionsByPlayerName(playerName) {
  const url = `https:
  const body = {
    structuredQuery: {
      from: [{ collectionId: "sessions", allDescendants: true }],
      where: {
        fieldFilter: {
          field: { fieldPath: "playerName" },
          op: "EQUAL",
          value: { stringValue: playerName },
        },
      },
    },
  };
  const res = await fetch(url, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
  if (!res.ok) return [];
  const results = await res.json();
  return (results || []).filter((r) => r.document).map((r) => r.document);
}

async function fetchSessionCounts(playerName) {
  const encoded = encodeURIComponent(safeId(playerName));
  const url = `${BASE_URL}/players/${encoded}/sessions`;
  const res = await fetch(url);

  let docs = [];
  if (res.ok) {
    const data = await res.json();
    docs = data.documents || [];
  }
  if (docs.length === 0) {
    docs = await querySessionsByPlayerName(playerName);
  }

  let g1Count = 0, g2Count = 0, emCount = 0, lastTimestamp = "";
  docs.forEach((doc) => {
    const id = docId(doc.name);
    if (id.startsWith("game1_")) g1Count++;
    if (id.startsWith("game2_")) g2Count++;
    if (id.startsWith("em_"))    emCount++;
    const ts = doc.fields?.timestamp?.stringValue || "";
    if (ts > lastTimestamp) lastTimestamp = ts;
  });

  return { g1Count, g2Count, emCount, lastTimestamp };
}

const ARABIC_TO_ENGLISH_EMOTION = {
  "سَعِيد": "Happy",   "سعيد": "Happy",
  "حَزِين": "Sad",     "حزين": "Sad",
  "خَائِف": "Fear",    "خائف": "Fear",
  "غَاضِب": "Angry",   "غاضب": "Angry",
  "مُتَفَاجِئ": "Surprised", "متفاجئ": "Surprised",
  "مُشْمَئِزّ": "Disgusted", "مشمئز": "Disgusted",
  "مُحَايِد": "Neutral",    "محايد": "Neutral",
};

function toEnglishEmotion(name) {
  if (!name) return name;
  return ARABIC_TO_ENGLISH_EMOTION[name.trim()] || name;
}

export async function fetchChildSessions(playerName) {
  const encoded = encodeURIComponent(safeId(playerName));
  const url     = `${BASE_URL}/players/${encoded}/sessions`;
  const res     = await fetch(url);

  let docs = [];
  if (res.ok) {
    const data = await res.json();
    docs = data.documents || [];
  }

  if (docs.length === 0) {
    console.log(`[Firebase] Direct path empty for "${playerName}", trying field query...`);
    docs = await querySessionsByPlayerName(playerName);
    console.log(`[Firebase] Field query returned ${docs.length} docs for "${playerName}"`);
  }

  const g1Sessions = [];
  const g2Sessions = [];

  docs.forEach((doc) => {
    const id = docId(doc.name);
    const f  = doc.fields || {};
    if (id.startsWith("game1_")) g1Sessions.push(parseGame1Doc(f));
    else if (id.startsWith("game2_")) g2Sessions.push(parseGame2Doc(f));
  });

  const byTime = (a, b) => (a.timestamp || "").localeCompare(b.timestamp || "");
  g1Sessions.sort(byTime);
  g2Sessions.sort(byTime);

  const emDocs = docs.filter(doc => docId(doc.name).startsWith("em_"));

  return { g1Sessions, g2Sessions, emDocs };
}

export function buildEmStatsFromDocs(emDocs) {
  if (!emDocs || emDocs.length === 0) return null;

  let totalAttempts = 0;
  let correct = 0;
  const resultsRaw = [];
  const improvementsRaw = [];
  const emotionStats = {};

  emDocs.forEach(doc => {
    const f = doc.fields || {};
    const target   = getString(f.targetEmotion)  || "";
    const detected = getString(f.detectedEmotion) || "";
    const matched  = getBool(f.matched);

    const conf     = Math.round(getDouble(f.confidence));

    totalAttempts++;
    if (matched) correct++;

    const entry = matched
      ? `${target}: correct (${conf}%)`
      : `${target}: incorrect → ${detected} detected (${conf}%)`;
    resultsRaw.push(entry);

    if (matched) {
      const hadWrong = resultsRaw.some(r => r.startsWith(target + ": incorrect"));
      if (hadWrong) improvementsRaw.push(target);
    }

    if (!emotionStats[target])
      emotionStats[target] = { attempts: 0, correct: 0, totalConf: 0 };
    emotionStats[target].attempts++;
    if (matched) emotionStats[target].correct++;
    emotionStats[target].totalConf += conf;
  });

  const perEmotion = Object.entries(emotionStats).map(([emotion, s]) => ({
    emotion,
    attempts:  s.attempts,
    correct:   s.correct,
    incorrect: s.attempts - s.correct,
    accuracy:  Math.round((s.correct / s.attempts) * 100),
    avgConf:   s.attempts > 0 ? Math.round(s.totalConf / s.attempts) : 0,
  })).sort((a, b) => b.attempts - a.attempts);

  const totalConf = Object.values(emotionStats).reduce((sum, s) => sum + s.totalConf, 0);
  const avgConfOverall = totalAttempts > 0 ? Math.round(totalConf / totalAttempts) : 0;

  return {
    totalAttempts,
    correct,
    incorrect:      totalAttempts - correct,
    avgConfOverall,
    results:        resultsRaw,
    improvements:   [...new Set(improvementsRaw)],
    perEmotion,
  };
}

export async function fetchEmotionMirror(playerName) {
  const encoded = encodeURIComponent(safeId(playerName));
  const url     = `${BASE_URL}/players/${encoded}/sessions`;
  const res     = await fetch(url);

  if (!res.ok) return null;

  const data = await res.json();
  const docs = data.documents || [];

  const emDocs = docs.filter(doc => docId(doc.name).startsWith("em_"));
  if (emDocs.length === 0) return null;

  let totalAttempts = 0;
  let correct = 0;
  const resultsRaw = [];
  const improvementsRaw = [];
  const emotionStats = {};

  emDocs.forEach(doc => {
    const f = doc.fields || {};
    const target   = getString(f.targetEmotion)   || "";
    const detected = getString(f.detectedEmotion)  || "";
    const matched  = getBool(f.matched);
    const conf     = getInt(f.confidence);

    totalAttempts++;
    if (matched) correct++;

    const entry = matched
      ? `${target}: correct (${conf}%)`
      : `${target}: incorrect → ${detected} detected (${conf}%)`;
    resultsRaw.push(entry);

    if (matched) {
      const hadWrong = resultsRaw.some(r =>
        r.startsWith(target + ": incorrect"));
      if (hadWrong) improvementsRaw.push(target);
    }

    if (!emotionStats[target])
      emotionStats[target] = { attempts: 0, correct: 0, totalConf: 0 };
    emotionStats[target].attempts++;
    if (matched) emotionStats[target].correct++;
    emotionStats[target].totalConf += conf;
  });

  const perEmotion = Object.entries(emotionStats).map(([emotion, s]) => ({
    emotion,
    attempts:  s.attempts,
    correct:   s.correct,
    incorrect: s.attempts - s.correct,
    accuracy:  Math.round((s.correct / s.attempts) * 100),
    avgConf:   Math.round(s.totalConf / s.attempts),
  })).sort((a, b) => b.attempts - a.attempts);

  return {
    totalAttempts,
    correct,
    incorrect:    totalAttempts - correct,
    results:      resultsRaw,
    improvements: [...new Set(improvementsRaw)],
    perEmotion,
  };
}

function parseGame1Doc(f) {

  const rounds = [];
  const roundsArray = f.rounds?.arrayValue?.values || [];
  roundsArray.forEach((item) => {
    const m = item.mapValue?.fields || {};
    rounds.push({
      roundNumber:           getInt(m.roundNumber),
      fruitsShown:           getInt(m.fruitsShown),
      orderRequired:         m.orderRequired?.booleanValue === true,
      correctTaps:           getInt(m.correctTaps),
      wrongTaps:             getInt(m.wrongTaps),
      hintsUsed:             getInt(m.hintsUsed),
      hint1Used:             getInt(m.hint1Used),
      hint2Used:             getInt(m.hint2Used),
      passed:                m.passed?.booleanValue === true,
      responseTimeSec:       getDouble(m.responseTimeSec),
      adaptiveStateSnapshot: getString(m.adaptiveStateSnapshot) || "",
    });
  });

  return {
    playerName:        getString(f.playerName),
    sessionId:         getString(f.sessionId),
    timestamp:         getString(f.timestamp),
    currentLevel:      getInt(f.currentLevel),
    attemptNumber:     getInt(f.attemptNumber),
    totalCorrect:      getInt(f.totalCorrect),
    totalWrong:        getInt(f.totalWrong),
    avgResponseTimeSec: getDouble(f.avgResponseTimeSec),
    hintsUsed:         getInt(f.hintsUsed),
    hint1Used:         getInt(f.hint1Used),
    hint2Used:         getInt(f.hint2Used),
    levelPassed:       getBool(f.levelPassed),
    starsEarned:       getInt(f.starsEarned),
    finalDelay:        getDouble(f.finalDelay),
    finalGrid:         getString(f.finalGrid),
    finalFruitsR0:     getInt(f.finalFruitsR0),
    finalFruitsR1:     getInt(f.finalFruitsR1),
    adaptiveChanges:   getString(f.adaptiveChanges),
    difficultyMode:    getString(f.difficultyMode) || "adaptive",
    rounds,
  };
}

function parseGame2Doc(f) {

  const confusionPairs = [];
  const cpArray = f.confusionPairs?.arrayValue?.values || [];
  cpArray.forEach((item) => {
    const m = item.mapValue?.fields || {};
    confusionPairs.push({
      correctEmotion:  toEnglishEmotion(getString(m.correctEmotion)),
      selectedEmotion: toEnglishEmotion(getString(m.selectedEmotion)),
      count:           getInt(m.count),
    });
  });

  return {
    playerName:        getString(f.playerName),
    sessionId:         getString(f.sessionId),
    timestamp:         getString(f.timestamp),
    currentLevel:      getInt(f.currentLevel),
    attemptNumber:     getInt(f.attemptNumber),
    emotionTested:     getString(f.emotionTested),
    correctAnswers:    getInt(f.correctAnswers),
    wrongAnswers:      getInt(f.wrongAnswers),
    avgResponseTimeSec: getDouble(f.avgResponseTimeSec),
    hintsUsed:         getInt(f.hintsUsed),
    hint1Used:         getInt(f.hint1Used),
    hint2Used:         getInt(f.hint2Used),
    levelPassed:       getBool(f.levelPassed),
    starsEarned:       getInt(f.starsEarned),
    confusionPairs,
  };
}

export async function uploadSessionsToFirebase(playerName, g1Sessions, g2Sessions) {
  const safePlayer = playerName.trim().toLowerCase().replace(/ /g, "_");
  const base = `https:

  await fetch(`${base}/players/${encodeURIComponent(safePlayer)}`, {
    method: "PATCH",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      fields: {
        playerName:    { stringValue: playerName },
        registeredAt:  { stringValue: new Date().toISOString().slice(0, 10) },
      }
    }),
  });

  for (const s of g1Sessions) {
    const docName = `game1_L${s.currentLevel}_A${s.attemptNumber || 1}`;
    const url = `${base}/players/${encodeURIComponent(safePlayer)}/sessions/${encodeURIComponent(docName)}`;

    const roundsValues = (s.rounds || []).map(r => ({
      mapValue: { fields: {
        roundNumber:    { integerValue: String(r.roundNumber  || 0) },
        fruitsShown:    { integerValue: String(r.fruitsShown  || 1) },
        correctTaps:    { integerValue: String(r.correctTaps  || 0) },
        wrongTaps:      { integerValue: String(r.wrongTaps    || 0) },
        hintsUsed:      { integerValue: String(r.hintsUsed    || 0) },
        hint1Used:      { integerValue: String(r.hint1Used    || 0) },
        hint2Used:      { integerValue: String(r.hint2Used    || 0) },
        passed:         { booleanValue: r.passed || false },
        responseTimeSec:{ doubleValue:  r.responseTimeSec || 0 },
        orderRequired:  { booleanValue: r.orderRequired || false },
      }}
    }));

    await fetch(url, {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ fields: {
        playerName:       { stringValue: s.playerName || playerName },
        sessionId:        { stringValue: s.sessionId  || "" },
        timestamp:        { stringValue: s.timestamp  || "" },
        currentLevel:     { integerValue: String(s.currentLevel  || 0) },
        attemptNumber:    { integerValue: String(s.attemptNumber || 1) },
        totalCorrect:     { integerValue: String(s.totalCorrect  || 0) },
        totalWrong:       { integerValue: String(s.totalWrong    || 0) },
        avgResponseTimeSec: { doubleValue: s.avgResponseTimeSec || 0 },
        hintsUsed:        { integerValue: String(s.hintsUsed    || 0) },
        hint1Used:        { integerValue: String(s.hint1Used    || 0) },
        hint2Used:        { integerValue: String(s.hint2Used    || 0) },
        levelPassed:      { booleanValue: s.levelPassed || false },
        starsEarned:      { integerValue: String(s.starsEarned  || 0) },
        finalDelay:       { doubleValue:  s.finalDelay  || 0 },
        finalGrid:        { stringValue:  s.finalGrid   || "" },
        finalFruitsR0:    { integerValue: String(s.finalFruitsR0 || 0) },
        finalFruitsR1:    { integerValue: String(s.finalFruitsR1 || 0) },
        adaptiveChanges:  { stringValue:  s.adaptiveChanges || "" },
        difficultyMode:   { stringValue:  s.difficultyMode  || "adaptive" },
        rounds:           { arrayValue: { values: roundsValues } },
      }}),
    });
  }

  for (const s of g2Sessions) {
    const docName = `game2_L${s.currentLevel}_A${s.attemptNumber || 1}`;
    const url = `${base}/players/${encodeURIComponent(safePlayer)}/sessions/${encodeURIComponent(docName)}`;

    const cpValues = (s.confusionPairs || []).map(p => ({
      mapValue: { fields: {
        correctEmotion:  { stringValue: p.correctEmotion  || "" },
        selectedEmotion: { stringValue: p.selectedEmotion || "" },
        count:           { integerValue: String(p.count   || 1) },
      }}
    }));

    await fetch(url, {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ fields: {
        playerName:        { stringValue: s.playerName || playerName },
        sessionId:         { stringValue: s.sessionId  || "" },
        timestamp:         { stringValue: s.timestamp  || "" },
        currentLevel:      { integerValue: String(s.currentLevel  || 0) },
        attemptNumber:     { integerValue: String(s.attemptNumber || 1) },
        emotionTested:     { stringValue:  s.emotionTested || "" },
        correctAnswers:    { integerValue: String(s.correctAnswers || 0) },
        wrongAnswers:      { integerValue: String(s.wrongAnswers   || 0) },
        avgResponseTimeSec:{ doubleValue:  s.avgResponseTimeSec || 0 },
        hintsUsed:         { integerValue: String(s.hintsUsed  || 0) },
        hint1Used:         { integerValue: String(s.hint1Used  || 0) },
        hint2Used:         { integerValue: String(s.hint2Used  || 0) },
        levelPassed:       { booleanValue: s.levelPassed || false },
        starsEarned:       { integerValue: String(s.starsEarned || 0) },
        confusionPairs:    { arrayValue: { values: cpValues } },
      }}),
    });
  }

  console.log(`[Firebase] Uploaded ${g1Sessions.length} G1 + ${g2Sessions.length} G2 sessions for ${playerName}`);
}

function getString(field) {
  return field?.stringValue || "";
}

function getInt(field) {
  const v = field?.integerValue;
  return v !== undefined ? parseInt(v, 10) : 0;
}

function getDouble(field) {
  const v = field?.doubleValue;
  return v !== undefined ? parseFloat(v) : 0;
}

function getBool(field) {
  return field?.booleanValue === true;
}

function docId(fullPath) {
  const parts = fullPath.split("/");
  return parts[parts.length - 1];
}