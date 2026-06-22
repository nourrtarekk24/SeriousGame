import React, { useState, useEffect, useRef } from "react";
import { parseChildData, computeG1Stats, computeG1StatsByMode, computeG2Stats } from "../utils/dataParser";
import { fetchChildSessions, fetchEmotionMirror, buildEmStatsFromDocs, saveReportToFirebase, fetchReportsFromFirebase, deleteReportFromFirebase } from "../utils/firebaseService";
import { generateReport } from "../utils/geminiService";
import config from "../config/config";
import {
  LineChart, Line,
  XAxis, YAxis, CartesianGrid, Tooltip,
  ResponsiveContainer
} from "recharts";

function DashboardPage({ child, onBack, therapistName, onLogout }) {
  const [activeTab,    setActiveTab]    = useState("g1");
  const [g1Stats,      setG1Stats]      = useState(null);
  const [allG1Sessions, setAllG1Sessions] = useState([]);
  const [g2Stats,      setG2Stats]      = useState(null);
  const [emStats,      setEmStats]      = useState(null);
  const [loading,      setLoading]      = useState(true);
  const [report,       setReport]       = useState("");
  const [reportState,  setReportState]  = useState("idle");
  const [reportLang,   setReportLang]   = useState("en");
  const [savedReports, setSavedReports] = useState([]);
  const reportRef = useRef(null);

  useEffect(() => {
    async function load() {
      setLoading(true);
      try {
        let g1Sessions = [];
        let g2Sessions = [];
        let em = null;

        if (config.USE_FIREBASE) {

          const reports = await fetchReportsFromFirebase(child.name);
          setSavedReports(reports);

          const sessions = await fetchChildSessions(child.name);
          g1Sessions = sessions.g1Sessions || [];
          g2Sessions = sessions.g2Sessions || [];
          const emResult = buildEmStatsFromDocs(sessions.emDocs || []);
          if (emResult) setEmStats(emResult);
        }

        if (child.files && child.files.length > 0) {
          const data = await parseChildData(child);

          data.g1Sessions.forEach(uploaded => {
            const exists = g1Sessions.some(
              fb => fb.currentLevel === uploaded.currentLevel &&
                    fb.attemptNumber === uploaded.attemptNumber
            );
            if (!exists) g1Sessions.push(uploaded);
          });

          data.g2Sessions.forEach(uploaded => {
            const exists = g2Sessions.some(
              fb => fb.currentLevel === uploaded.currentLevel &&
                    fb.attemptNumber === uploaded.attemptNumber
            );
            if (!exists) g2Sessions.push(uploaded);
          });
        }

        const byTime = (a, b) => (a.timestamp || "").localeCompare(b.timestamp || "");
        g1Sessions.sort(byTime);
        g2Sessions.sort(byTime);

        setG1Stats(computeG1Stats(g1Sessions));
        setAllG1Sessions(g1Sessions);
        setG2Stats(computeG2Stats(g2Sessions));
      } catch (err) {
        console.error("Error loading session data:", err);
      }
      setLoading(false);
    }
    load();
  }, [child]);

  async function handleGenerateReport() {
    setReportState("loading");
    setReport("");
    try {
      const text = await generateReport(child.name, g1Stats, g2Stats, emStats, reportLang);
      setReport(text);
      setReportState("done");

      if (config.USE_FIREBASE) {
        const entry = await saveReportToFirebase(child.name, text, reportLang);
        if (entry) setSavedReports(prev => [entry, ...prev]);
      }
    } catch (err) {
      console.error("Report generation failed:", err);
      setReportState("error");
      setReport(err.message || "Unknown error");
    }
  }

  async function handleDeleteReport(reportId) {
    if (config.USE_FIREBASE) {
      await deleteReportFromFirebase(child.name, reportId);
    }
    setSavedReports(prev => prev.filter(r => r.id !== reportId));
  }

  function handleDownloadPDF() {
    const reportText = reportRef.current?.innerText || report;
    if (!reportText) return;

    const printWindow = window.open("", "_blank");
    printWindow.document.write(`
      <html>
        <head>
          <title>Clinical Report — ${child.name} — ${new Date().toISOString().slice(0,10)}</title>
          <style>
            body {
              font-family: Georgia, serif;
              font-size: 13px;
              line-height: 1.9;
              color: #1a1a1a;
              max-width: 720px;
              margin: 40px auto;
              padding: 0 20px;
            }
            h1 {
              font-size: 18px;
              border-bottom: 2px solid #1a1a1a;
              padding-bottom: 8px;
              margin-bottom: 24px;
            }
            pre {
              white-space: pre-wrap;
              word-break: break-word;
              font-family: Georgia, serif;
              font-size: 13px;
              line-height: 1.9;
            }
            @media print {
              body { margin: 20mm; }
            }
          </style>
        </head>
        <body>
          <h1>Lumi's World — Clinical Report<br/>
          <span style="font-size:13px;font-weight:normal;">Patient: ${child.name} &nbsp;|&nbsp; Date: ${new Date().toISOString().slice(0,10)}</span></h1>
          <pre>${reportText.replace(/</g, "&lt;").replace(/>/g, "&gt;")}</pre>
        </body>
      </html>
    `);
    printWindow.document.close();
    printWindow.focus();
    setTimeout(() => {
      printWindow.print();
      printWindow.close();
    }, 300);
  }

  if (loading) {
    return (
      <div className="dashboard-page" style={{ display:"flex", alignItems:"center", justifyContent:"center", minHeight:"100vh" }}>
        <div style={{ textAlign:"center" }}>
          <div className="spinner" style={{ width:40, height:40, borderWidth:3, margin:"0 auto 16px" }} />
          <p style={{ color:"var(--text-muted)" }}>Loading session data...</p>
        </div>
      </div>
    );
  }

  return (
    <div className="dashboard-page fade-in">

      {/* ── Header ── */}
      <header className="page-header">
        <div className="header-brand" style={{ gap:16 }}>
          <button className="btn-back" onClick={onBack}>←</button>
          <div className="brand-dot" />
          <h1 className="dashboard-child-name">
            <span>{child.name}</span>'s Progress
          </h1>
        </div>
        <div className="header-right">
          {g1Stats && <span className="header-therapist">G1: {g1Stats.sessionCount} sessions</span>}
          {g1Stats && g2Stats && <span style={{ color:"var(--border-dark)" }}>|</span>}
          {g2Stats && <span className="header-therapist">G2: {g2Stats.sessionCount} sessions</span>}
          {therapistName && <span className="header-therapist" style={{ marginLeft:8 }}>👤 {therapistName}</span>}
          <button className="btn-logout" onClick={onLogout}>Sign Out</button>
        </div>
      </header>

      {}
      <div className="tabs-bar">
        <button className={`tab-btn ${activeTab==="g1"     ? "active":""}`} onClick={() => setActiveTab("g1")}>🍎 Fruit Finder</button>
        <button className={`tab-btn ${activeTab==="g2"     ? "active":""}`} onClick={() => setActiveTab("g2")}>😊 Emotion Quest</button>
        <button className={`tab-btn ${activeTab==="em"     ? "active":""}`} onClick={() => setActiveTab("em")}>🪞 Emotion Mirror</button>
        <button className={`tab-btn ${activeTab==="report" ? "active":""}`} onClick={() => setActiveTab("report")}>📋 Clinical Report</button>
      </div>

      {}
      <div className="dashboard-content">
        {activeTab === "g1"     && <G1Tab     stats={g1Stats} allG1Sessions={allG1Sessions} />}
        {activeTab === "g2"     && <G2Tab     stats={g2Stats} />}
        {activeTab === "em"     && <EMTab     emStats={emStats} />}
        {activeTab === "report" && (
          <ReportTab
            report={report}
            reportState={reportState}
            reportRef={reportRef}
            onGenerate={handleGenerateReport}
            onDownload={handleDownloadPDF}
            reportLang={reportLang}
            setReportLang={setReportLang}
            savedReports={savedReports}
            onDeleteReport={handleDeleteReport}
          />
        )}
      </div>
    </div>
  );
}

function G1Tab({ stats, allG1Sessions }) {
  const [mode, setMode] = useState("adaptive");

  if (!stats && (!allG1Sessions || allG1Sessions.length === 0))
    return <NoData game="Fruit Finder" />;

  const modeStats = allG1Sessions
    ? computeG1StatsByMode(allG1Sessions, mode)
    : null;

  const modeColor     = mode === "adaptive" ? "var(--green)"     : "#6495ed";
  const modeChartLine = mode === "adaptive" ? "#1a9e5a"          : "#6495ed";

  return (
    <div>
      {}
      <div style={{ display:"flex", gap:10, marginBottom:28, background:"var(--bg)", padding:4, borderRadius:12, border:"1px solid var(--border)", width:"fit-content" }}>
        {["adaptive","fixed"].map((m) => {
          const active = mode === m;
          const color  = m === "adaptive" ? "var(--green)" : "#6495ed";
          const pale   = m === "adaptive" ? "var(--green-pale)" : "rgba(100,149,237,0.12)";
          const label  = m === "adaptive" ? "🔄 Adaptive" : "📌 Fixed";
          const sub    = m === "adaptive" ? "Auto-adjusting difficulty" : "Therapist-set difficulty";
          return (
            <button key={m} onClick={() => setMode(m)} style={{ display:"flex", flexDirection:"column", alignItems:"flex-start", padding:"10px 20px", borderRadius:9, border:"none", cursor:"pointer", background: active ? pale : "transparent", outline: active ? `2px solid ${color}` : "none", transition:"all 0.18s ease", minWidth:160 }}>
              <span style={{ fontSize:13, fontWeight:700, color: active ? color : "var(--text-muted)" }}>{label}</span>
              <span style={{ fontSize:11, color: active ? color : "var(--text-light)", marginTop:2, opacity: active ? 0.85 : 0.6 }}>{sub}</span>
            </button>
          );
        })}
      </div>

      {}
      {!modeStats ? (
        <div style={{ textAlign:"center", padding:"56px 24px", background:"var(--bg-card)", borderRadius:"var(--radius)", border:"1px dashed var(--border-dark)" }}>
          <div style={{ fontSize:36, marginBottom:12 }}>{mode === "adaptive" ? "🔄" : "📌"}</div>
          <h3 style={{ fontSize:16, color:"var(--text-main)", marginBottom:6 }}>No {mode === "adaptive" ? "Adaptive" : "Fixed"} sessions recorded yet</h3>
          <p style={{ fontSize:13, color:"var(--text-muted)", maxWidth:360, margin:"0 auto" }}>
            {mode === "adaptive" ? "This child has not played Fruit Finder in Adaptive mode." : "This child has not played Fruit Finder in Fixed mode."}
          </p>
        </div>
      ) : (
        <G1ModeView stats={modeStats} modeColor={modeColor} modeChartLine={modeChartLine} mode={mode} />
      )}
    </div>
  );
}

function G1ModeView({ stats, modeColor, modeChartLine, mode }) {
  const rtTrendClass = stats.rtTrend < -0.5 ? "trend-up" : stats.rtTrend > 0.5 ? "trend-down" : "trend-flat";
  const rtTrendText  = stats.rtTrend < -0.5 ? `↑ ${Math.abs(stats.rtTrend)}s faster` : stats.rtTrend > 0.5 ? `↓ ${stats.rtTrend}s slower` : "→ Stable";

  return (
    <div>
      <p style={{ fontSize:12, color:"var(--text-muted)", marginBottom:20 }}>
        Showing data from <strong style={{ color:modeColor }}>{stats.sessionCount} {mode} session{stats.sessionCount !== 1 ? "s" : ""}</strong>
      </p>

      <div className="stats-row">
        <StatCard value={stats.perfectionRate + "%"} label="Memory Accuracy"
          sub={stats.hasRoundData ? `${stats.perfectRounds} of ${stats.totalRounds} rounds with no wrong taps` : `${stats.perfectRounds} of ${stats.totalRounds} sessions without wrong taps`}
          valueClass={stats.perfectionRate >= 60 ? "good" : "bad"} />
        <StatCard value={`${stats.passed} / 4`} label="Levels Passed" />
        <StatCard value={stats.hintsUsed} label="Total Hints Used" />
        <StatCard value={stats.avgRT + "s"} label="Avg Response Time" />
        <StatCard value={rtTrendText} label="Speed Trend" valueClass={rtTrendClass}
          sub={stats.rtTrend < -0.5 ? "Recalling faster over time" : stats.rtTrend > 0.5 ? "Taking longer over time" : "Consistent speed"} />
      </div>

      {stats.accuracyOverTime.length > 1 && (
        <div className="chart-card">
          <h3>Memory Accuracy Over Sessions</h3>
          <p style={{ color:"var(--text-muted)", fontSize:12, marginBottom:12 }}>% of rounds completed without wrong taps — {mode} mode only</p>
          <ResponsiveContainer width="100%" height={220}>
            <LineChart data={stats.accuracyOverTime} margin={{ top:5, right:20, left:0, bottom:5 }}>
              <CartesianGrid strokeDasharray="3 3" stroke="rgba(30,45,74,0.06)" />
              <XAxis dataKey="label" stroke="#8a9bbf" tick={{ fontSize:12 }} />
              <YAxis domain={[0,100]} stroke="#8a9bbf" tick={{ fontSize:12 }} unit="%" />
              <Tooltip contentStyle={{ background:"#fff", border:"1px solid var(--border)", borderRadius:8 }} formatter={(v) => v + "%"} />
              <Line type="monotone" dataKey="accuracy" stroke={modeChartLine} strokeWidth={2.5} dot={{ fill:modeChartLine, r:4 }} />
            </LineChart>
          </ResponsiveContainer>
        </div>
      )}

      <div className="stats-row" style={{ gridTemplateColumns:"repeat(2,1fr)", marginBottom:32 }}>
        <StatCard value={stats.maxFruitsReached} label="Peak Memory Load" sub="max fruits shown in one round" />
        <StatCard value={stats.maxDelayReached + "s"} label="Peak Retention Delay" sub="longest delay the child faced" />
      </div>

      <p className="section-label">Level Results</p>
      <div className="levels-grid" style={{ marginBottom:32 }}>
        {stats.levelStats.map((lvl) => <LevelCard key={lvl.level} level={lvl} />)}
      </div>

      {stats.hintsUsed > 0 && (
        <>
          <p className="section-label">Hint Usage</p>
          <div className="stats-row" style={{ gridTemplateColumns:"repeat(3,1fr)", marginBottom:32 }}>
            <StatCard value={stats.hintsUsed} label="Total Hints" />
            <StatCard value={stats.hint1Used} label="Hint 1 — Elimination" sub="half the wrong cells dimmed" />
            <StatCard value={stats.hint2Used} label="Hint 2 — Memory Replay" sub="fruit shown again" />
          </div>
        </>
      )}

    </div>
  );
}

function G2Tab({ stats }) {
  if (!stats) return <NoData game="Emotion Quest" />;

  return (
    <div>
      {}
      <div className="stats-row">
        <StatCard
          value={stats.firstTryRate + "%"}
          label="First-Try Rate"
          sub={`${stats.sessionsFirstTry} of ${stats.sessionsPassed} passed sessions needed no hints`}
          valueClass={stats.firstTryRate >= 60 ? "good" : "bad"}
        />
        <StatCard value={`${stats.passed} / 6`} label="Emotions Passed" />
        <StatCard value={stats.hintsUsed} label="Total Hints Used" />
        <StatCard value={stats.avgRT + "s"} label="Avg Response Time" />
        <StatCard
          value={stats.hintTrendText}
          label="Hint Trend"
          sub="Are hints needed less over time?"
          valueClass={stats.hintTrendClass}
        />
      </div>

      {}
      {(stats.strongEmotions.length > 0 || stats.weakEmotions.length > 0) && (
        <div style={{ display:"flex", gap:16, marginBottom:32 }}>
          {stats.strongEmotions.length > 0 && (
            <div className="chart-card" style={{ flex:1, padding:"20px 24px" }}>
              <h3 style={{ fontSize:14, marginBottom:4, color:"var(--green)" }}>✓ Independent — No Hints Needed</h3>
              <p style={{ color:"var(--text-muted)", fontSize:12, marginBottom:12 }}>Passed most recent attempt with 0 hints</p>
              <div style={{ display:"flex", flexWrap:"wrap", gap:8 }}>
                {stats.strongEmotions.map((e) => (
                  <span key={e} className="level-card-badge badge-passed">{e}</span>
                ))}
              </div>
            </div>
          )}
          {stats.weakEmotions.length > 0 && (
            <div className="chart-card" style={{ flex:1, padding:"20px 24px" }}>
              <h3 style={{ fontSize:14, marginBottom:4, color:"var(--red)" }}>⚠ Needs Support</h3>
              <p style={{ color:"var(--text-muted)", fontSize:12, marginBottom:12 }}>Failed or needed maximum hints in most recent attempt</p>
              <div style={{ display:"flex", flexWrap:"wrap", gap:8 }}>
                {stats.weakEmotions.map((e) => (
                  <span key={e} className="level-card-badge badge-failed">{e}</span>
                ))}
              </div>
            </div>
          )}
        </div>
      )}

      {}
      <p className="section-label">Emotion Results</p>
      <div className="levels-grid six-cols" style={{ marginBottom:32 }}>
        {stats.emotionStats.map((e, i) => <EmotionLevelCard key={e.emotion} emotion={e} index={i} />)}
      </div>

      {}
      <p className="section-label">Confusion Patterns</p>
      <p style={{ color:"var(--text-muted)", fontSize:12, marginBottom:12 }}>
        Which emotion the child selected when the answer was wrong
      </p>
      <div className="confusion-table" style={{ marginBottom:32 }}>
        <div className="confusion-table-header">
          <span>Emotion Confused</span><span>Times</span>
        </div>
        {stats.confusionPairs.length === 0 ? (
          <p className="confusion-empty">No confusion patterns recorded yet.</p>
        ) : (
          stats.confusionPairs.map((row) => {
            const [from, to] = row.pair.split(" → ");
            return (
              <div key={row.pair} className="confusion-row">
                <span className="confusion-pair">
                  <strong>{from}</strong> mistaken for {to}
                </span>
                <span className="confusion-count">{row.count}×</span>
              </div>
            );
          })
        )}
      </div>
    </div>
  );
}

function EMTab({ emStats }) {
  if (!emStats) {
    return (
      <div className="chart-card" style={{ textAlign:"center", padding:"48px 32px" }}>
        <span style={{ fontSize:48, display:"block", marginBottom:20 }}>🪞</span>
        <h3 style={{ fontSize:20, marginBottom:12 }}>Emotion Mirror Data</h3>
        <p style={{ color:"var(--text-muted)", fontSize:15, lineHeight:1.7, maxWidth:480, margin:"0 auto 24px" }}>
          Emotion Mirror results will appear here once sessions are recorded and Firebase is connected.
        </p>
      </div>
    );
  }

  const accuracy = emStats.totalAttempts > 0
    ? Math.round((emStats.correct / emStats.totalAttempts) * 100)
    : 0;

  return (
    <div>

      {}
      <div className="stats-row" style={{ gridTemplateColumns:"repeat(3,1fr)", marginBottom:32 }}>
        <StatCard value={emStats.totalAttempts} label="Total Attempts" />
        <StatCard value={emStats.correct}        label="Correct Imitations" valueClass="good" />
        <StatCard value={emStats.incorrect}      label="Incorrect Imitations" valueClass={emStats.incorrect > 0 ? "bad" : "good"} />
      </div>

      {}
      {(emStats.perEmotion || []).length > 0 && (
        <>
          <p className="section-label">Performance Per Emotion</p>
          <p style={{ color:"var(--text-muted)", fontSize:12, marginBottom:12 }}>
            Correct imitations, attempts, and average confidence per emotion
          </p>
          <div className="confusion-table" style={{ marginBottom:32 }}>
            <div className="confusion-table-header" style={{ gridTemplateColumns:"1fr 80px 80px 80px 80px" }}>
              <span>Emotion</span>
              <span>Attempts</span>
              <span>Correct</span>
              <span>Accuracy</span>
              <span>Avg Conf</span>
            </div>
            {emStats.perEmotion.map((e) => (
              <div key={e.emotion} className="confusion-row" style={{ gridTemplateColumns:"1fr 80px 80px 80px 80px" }}>
                <span className="confusion-pair" style={{ fontWeight:600 }}>{e.emotion}</span>
                <span style={{ color:"var(--text-muted)" }}>{e.attempts}</span>
                <span style={{ color: e.correct === e.attempts ? "var(--green)" : e.correct > 0 ? "var(--amber)" : "var(--red)", fontWeight:600 }}>
                  {e.correct}/{e.attempts}
                </span>
                <span style={{ color: e.accuracy >= 60 ? "var(--green)" : "var(--red)", fontWeight:600 }}>
                  {e.accuracy}%
                </span>
                <span style={{ color:"var(--text-muted)" }}>{e.avgConf}%</span>
              </div>
            ))}
          </div>
        </>
      )}

      {}
      {(emStats.improvements || []).length > 0 && (
        <>
          <p className="section-label">Improved After Feedback</p>
          <p style={{ color:"var(--text-muted)", fontSize:12, marginBottom:12 }}>
            Emotions the child successfully imitated after a previous incorrect attempt
          </p>
          <div style={{ display:"flex", gap:10, flexWrap:"wrap", marginBottom:32 }}>
            {emStats.improvements.map((e) => (
              <span key={e} style={{
                background:"var(--green-pale)", color:"var(--green)",
                borderRadius:6, padding:"4px 14px", fontSize:13, fontWeight:600,
              }}>✓ {e}</span>
            ))}
          </div>
        </>
      )}

      {}
      {(emStats.results || []).length > 0 && (
        <>
          <p className="section-label">Attempt Log</p>
          <div className="confusion-table" style={{ marginBottom:32 }}>
            <div className="confusion-table-header" style={{ gridTemplateColumns:"1fr auto" }}>
              <span>Result</span><span>Outcome</span>
            </div>
            {emStats.results.map((r, i) => (
              <div key={i} className="confusion-row">
                <span className="confusion-pair">{r}</span>
                <span style={{ color: r.includes(": correct") ? "var(--green)" : "var(--red)", fontWeight:600 }}>
                  {r.includes(": correct") ? "✓" : "✗"}
                </span>
              </div>
            ))}
          </div>
        </>
      )}

    </div>
  );
}

function ReportTab({ report, reportState, reportRef, onGenerate, onDownload, reportLang, setReportLang, savedReports = [], onDeleteReport }) {

  function downloadReportAsTxt(entry) {
    const blob = new Blob([entry.content], { type: "text/plain;charset=utf-8" });
    const url  = URL.createObjectURL(blob);
    const a    = document.createElement("a");
    a.href     = url;
    a.download = `Lumi_Report_${entry.childName}_${entry.date.replace(/ /g,"_")}.txt`;
    a.click();
    URL.revokeObjectURL(url);
  }

  function downloadReportAsPDF(entry) {
    const win = window.open("", "_blank");
    win.document.write(`
      <html><head><title>Clinical Report — ${entry.childName}</title>
      <style>
        body { font-family: Arial, sans-serif; padding: 40px; max-width: 800px; margin: auto; }
        h2 { color: #1a7f6e; } pre { white-space: pre-wrap; line-height: 1.7; font-size: 14px; }
        .meta { color: #888; font-size: 12px; margin-bottom: 24px; }
      </style></head>
      <body>
        <h2>Lumi Play — Clinical Report</h2>
        <div class="meta">Child: ${entry.childName} &nbsp;|&nbsp; Generated: ${entry.date} ${entry.time} &nbsp;|&nbsp; Language: ${entry.lang === "ar" ? "Arabic" : "English"}</div>
        <pre>${entry.content}</pre>
      </body></html>
    `);
    win.document.close();
    setTimeout(() => { win.print(); }, 500);
  }

  return (
    <div className="report-section">
      <h3>AI Clinical Report</h3>
      <p className="report-description">
        Generates a structured clinical report using Gemini AI, interpreting session data
        across all games. Intended as a clinical aid — not a diagnosis.
      </p>

      {}
      <div style={{ display:"flex", gap:8, marginBottom:12, alignItems:"center" }}>
        <span style={{ fontSize:12, color:"var(--text-muted)", fontWeight:500 }}>Report language:</span>
        {["en","ar"].map(lang => (
          <button
            key={lang}
            onClick={() => setReportLang(lang)}
            style={{
              padding:"4px 14px", borderRadius:20, fontSize:12, fontWeight:600,
              border: reportLang === lang ? "none" : "1px solid var(--border)",
              background: reportLang === lang ? "var(--teal)" : "transparent",
              color: reportLang === lang ? "#fff" : "var(--text-muted)",
              cursor:"pointer", transition:"all 0.2s"
            }}
          >
            {lang === "en" ? "English" : "العربية"}
          </button>
        ))}
      </div>

      <div style={{ display:"flex", gap:12, alignItems:"center", flexWrap:"wrap" }}>
        <button
          className="btn-generate"
          onClick={onGenerate}
          disabled={reportState === "loading"}
        >
          {reportState === "loading" ? (
            <><span className="spinner" /> Generating...</>
          ) : reportState === "done" ? (
            "↻ Regenerate Report"
          ) : (
            "✦ Generate Report"
          )}
        </button>

        {reportState === "done" && (
          <button
            className="btn-generate"
            onClick={onDownload}
            style={{ background:"linear-gradient(135deg,#2ecc71,#1a9e55)" }}
          >
            ⬇ Download as PDF
          </button>
        )}
      </div>

      {reportState === "loading" && (
        <p className="report-status">Asking Gemini to analyse all session data — this takes about 15 seconds...</p>
      )}
      {reportState === "error" && (
        <p className="report-status error">
          Report failed: {report || "Check your Gemini API key in config.js and try again."}
        </p>
      )}
      {reportState === "done" && report && (
        <div className="report-output fade-in" ref={reportRef}>
          <pre>{report}</pre>
        </div>
      )}

      {}
      {savedReports.length > 0 && (
        <div style={{ marginTop:40 }}>
          <p className="section-label">Saved Reports</p>
          <p style={{ fontSize:12, color:"var(--text-muted)", marginBottom:16 }}>
            All previously generated reports — download as PDF or plain text.
          </p>
          <div style={{ display:"flex", flexDirection:"column", gap:10 }}>
            {savedReports.map(entry => (
              <div key={entry.id} style={{
                display:"flex", alignItems:"center", justifyContent:"space-between",
                padding:"12px 16px", borderRadius:10,
                border:"1px solid var(--border)", background:"var(--card-bg)",
              }}>
                <div>
                  <span style={{ fontWeight:600, fontSize:13 }}>{entry.childName}</span>
                  <span style={{ fontSize:11, color:"var(--text-muted)", marginLeft:10 }}>
                    {entry.date} · {entry.time} · {entry.lang === "ar" ? "Arabic" : "English"}
                  </span>
                </div>
                <div style={{ display:"flex", gap:8 }}>
                  <button
                    onClick={() => downloadReportAsPDF(entry)}
                    style={{
                      padding:"5px 12px", borderRadius:6, fontSize:11, fontWeight:600,
                      background:"var(--teal)", color:"#fff", border:"none", cursor:"pointer"
                    }}
                  >
                    ⬇ PDF
                  </button>
                  <button
                    onClick={() => downloadReportAsTxt(entry)}
                    style={{
                      padding:"5px 12px", borderRadius:6, fontSize:11, fontWeight:600,
                      background:"transparent", color:"var(--text-muted)",
                      border:"1px solid var(--border)", cursor:"pointer"
                    }}
                  >
                    ⬇ TXT
                  </button>
                  <button
                    onClick={() => onDeleteReport(entry.id)}
                    style={{
                      padding:"5px 10px", borderRadius:6, fontSize:11,
                      background:"transparent", color:"#e74c3c",
                      border:"1px solid #e74c3c", cursor:"pointer"
                    }}
                  >
                    ✕
                  </button>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}

function StatCard({ value, label, valueClass = "", sub = "" }) {
  return (
    <div className="stat-card">
      <span className={`stat-card-value ${valueClass}`}>{value}</span>
      <span className="stat-card-label">{label}</span>
      {sub && <span className="stat-card-sub">{sub}</span>}
    </div>
  );
}

function formatAdaptiveChanges(raw) {
  if (!raw || raw === "None") return "None";
  try {
    return raw

      .replace(/R(\d+)\[([^\]]+)\]/g, (_, round, changes) => {
        const parts = changes.split("|").map(c => {

          if (c.startsWith("Delay")) return c.replace("Delay ", "Delay: ");

          if (c.startsWith("Grid")) return c.replace("Grid ", "Grid: ").replace("x", "×").replace("x", "×");

          if (c.startsWith("R0Fruits")) return "Round 1 fruits: " + c.replace("R0Fruits ", "");
          if (c.startsWith("R1Fruits")) return "Round 2 fruits: " + c.replace("R1Fruits ", "");
          return c;
        });
        return `After round ${round}: ${parts.join(", ")}`;
      })

      .split(",").join(" · ");
  } catch {
    return raw;
  }
}

function LevelCard({ level: lvl }) {
  const notPlayed = !lvl.played;
  const color     = notPlayed ? "#8a9bbf" : lvl.passed ? "#2ecc71" : "#e74c3c";
  const stars     = "★".repeat(lvl.stars || 0) + "☆".repeat(3 - (lvl.stars || 0));

  return (
    <div className="level-card">
      <div className="level-card-header">
        <span className="level-card-title">Level {lvl.level + 1}</span>
        <span className="level-card-stars" style={{ color:"#f5a623" }}>{notPlayed ? "☆☆☆" : stars}</span>
      </div>

      {notPlayed ? (
        <>
          <div className="level-card-accuracy" style={{ color:"#8a9bbf" }}>—</div>
          <div className="level-card-detail">Not played yet</div>
          <span className="level-card-badge badge-unplayed">Not played</span>
        </>
      ) : (
        <>
          {}
          {(lvl.attemptTrend || []).map((a, i) => (
            <div key={i} style={{
              borderTop: "1px solid var(--border)",
              paddingTop: 8, marginTop: 8,
            }}>
              {}
              <div style={{ display:"flex", justifyContent:"space-between", fontSize:12, marginBottom:4 }}>
                <span style={{ fontWeight:600, color:"var(--text)" }}>
                  Attempt {a.attempt}
                  <span style={{
                    marginLeft:6,
                    background: a.difficultyMode === "fixed" ? "rgba(100,149,237,0.15)" : "rgba(46,204,113,0.1)",
                    color: a.difficultyMode === "fixed" ? "#6495ed" : "#2ecc71",
                    borderRadius:4, padding:"1px 5px", fontSize:10, fontWeight:500
                  }}>
                    {a.difficultyMode === "fixed" ? "Fixed" : "Adaptive"}
                  </span>
                </span>
                <span style={{ display:"flex", gap:6, alignItems:"center" }}>
                  <span style={{
                    background: a.hints > 0 ? "rgba(245,166,35,0.12)" : "rgba(46,204,113,0.1)",
                    color: a.hints > 0 ? "#f5a623" : "#2ecc71",
                    borderRadius:4, padding:"1px 6px", fontSize:11
                  }}>
                    {a.hints > 0 ? `${a.hints} hint${a.hints > 1 ? "s" : ""}` : "no hints"}
                  </span>
                  <span style={{ color: a.passed ? "#2ecc71" : "#e74c3c", fontSize:13, fontWeight:700 }}>
                    {a.passed ? "✓" : "✗"}
                  </span>
                </span>
              </div>
              {}
              {(a.rounds || []).map((r, ri) => (
                <div key={ri} style={{
                  display:"flex", justifyContent:"space-between",
                  fontSize:11, color:"var(--text-muted)",
                  paddingLeft:8, marginTop:2,
                }}>
                  <span>Round {r.roundNumber} · {r.fruitsShown} fruit{r.fruitsShown > 1 ? "s" : ""}</span>
                  <span style={{ display:"flex", gap:6 }}>
                    {r.wrongTaps > 0 && (
                      <span style={{ color:"#e74c3c" }}>{r.wrongTaps} wrong tap{r.wrongTaps > 1 ? "s" : ""}</span>
                    )}
                    {r.wrongTaps === 0 && (
                      <span style={{ color:"#2ecc71" }}>perfect</span>
                    )}
                    <span style={{ color:"var(--text-muted)" }}>{r.responseTime}s</span>
                    <span style={{ color: r.passed ? "#2ecc71" : "#e74c3c" }}>
                      {r.passed ? "✓" : "✗"}
                    </span>
                  </span>
                </div>
              ))}

              {}
              {a.difficultyMode !== "fixed" && a.adaptiveChanges && a.adaptiveChanges !== "None" && (
                <div style={{
                  marginTop:6, padding:"4px 8px",
                  background:"rgba(46,204,113,0.07)",
                  borderRadius:5, fontSize:10,
                  color:"var(--text-muted)",
                  borderLeft:"2px solid #2ecc71"
                }}>
                  <span style={{ fontWeight:600, color:"#2ecc71" }}>Difficulty adjustments: </span>
                  {formatAdaptiveChanges(a.adaptiveChanges)}
                </div>
              )}
              {(a.finalGrid || a.finalDelay > 0) && (
                <div style={{ fontSize:10, color:"var(--text-muted)", marginTop:3, paddingLeft:8 }}>
                  Final: grid {a.finalGrid || "—"} · delay {a.finalDelay}s
                </div>
              )}
            </div>
          ))}

          <span className={`level-card-badge ${lvl.passed ? "badge-passed" : "badge-failed"}`}
            style={{ marginTop:10 }}>
            {lvl.passed ? "Passed ✓" : "Not passed"}
          </span>
        </>
      )}
    </div>
  );
}

function EmotionLevelCard({ emotion: e, index }) {
  const notPlayed = !e.played;
  const latestPassed = e.passed;
  const color = notPlayed ? "#8a9bbf" : latestPassed ? "#2ecc71" : "#e74c3c";
  const stars = "★".repeat(e.stars || 0) + "☆".repeat(3 - (e.stars || 0));

  return (
    <div className="level-card">
      <div className="level-card-header">
        <span className="level-card-title">L{index + 1} · {e.emotion}</span>
        <span className="level-card-stars" style={{ color:"#f5a623" }}>{notPlayed ? "☆☆☆" : stars}</span>
      </div>

      {notPlayed ? (
        <>
          <div className="level-card-accuracy" style={{ color:"#8a9bbf" }}>—</div>
          <div className="level-card-detail">Not played yet</div>
          <span className="level-card-badge badge-unplayed">Not played</span>
        </>
      ) : (
        <>
          {}
          <div style={{ marginTop: 8, display:"flex", flexDirection:"column", gap:4 }}>
            {(e.attemptTrend || []).map((a, i) => (
              <div key={i} style={{
                display:"flex", justifyContent:"space-between", alignItems:"center",
                fontSize:12, color:"var(--text-muted)",
                borderTop: i === 0 ? "1px solid var(--border)" : "none",
                paddingTop: i === 0 ? 8 : 0,
              }}>
                <span style={{ fontWeight:500, color:"var(--text)" }}>Attempt {a.attempt}</span>
                <span style={{ display:"flex", gap:6, alignItems:"center" }}>
                  {}
                  <span style={{ color:"#f5a623", fontSize:11 }}>
                    {"★".repeat(a.stars || 0)}{"☆".repeat(3 - (a.stars || 0))}
                  </span>
                  {}
                  <span style={{
                    background: a.hints > 0 ? "rgba(245,166,35,0.12)" : "rgba(46,204,113,0.1)",
                    color: a.hints > 0 ? "#f5a623" : "#2ecc71",
                    borderRadius:4, padding:"1px 6px", fontSize:11
                  }}>
                    {a.hints > 0 ? `${a.hints} hint${a.hints > 1 ? "s" : ""}` : "no hints"}
                  </span>
                  {}
                  <span style={{ color: a.passed ? "#2ecc71" : "#e74c3c", fontSize:13, fontWeight:700 }}>
                    {a.passed ? "✓" : "✗"}
                  </span>
                </span>
              </div>
            ))}
          </div>

          {e.avgRT > 0 && (
            <div style={{ fontSize:11, color:"var(--text-muted)", marginTop:6 }}>
              Avg response: {e.avgRT}s
            </div>
          )}

          <span className={`level-card-badge ${latestPassed ? "badge-passed" : "badge-failed"}`}
            style={{ marginTop:8 }}>
            {latestPassed ? "Passed ✓" : "Not passed"}
          </span>
        </>
      )}
    </div>
  );
}

function NoData({ game }) {
  return (
    <div className="no-data">
      <span className="no-data-icon">📭</span>
      <h3>No {game} data yet</h3>
      <p>This child hasn't played {game} yet, or no session files were found.</p>
    </div>
  );
}

export default DashboardPage;