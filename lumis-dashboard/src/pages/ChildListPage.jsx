import React, { useState } from "react";
import { groupFilesByChild } from "../utils/dataParser";

function ChildListPage({ children, onSelectChild, onRefresh, onLogout, therapistName, onUpload, onDeleteChild }) {
  const [dragging, setDragging] = useState(false);

  async function collectFiles(items) {
    const files = [];

    async function readEntry(entry, depth = 0) {
      if (entry.isFile) {
        // Skip files at depth 0 — no parent folder means we can't
        // determine which child they belong to. This prevents loose
        // files from creating broken cards.
        if (depth === 0) return;
        await new Promise(resolve => {
          entry.file(file => {
            Object.defineProperty(file, "webkitRelativePath", {
              value: entry.fullPath.replace(/^\//, ""),
              writable: false,
            });
            files.push(file);
            resolve();
          }, () => resolve());
        });
      } else if (entry.isDirectory) {
        const reader = entry.createReader();
        await new Promise(resolve => {
          const allEntries = [];
          function readBatch() {
            reader.readEntries(async batch => {
              if (batch.length === 0) {
                for (const e of allEntries) await readEntry(e, depth + 1);
                resolve();
              } else {
                allEntries.push(...batch);
                readBatch();
              }
            }, () => resolve());
          }
          readBatch();
        });
      }
    }

    for (const item of Array.from(items)) {
      const entry = item.webkitGetAsEntry?.();
      if (entry) await readEntry(entry, 0);
    }
    return files;
  }

  async function handleDrop(e) {
    e.preventDefault();
    setDragging(false);
    const files = await collectFiles(Array.from(e.dataTransfer.items));
    console.log("[Upload] Files collected:", files.map(f => f.webkitRelativePath || f.name));
    const found = groupFilesByChild(files);
    console.log("[Upload] Children found:", found.map(c => c.name));
    if (found.length === 0) {
      alert("No session files found. Make sure you dropped the SunvaleData folder or a child session folder.");
      return;
    }
    if (onUpload) onUpload(found);
  }

  return (
    <div
      className="list-page fade-in"
      onDragOver={e => { e.preventDefault(); setDragging(true); }}
      onDragLeave={() => setDragging(false)}
      onDrop={handleDrop}
      style={{
        outline: dragging ? "3px dashed var(--teal)" : "none",
        outlineOffset: "-6px",
        transition: "outline 0.15s",
      }}
    >

      <header className="page-header">
        <div className="header-brand">
          <div className="brand-dot" />
          <h1>Lumi <span>Play</span></h1>
        </div>
        <div className="header-right">
          {therapistName && (
            <span className="header-therapist">👤 {therapistName}</span>
          )}
          {onRefresh && (
            <button className="btn-secondary" onClick={onRefresh}>↻ Refresh</button>
          )}
          <button className="btn-logout" onClick={onLogout}>Sign Out</button>
        </div>
      </header>

      {dragging && (
        <div style={{
          position: "fixed", inset: 0, background: "rgba(0,128,100,0.08)",
          display: "flex", alignItems: "center", justifyContent: "center",
          zIndex: 999, pointerEvents: "none",
        }}>
          <div style={{
            background: "white", borderRadius: 16, padding: "40px 60px",
            border: "3px dashed var(--teal)", textAlign: "center",
          }}>
            <div style={{ fontSize: 48 }}>📂</div>
            <h2 style={{ color: "var(--teal)", marginTop: 12 }}>Drop session folder here</h2>
          </div>
        </div>
      )}

      <main className="list-body">
        <p className="list-section-title">
          Registered Children
          <span>{children.length} found</span>
        </p>

        {children.length === 0 ? (
          <div className="no-data" style={{ marginTop:60 }}>
            <span className="no-data-icon">👶</span>
            <h3>No children registered yet</h3>
            <p>When a child plays the game for the first time, their profile will appear here automatically.</p>
          </div>
        ) : (
          <div className="children-grid">
            {children.map((child) => (
              <ChildCard
                key={child.name}
                child={child}
                onClick={() => onSelectChild(child)}
                onDelete={onDeleteChild ? () => onDeleteChild(child.name) : null}
              />
            ))}
          </div>
        )}
      </main>
    </div>
  );
}

function ChildCard({ child, onClick, onDelete }) {
  const initial  = child.name.charAt(0).toUpperCase();

  const g1Count = child.g1Count ?? child.files?.filter((f) => f.name?.startsWith("game1_")).length;
  const g2Count = child.g2Count ?? child.files?.filter((f) => f.name?.startsWith("game2_")).length;
  const emCount = child.emCount ?? 0;

  return (
    <div className="child-card" onClick={onClick} style={{ position: "relative" }}>
      {onDelete && (
        <button
          onClick={e => { e.stopPropagation(); onDelete(); }}
          style={{
            position: "absolute", top: 10, right: 10,
            background: "none", border: "none", cursor: "pointer",
            fontSize: 16, color: "var(--text-muted)", lineHeight: 1,
            padding: "2px 6px", borderRadius: 6,
          }}
          title="Remove card"
        >✕</button>
      )}
      <div className="child-card-avatar">{initial}</div>
      <h3>{child.name}</h3>
      <div className="child-card-stats">
        <div className="child-stat">
          <span className="child-stat-value" style={{ color: g1Count > 0 ? "var(--teal)" : "var(--text-muted)" }}>
            {g1Count > 0 ? g1Count : "—"}
          </span>
          <span className="child-stat-label">G1 Sessions</span>
        </div>
        <div className="child-stat">
          <span className="child-stat-value" style={{ color: g2Count > 0 ? "var(--teal)" : "var(--text-muted)" }}>
            {g2Count > 0 ? g2Count : "—"}
          </span>
          <span className="child-stat-label">G2 Sessions</span>
        </div>
        <div className="child-stat">
          <span className="child-stat-value" style={{ color: emCount > 0 ? "var(--teal)" : "var(--text-muted)" }}>
            {emCount > 0 ? emCount : "—"}
          </span>
          <span className="child-stat-label">EM Sessions</span>
        </div>
      </div>
      <span className="child-card-arrow">→</span>
    </div>
  );
}

export default ChildListPage;