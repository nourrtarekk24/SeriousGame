// ─────────────────────────────────────────────────────────────────────
// LoginPage.jsx — Therapist login and registration
// ─────────────────────────────────────────────────────────────────────

import React, { useState } from "react";
import config from "../config/config";

const DEFAULT_PASSCODE = "lumiplay2026";

// ── Inline styles so this renders correctly even during CSS issues ──
const S = {
  page: {
    minHeight: "100vh",
    display: "flex",
    alignItems: "center",
    justifyContent: "center",
    background: "linear-gradient(135deg, #1e2d4a 0%, #0a4a6e 50%, #0d7377 100%)",
    padding: 24,
    fontFamily: "'DM Sans', 'Segoe UI', sans-serif",
  },
  card: {
    background: "#ffffff",
    borderRadius: 20,
    padding: "48px 40px 36px",
    width: "100%",
    maxWidth: 440,
    boxShadow: "0 20px 60px rgba(0,0,0,0.25)",
  },
  logoWrap: {
    textAlign: "center",
    marginBottom: 36,
  },
  logoIcon: {
    fontSize: 48,
    display: "block",
    marginBottom: 10,
  },
  logoTitle: {
    fontFamily: "'Playfair Display', Georgia, serif",
    fontSize: 32,
    color: "#1a2340",
    margin: "0 0 4px",
    fontWeight: 700,
  },
  logoAccent: { color: "#0d7377" },
  logoSub: {
    fontSize: 12,
    color: "#6b7a99",
    letterSpacing: "1.5px",
    textTransform: "uppercase",
    fontWeight: 500,
  },
  toggle: {
    display: "flex",
    background: "#f4f6fb",
    borderRadius: 8,
    padding: 4,
    marginBottom: 28,
  },
  toggleBtn: (active) => ({
    flex: 1,
    border: "none",
    background: active ? "#fff" : "transparent",
    color: active ? "#0d7377" : "#6b7a99",
    padding: "10px 0",
    borderRadius: 6,
    fontSize: 14,
    fontWeight: active ? 700 : 500,
    cursor: "pointer",
    boxShadow: active ? "0 1px 4px rgba(30,45,74,0.1)" : "none",
    transition: "all 0.2s",
    fontFamily: "inherit",
  }),
  form: {
    display: "flex",
    flexDirection: "column",
    gap: 18,
  },
  field: {
    display: "flex",
    flexDirection: "column",
    gap: 6,
  },
  label: {
    fontSize: 13,
    fontWeight: 600,
    color: "#1a2340",
    letterSpacing: "0.3px",
  },
  input: {
    border: "1.5px solid #e2e8f5",
    borderRadius: 8,
    padding: "12px 16px",
    fontSize: 15,
    color: "#1a2340",
    background: "#f4f6fb",
    outline: "none",
    fontFamily: "inherit",
    transition: "border-color 0.2s",
  },
  hint: {
    fontSize: 12,
    color: "#6b7a99",
    lineHeight: 1.5,
    marginTop: 2,
  },
  error: {
    fontSize: 13,
    color: "#d93025",
    background: "#fdecea",
    padding: "10px 14px",
    borderRadius: 6,
    borderLeft: "3px solid #d93025",
  },
  submitBtn: (disabled) => ({
    background: disabled ? "#a0c4c5" : "#0d7377",
    color: "#fff",
    border: "none",
    padding: "14px",
    borderRadius: 8,
    fontSize: 15,
    fontWeight: 700,
    cursor: disabled ? "not-allowed" : "pointer",
    display: "flex",
    alignItems: "center",
    justifyContent: "center",
    gap: 8,
    marginTop: 4,
    transition: "all 0.2s",
    fontFamily: "inherit",
  }),
  status: {
    display: "flex",
    alignItems: "center",
    gap: 8,
    marginTop: 24,
    paddingTop: 20,
    borderTop: "1px solid #e2e8f5",
    fontSize: 12,
    color: "#6b7a99",
  },
  dot: (online) => ({
    width: 8,
    height: 8,
    borderRadius: "50%",
    background: online ? "#1a9e5a" : "#e8920a",
    boxShadow: `0 0 6px ${online ? "#1a9e5a" : "#e8920a"}`,
    flexShrink: 0,
  }),
};

function LoginPage({ onLogin }) {
  const [mode,     setMode]     = useState("login");
  const [name,     setName]     = useState("");
  const [email,    setEmail]    = useState("");
  const [password, setPassword] = useState("");
  const [error,    setError]    = useState("");
  const [loading,  setLoading]  = useState(false);
  const [focused,  setFocused]  = useState("");

  function hashString(str) {
    let hash = 0;
    for (let i = 0; i < str.length; i++) {
      hash = ((hash << 5) - hash) + str.charCodeAt(i);
      hash |= 0;
    }
    return hash.toString(36);
  }

  function getAccounts() {
    try { return JSON.parse(localStorage.getItem("lumi_accounts") || "[]"); }
    catch { return []; }
  }

  function saveAccount(account) {
    const accounts = getAccounts();
    accounts.push(account);
    localStorage.setItem("lumi_accounts", JSON.stringify(accounts));
  }

  function handleLogin(e) {
    e.preventDefault();
    setError("");
    setLoading(true);
    setTimeout(() => {
      const accounts = getAccounts();

      // No accounts yet — accept default passcode OR email+password
      if (accounts.length === 0) {
        if (password === DEFAULT_PASSCODE) {
          onLogin({ name: "Therapist", email: "default" });
          setLoading(false);
          return;
        }
      }

      const match = accounts.find(
        (a) => a.email.toLowerCase() === email.toLowerCase() &&
               a.passwordHash === hashString(password)
      );
      if (match) {
        onLogin({ name: match.name, email: match.email });
      } else {
        setError("Email or password is incorrect. If this is your first time, register an account first.");
      }
      setLoading(false);
    }, 500);
  }

  function handleRegister(e) {
    e.preventDefault();
    setError("");
    if (!name.trim())          { setError("Please enter your name."); return; }
    if (!email.includes("@")) { setError("Please enter a valid email address."); return; }
    if (password.length < 6)  { setError("Password must be at least 6 characters."); return; }
    const accounts = getAccounts();
    if (accounts.find((a) => a.email.toLowerCase() === email.toLowerCase())) {
      setError("An account with this email already exists."); return;
    }
    setLoading(true);
    setTimeout(() => {
      saveAccount({ name: name.trim(), email: email.toLowerCase(), passwordHash: hashString(password) });
      onLogin({ name: name.trim(), email: email.toLowerCase() });
      setLoading(false);
    }, 500);
  }

  const inputStyle = (fieldName) => ({
    ...S.input,
    borderColor: focused === fieldName ? "#0d7377" : "#e2e8f5",
    boxShadow: focused === fieldName ? "0 0 0 3px rgba(13,115,119,0.12)" : "none",
    background: focused === fieldName ? "#fff" : "#f4f6fb",
  });

  return (
    <div style={S.page}>
      <div style={S.card}>

        {/* Logo */}
        <div style={S.logoWrap}>
          <span style={S.logoIcon}>🌟</span>
          <h1 style={S.logoTitle}>
            Lumi <span style={S.logoAccent}>Play</span>
          </h1>
          <p style={S.logoSub}>Therapist Dashboard</p>
        </div>

        {/* Mode toggle */}
        <div style={S.toggle}>
          <button
            style={S.toggleBtn(mode === "login")}
            onClick={() => { setMode("login"); setError(""); }}
            type="button"
          >
            Sign In
          </button>
          <button
            style={S.toggleBtn(mode === "register")}
            onClick={() => { setMode("register"); setError(""); }}
            type="button"
          >
            Register
          </button>
        </div>

        {/* Form */}
        <form onSubmit={mode === "login" ? handleLogin : handleRegister} style={S.form}>

          {mode === "register" && (
            <div style={S.field}>
              <label style={S.label}>Full Name</label>
              <input
                type="text"
                placeholder="Dr. Sarah Ahmed"
                value={name}
                onChange={(e) => setName(e.target.value)}
                onFocus={() => setFocused("name")}
                onBlur={() => setFocused("")}
                style={inputStyle("name")}
                autoComplete="name"
              />
            </div>
          )}

          {/* Always show email + password */}
          <div style={S.field}>
            <label style={S.label}>Email Address</label>
            <input
              type="email"
              placeholder="doctor@clinic.com"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              onFocus={() => setFocused("email")}
              onBlur={() => setFocused("")}
              style={inputStyle("email")}
              autoComplete="email"
            />
          </div>
          <div style={S.field}>
            <label style={S.label}>Password</label>
            <input
              type="password"
              placeholder={mode === "register" ? "Min. 6 characters" : "Your password"}
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              onFocus={() => setFocused("pass")}
              onBlur={() => setFocused("")}
              style={inputStyle("pass")}
              autoComplete={mode === "register" ? "new-password" : "current-password"}
            />
          </div>

          {error && <p style={S.error}>{error}</p>}

          <button type="submit" style={S.submitBtn(loading)} disabled={loading}>
            {loading
              ? "Please wait..."
              : mode === "login"
              ? "Sign In  →"
              : "Create Account  →"}
          </button>

        </form>

        {/* Firebase status */}
        <div style={S.status}>
          
          <span>
          
              
              
          </span>
        </div>

      </div>
    </div>
  );
}

export default LoginPage;