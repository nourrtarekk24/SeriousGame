import React, { useRef, useState } from "react";
import { groupFilesByChild } from "../utils/dataParser";

function UploadPage({ onUpload, therapistName }) {
  const inputRef = useRef(null);
  const [dragging, setDragging] = useState(false);

  function processFiles(files) {
    if (!files || files.length === 0) return;
    const children = groupFilesByChild(files);
    if (children.length === 0) {
      alert("No session data found.\n\nMake sure the folder contains session JSON files.");
      return;
    }
    onUpload(children);
  }

  function handleFolderSelected(e) {
    processFiles(e.target.files);
  }

  // Recursively collect all File entries from a DataTransferItemList
  async function collectFiles(items) {
    const files = [];
    const queue = [];

    for (const item of items) {
      const entry = item.webkitGetAsEntry?.();
      if (entry) queue.push(entry);
    }

    async function readEntry(entry) {
      if (entry.isFile) {
        await new Promise(resolve => {
          entry.file(file => {
            // Attach relative path so groupFilesByChild can parse the folder structure
            Object.defineProperty(file, "webkitRelativePath", {
              value: entry.fullPath.replace(/^\//, ""),
              writable: false,
            });
            files.push(file);
            resolve();
          });
        });
      } else if (entry.isDirectory) {
        const reader = entry.createReader();
        await new Promise(resolve => {
          reader.readEntries(async entries => {
            for (const e of entries) await readEntry(e);
            resolve();
          });
        });
      }
    }

    for (const entry of queue) await readEntry(entry);
    return files;
  }

  async function handleDrop(e) {
    e.preventDefault();
    setDragging(false);
    const files = await collectFiles(Array.from(e.dataTransfer.items));
    processFiles(files);
  }

  function handleDragOver(e) {
    e.preventDefault();
    setDragging(true);
  }

  function handleDragLeave() {
    setDragging(false);
  }

  return (
    <div className="upload-page">

      <div className="upload-logo">
        <div style={{ fontSize:52, marginBottom:10 }}>🌟</div>
        <h1>Lumi <span>Play</span></h1>
        <p className="upload-subtitle">Therapist Dashboard</p>
        {therapistName && (
          <p style={{ marginTop:8, fontSize:13, color:"var(--text-muted)" }}>
            Signed in as <strong>{therapistName}</strong>
          </p>
        )}
      </div>

      <div
        className="upload-box"
        onClick={() => inputRef.current?.click()}
        onDrop={handleDrop}
        onDragOver={handleDragOver}
        onDragLeave={handleDragLeave}
        style={{
          borderColor: dragging ? "var(--teal)" : undefined,
          background: dragging ? "var(--teal-light, #e6f4f1)" : undefined,
          transition: "border-color 0.2s, background 0.2s",
        }}
      >
        <input
          ref={inputRef}
          type="file"
          webkitdirectory="true"
          directory="true"
          multiple
          onChange={handleFolderSelected}
          onClick={(e) => e.stopPropagation()}
        />
        <span className="upload-icon">{dragging ? "📂" : "📁"}</span>
        <h2>{dragging ? "Drop the folder here!" : "Drag & Drop or Click to Select"}</h2>
        <p>
          Drag the <strong>SunvaleData</strong> folder directly from File Explorer,
          or click to browse. All children's sessions will load automatically.
        </p>
      </div>

      <p className="upload-hint">
      </p>
    </div>
  );
}

export default UploadPage;