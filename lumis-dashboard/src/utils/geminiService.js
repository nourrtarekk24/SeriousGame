import config from "../config/config";

export async function generateReport(childName, g1Stats, g2Stats, emStats, lang = "en") {
  const prompt = buildPrompt(childName, g1Stats, g2Stats, emStats, lang);

  for (let attempt = 1; attempt <= 3; attempt++) {
    const response = await fetch(config.GEMINI_URL, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "x-goog-api-key": config.GEMINI_API_KEY,
      },
      body: JSON.stringify({
        contents: [{ parts: [{ text: prompt }] }],
        generationConfig: { maxOutputTokens: 8192, temperature: 0.3 },
      }),
    });

    if (response.ok) {
      const data = await response.json();
      return data?.candidates?.[0]?.content?.parts?.[0]?.text
        || "Report could not be generated.";
    }

    if (response.status === 503 && attempt < 3) {
      await new Promise(res => setTimeout(res, attempt * 3000));
      continue;
    }
    if (response.status === 429 && attempt < 3) {
      await new Promise(res => setTimeout(res, attempt * 5000));
      continue;
    }

    let friendlyError = "Report generation failed. Please try again.";
    try {
      const errText = await response.text();
      const parsed  = JSON.parse(errText);
      if (response.status === 503)
        friendlyError = "Gemini is currently busy. Please wait a moment and try again.";
      else if (response.status === 429)
        friendlyError = "Too many requests. Please wait 30 seconds and try again.";
      else if (response.status === 400)
        friendlyError = "Invalid request. Check your Gemini API key in config.js.";
      else if (parsed?.error?.message)
        friendlyError = parsed.error.message;
    } catch (_) {}

    throw new Error(friendlyError);
  }
}

function buildPrompt(childName, g1, g2, em, lang = "en") {
  const date = new Date().toISOString().slice(0, 10);
  const hasEM = em && em.totalAttempts > 0;
  const isArabic = lang === "ar";

  let prompt = `You are a licensed clinical neuropsychologist writing a formal session progress report.
The data comes from "Lumi Play", a therapeutic serious game for children aged 4-8 with autism or emotional difficulties.
The game has two cognitive tasks: visual working memory (Fruit Finder) and facial emotion recognition (Emotion Quest), plus an optional emotion imitation activity (Emotion Mirror).

Rules for this report:
- Write in a formal clinical tone suitable for a therapist or researcher
- Do NOT simply restate numbers — interpret what they mean clinically
- Every observation must reference actual data values
- Use plain text only. No markdown, no asterisks, no dashes at line start
- Number each section exactly as shown below
- CRITICAL FORMAT RULE: Every single sentence must be on its own separate line
- Do NOT write paragraphs. Every sentence = one line
- Put a blank line between sections
- Maximum 15 words per sentence
- Be specific: name emotions, levels, confusion pairs, grid sizes
${isArabic ? "- IMPORTANT: Write the ENTIRE report in Arabic. Use formal Modern Standard Arabic (MSA). Include full tashkeel (diacritics) throughout for readability." : ""}

PATIENT: ${childName}
REPORT DATE: ${date}
TOTAL SESSIONS: ${(g1?.sessionCount || 0) + (g2?.sessionCount || 0)}

`;

  if (g1) {
    const rtTrend = g1.rtTrend < -0.5
      ? `Improving — ${Math.abs(g1.rtTrend)}s faster from first to last session`
      : g1.rtTrend > 0.5
      ? `Declining — ${g1.rtTrend}s slower from first to last session`
      : "Stable across sessions";

    const levelRepetitions = g1.levelStats
      .filter((l) => l.played && l.attempts > 1)
      .map((l) => {
        const trend = l.attemptTrend
          .map((a) => `Attempt ${a.attempt}: ${a.hints} hints ${a.passed ? "passed" : "failed"}`)
          .join(" | ");
        return `Level ${l.level + 1}: ${l.attempts} attempts — ${trend}`;
      });

    prompt += `=== GAME 1: FRUIT FINDER — VISUAL WORKING MEMORY ===
Memory accuracy: ${g1.perfectionRate}% (${g1.perfectRounds} perfect rounds out of ${g1.totalRounds} total)
Note: A perfect round = child found all target fruits with zero wrong taps
Sessions played: ${g1.sessionCount}
Distinct levels passed: ${g1.passed} out of 4
Hints used: ${g1.hintsUsed} total (Hint 1 elimination: ${g1.hint1Used}, Hint 2 memory replay: ${g1.hint2Used})
Average response time: ${g1.avgRT}s
Response time trend: ${rtTrend}
Peak memory load reached: ${g1.maxFruitsReached} fruits shown in one round
Peak retention delay reached: ${g1.maxDelayReached}s

Per-level results:
${g1.levelStats.map((l) => l.played
  ? `  Level ${l.level + 1}: ${l.attempts} attempt(s), ${l.passed ? "PASSED" : "NOT PASSED"}, ${l.stars} stars, grid ${l.grid}, delay ${l.delay}s, ${l.hints} hints`
  : `  Level ${l.level + 1}: Not played`
).join("\n")}

${levelRepetitions.length > 0
  ? "Level repetition detail:\n" + levelRepetitions.map((r) => "  " + r).join("\n")
  : "No levels were repeated across attempts."}

Grid performance (% of rounds with zero wrong taps per configuration):
${g1.gridPerformance.slice(0, 6).map((g) =>
  `  ${g.config}: ${g.accuracy}% perfect (${g.attempts} rounds)`
).join("\n") || "  No round-level data available (data from Firebase only)"}

`;
  }

  if (g2) {
    prompt += `=== GAME 2: EMOTION QUEST — FACIAL EMOTION RECOGNITION ===
First-try rate: ${g2.firstTryRate}% (passed with no hints in ${g2.sessionsFirstTry} of ${g2.sessionsPassed} passed sessions)
Sessions played: ${g2.sessionCount}
Distinct emotions passed: ${g2.passed} out of 6
Total hints used: ${g2.hintsUsed}
Average response time: ${g2.avgRT}s
Hint dependency trend: ${g2.hintTrendText || "Stable"}
Independent emotions (most recent: passed, no hints): ${g2.strongEmotions.join(", ") || "None yet"}
Needs support (most recent: failed or 2+ hints): ${g2.weakEmotions.join(", ") || "None identified"}

Per-emotion results:
${g2.emotionStats.map((e) => e.played
  ? `  ${e.emotion}: ${e.attempts} attempt(s), ${e.totalHints} hints total, ${e.passed ? "PASSED" : "NOT PASSED"}, ${e.stars} stars, avg response ${e.avgRT}s`
  : `  ${e.emotion}: Not played`
).join("\n")}

Confusion patterns (most frequent first):
${g2.confusionPairs.slice(0, 8).map((p) =>
  `  ${p.pair}: ${p.count} time(s)`
).join("\n") || "  None recorded yet"}

`;
  }

  if (hasEM) {
    prompt += `=== EMOTION MIRROR — FACIAL EXPRESSION IMITATION ===
Total imitation attempts: ${em.totalAttempts}
Correct imitations: ${em.correct} (${Math.round((em.correct / em.totalAttempts) * 100)}%)
Incorrect imitations: ${em.incorrect}
Session results: ${em.results.join(", ")}
${em.improvements.length > 0 ? "Emotions that improved on retry: " + em.improvements.join(", ") : "No retry improvements recorded"}

`;
  }

  const s = isArabic ? {
    s1: "القسم الأول — نظرة عامة على الجلسة",
    s2: "القسم الثاني — تحليل الذاكرة العاملة البصرية",
    s3: "القسم الثالث — التقدم في المستويات والتكرار",
    s4: "القسم الرابع — تحليل التعرف على المشاعر",
    s5: "القسم الخامس — تحليل مرآة المشاعر",
    s6: isArabic && hasEM ? "القسم السادس — نقاط القوة" : "القسم الخامس — نقاط القوة",
    s7: isArabic && hasEM ? "القسم السابع — مجالات التطوير" : "القسم السادس — مجالات التطوير",
    s8: isArabic && hasEM ? "القسم الثامن — التوصيات السريرية" : "القسم السابع — التوصيات السريرية",
  } : {
    s1: "SECTION 1 — SESSION OVERVIEW",
    s2: "SECTION 2 — VISUAL WORKING MEMORY ANALYSIS",
    s3: "SECTION 3 — LEVEL PROGRESSION AND REPETITION",
    s4: "SECTION 4 — EMOTION RECOGNITION ANALYSIS",
    s5: "SECTION 5 — EMOTION MIRROR ANALYSIS",
    s6: hasEM ? "SECTION 6 — STRENGTHS" : "SECTION 5 — STRENGTHS",
    s7: hasEM ? "SECTION 7 — AREAS FOR DEVELOPMENT" : "SECTION 6 — AREAS FOR DEVELOPMENT",
    s8: hasEM ? "SECTION 8 — CLINICAL RECOMMENDATIONS" : "SECTION 7 — CLINICAL RECOMMENDATIONS",
  };

  prompt += `Write the clinical report using EXACTLY these numbered sections.
Each section must contain 4-6 sentences minimum, one sentence per line.
Do not skip any section. Do not add extra sections.
Use EXACTLY the section titles provided below — do not translate or change them.

${s.s1}
Summarise what was played, total sessions, and give one key clinical observation about overall performance.
Reference the total session count and which games were played.

${s.s2}
${g1
  ? "Interpret the Fruit Finder results clinically. Discuss what the memory accuracy percentage means — a perfect round means zero wrong taps, which reflects strong working memory precision. Comment on hint dependency and whether it suggests independence or reliance. Reference the peak memory load and delay values and what they suggest about working memory ceiling. Comment on the response time trend."
  : "Fruit Finder was not played in this dataset. State this clearly and note its absence from the clinical picture."}

${s.s3}
${g1 && g1.levelStats.some((l) => l.played && l.attempts > 1)
  ? "For each level attempted more than once, analyse whether performance improved across attempts. What does this suggest about the child's learning rate and frustration tolerance? Reference specific hint counts per attempt."
  : g1
  ? "Each level was attempted only once. Analyse the progression across the four levels. What does the pattern of passed versus not-passed levels suggest?"
  : "Fruit Finder was not played. Note this."}

${s.s4}
${g2
  ? "Interpret the Emotion Quest results clinically. Discuss the first-try rate and what it indicates about independent emotion recognition. For the top confusion pairs, explain their clinical significance — for example, Fear-Surprised confusion suggests the child processes arousal level rather than specific facial features. Identify which emotions are consistently strong versus weak. Comment on the hint dependency trend."
  : "Emotion Quest was not played in this dataset. State this clearly."}

${hasEM
  ? `${s.s5}
Compare the child's ability to RECOGNISE emotions (Game 2) versus PRODUCE them (Emotion Mirror).
Where is the gap largest? Does the child find it easier to recognise or to imitate?
What does any disconnect suggest about their emotional processing?

${s.s6}
List 4 specific clinical strengths observed in this data.
Each strength must reference an actual data value.
Do not write generic statements.

${s.s7}
List 4 specific areas that need therapeutic support.
Each must name the specific skill, level, or emotion and reference a data value.

${s.s8}
Write 5 actionable recommendations for the therapist.
Each recommendation must reference specific data: confusion pairs, grid sizes, emotions, hint counts, or difficulty parameters.
Be concrete and practical.`
  : `${s.s6}
List 4 specific clinical strengths observed in this data.
Each strength must reference an actual data value.
Do not write generic statements.

${s.s7}
List 4 specific areas that need therapeutic support.
Each must name the specific skill, level, or emotion and reference a data value.

${s.s8}
Write 5 actionable recommendations for the therapist.
Each recommendation must reference specific data: confusion pairs, grid sizes, emotions, hint counts.
Be concrete and practical.`}

End the report with this exact line:
---
Report generated by Lumi Play AI · ${date} · For clinical use only`;

  return prompt;
}