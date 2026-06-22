// ─────────────────────────────────────────────────────────────────────
// config.js — all app settings. The therapist never sees this file.
// ─────────────────────────────────────────────────────────────────────

const config = {
  // ── Gemini API ────────────────────────────────────────────────────
  GEMINI_API_KEY: "AIzaSyAaV_mnbP8232AeygjD8E5kASU4KemhGYk",
  GEMINI_URL:
    "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent",

  // ── Firebase ──────────────────────────────────────────────────────
  // Set to true now that Firebase is set up.
  // Set to false to fall back to manual folder upload.
  USE_FIREBASE: true,
    GROQ_API_KEY: "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",


  APP_NAME: "Lumi Play",
  APP_SUBTITLE: "Therapist Dashboard",
};

export default config;