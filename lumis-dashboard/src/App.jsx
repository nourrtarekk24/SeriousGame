// ─────────────────────────────────────────────────────────────────────
// App.jsx — main controller
// Flow:
//   LoginPage (always first) → 
//   UploadPage (if Firebase OFF) OR loading from Firebase (if ON) →
//   ChildListPage →
//   DashboardPage
// ─────────────────────────────────────────────────────────────────────

import React, { useState, useRef } from "react";
import "./App.css";
import config from "./config/config";

import LoginPage     from "./pages/LoginPage";
import UploadPage    from "./pages/UploadPage";
import ChildListPage from "./pages/ChildListPage";
import DashboardPage from "./pages/DashboardPage";

import { fetchAllChildren, uploadSessionsToFirebase } from "./utils/firebaseService";
import { parseChildData } from "./utils/dataParser";

function App() {
  const [screen,         setScreen]         = useState("login");
  const [therapist,      setTherapist]      = useState(null);
  const [children,       setChildren]       = useState([]);
  const [selectedChild,  setSelectedChild]  = useState(null);
  const [loadError,      setLoadError]      = useState("");
  const [uploadedFiles,  setUploadedFiles]  = useState({});
  const [hiddenCards,    setHiddenCards]    = useState(new Set());

  // Refs so loadChildrenFromFirebase always reads latest state
  // even when called from stale closures (Refresh button)
  const uploadedFilesRef = useRef({});
  const hiddenCardsRef   = useRef(new Set());

  function mergeWithUploads(firebaseKids, uploads, hidden) {
    const visible = firebaseKids.filter(k => !hidden.has(k.name.toLowerCase()));

    // Track which upload keys were successfully matched to a Firebase child
    const matched = new Set();

    const merged = visible.map(kid => {
      const key       = kid.name.toLowerCase();
      const firstWord = key.split(" ")[0];

      // Exact match
      if (uploads[key]) {
        matched.add(key);
        return { ...kid, files: uploads[key] };
      }
      // First-word match (e.g. Firebase="nour", folder="nour tarek")
      if (uploads[firstWord]) {
        matched.add(firstWord);
        return { ...kid, files: uploads[firstWord] };
      }
      // Reverse first-word: Firebase="nour tarek", folder="nour"
      const uploadKey = Object.keys(uploads).find(k =>
        k.split(" ")[0] === key || key.split(" ")[0] === k
      );
      if (uploadKey) {
        matched.add(uploadKey);
        return { ...kid, files: uploads[uploadKey] };
      }
      return kid;
    });

    // Add any uploaded children that were NOT matched to a Firebase child
    // Note: uploads always override hidden state — if therapist uploads a folder,
    // always show that child regardless of whether they were previously dismissed.
    Object.entries(uploads).forEach(([key, files]) => {
      if (!matched.has(key)) {
        // Remove from hidden if it was there
        hidden.delete(key);
        merged.push({ name: key, files });
      }
    });

    return merged;
  }

  function handleDeleteChild(childName) {
    const key = childName.toLowerCase();
    setHiddenCards(prev => {
      const next = new Set([...prev, key]);
      hiddenCardsRef.current = next;
      return next;
    });
    setChildren(prev => prev.filter(c => c.name.toLowerCase() !== key));
  }

  async function handleLogin(therapistInfo) {
    setTherapist(therapistInfo);
    if (config.USE_FIREBASE) {
      await loadChildrenFromFirebase();
    } else {
      setScreen("upload");
    }
  }

  // Always reads from refs so it's never stale
  async function loadChildrenFromFirebase() {
    const uploads = uploadedFilesRef.current;
    const hidden  = hiddenCardsRef.current;
    setScreen("loading");
    setLoadError("");
    try {
      const kids = await fetchAllChildren();
      setChildren(mergeWithUploads(kids, uploads, hidden));
      setScreen("list");
    } catch (err) {
      console.error("Firebase load error:", err);
      setLoadError("Could not connect to the database. Check your internet connection.");
      setScreen("error");
    }
  }

  function handleUpload(childrenFound) {
    setUploadedFiles(prev => {
      const newUploads = { ...prev };
      childrenFound.forEach(c => {
        newUploads[c.name.toLowerCase()] = c.files;
      });
      uploadedFilesRef.current = newUploads;

      // Remove uploaded children from hidden set
      setHiddenCards(prevHidden => {
        const next = new Set(prevHidden);
        childrenFound.forEach(c => next.delete(c.name.toLowerCase()));
        hiddenCardsRef.current = next;
        return next;
      });

      setChildren(prevKids => mergeWithUploads(
        prevKids.map(k => ({ ...k, files: undefined })),
        newUploads,
        hiddenCardsRef.current
      ));

      // Push sessions to Firebase so they survive refresh permanently
      if (config.USE_FIREBASE) {
        childrenFound.forEach(async child => {
          try {
            const data = await parseChildData(child);
            await uploadSessionsToFirebase(child.name, data.g1Sessions, data.g2Sessions);
            // After upload, re-fetch so the child now appears from Firebase
            loadChildrenFromFirebase();
          } catch (e) {
            console.error("[Upload] Failed to push to Firebase:", e);
          }
        });
      }

      return newUploads;
    });
  }

  function handleSelectChild(child) {
    setSelectedChild(child);
    setScreen("dashboard");
  }

  function handleBackToList() {
    setSelectedChild(null);
    if (config.USE_FIREBASE) {
      loadChildrenFromFirebase();
    } else {
      setScreen("list");
    }
  }

  function handleLogout() {
    setTherapist(null);
    setChildren([]);
    setSelectedChild(null);
    setUploadedFiles({});
    setHiddenCards(new Set());
    uploadedFilesRef.current = {};
    hiddenCardsRef.current   = new Set();
    setScreen("login");
  }

  // ── Screens ───────────────────────────────────────────────────────

  if (screen === "login") {
    return <LoginPage onLogin={handleLogin} />;
  }

  if (screen === "loading") {
    return (
      <div className="splash-screen">
        <div className="splash-icon">🌟</div>
        <h1 className="splash-title">Lumi <span>Play</span></h1>
        <p className="splash-sub">Therapist Dashboard</p>
        <div className="spinner" style={{ width:32, height:32, borderWidth:3, margin:"32px auto 16px" }} />
        <p className="splash-hint">Loading session data...</p>
      </div>
    );
  }

  if (screen === "error") {
    return (
      <div className="splash-screen">
        <span style={{ fontSize:48, marginBottom:16 }}>⚠️</span>
        <h2 style={{ color:"var(--red)", marginBottom:12 }}>Connection Error</h2>
        <p style={{ color:"var(--text-muted)", marginBottom:24, textAlign:"center", maxWidth:400 }}>
          {loadError}
        </p>
        <button className="btn-generate" onClick={loadChildrenFromFirebase}>↻ Try Again</button>
      </div>
    );
  }

  return (
    <>
      {screen === "upload" && (
        <UploadPage onUpload={handleUpload} therapistName={therapist?.name} />
      )}
      {screen === "list" && (
        <ChildListPage
          children={children}
          therapistName={therapist?.name}
          onSelectChild={handleSelectChild}
          onUpload={handleUpload}
          onDeleteChild={handleDeleteChild}
          onRefresh={config.USE_FIREBASE ? () => loadChildrenFromFirebase(uploadedFiles, hiddenCards) : null}
          onLogout={handleLogout}
        />
      )}
      {screen === "dashboard" && selectedChild && (
        <DashboardPage
          child={selectedChild}
          therapistName={therapist?.name}
          onBack={handleBackToList}
          onLogout={handleLogout}
        />
      )}
    </>
  );
}

export default App;