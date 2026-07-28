import { useEffect, useState } from "react";
import { Link, NavLink, useNavigate } from "react-router-dom";
import { useI18n } from "../lib/useI18n";
import { useTheme } from "../lib/useTheme";
import { useAuth } from "../lib/useAuth";
import { Sparkles, Sun, Moon, User, LogOut, LayoutDashboard, ChevronDown } from "lucide-react";
import Logo from "./Logo";
import NotificationBell from "./NotificationBell";
import LanguageSwitcher from "./LanguageSwitcher";

export default function Nav() {
  const { t } = useI18n();
  const { theme, toggleTheme } = useTheme();
  const { profile, logout } = useAuth();
  const navigate = useNavigate();

  function handleAccountClick() {
    if (profile) {
      logout();
      navigate("/");
    }
  }

  return (
    <nav className="sticky top-0 z-50 w-full bg-background/75 backdrop-blur-xl border-b border-border">
      <div className="max-w-7xl mx-auto px-6 h-20 flex items-center justify-between">
        <div className="flex items-center gap-8">
          <Link to="/" className="group flex items-baseline gap-2.5">
            <Logo
              tone={theme === "dark" ? "night" : "default"}
              wordmark
              className="size-7"
              wordmarkClassName="text-2xl"
            />

            <span className="font-body text-sm font-medium tracking-tight text-foreground/40 group-hover:text-foreground/70 transition-colors">
              Luqya
            </span>
          </Link>

          <div className="hidden md:flex gap-7 text-sm font-medium">
            <NavLink
              to="/"
              className={({ isActive }) =>
                isActive
                  ? "text-primary"
                  : "text-foreground/70 hover:text-primary transition-colors"
              }
            >
              {t("navHome")}
            </NavLink>

            <NavLink
              to="/report"
              className={({ isActive }) =>
                isActive
                  ? "text-primary"
                  : "text-foreground/70 hover:text-primary transition-colors"
              }
            >
              {t("navReport")}
            </NavLink>

            <NavLink
              to="/search"
              className={({ isActive }) =>
                `inline-flex items-center gap-1.5 ${
                  isActive ? "text-primary" : "text-foreground/70 hover:text-primary transition-colors"
                }`
              }
            >
              <Sparkles className="size-3.5" />
              {t("navSearch")}
            </NavLink>

            <NavLink
              to="/browse"
              className={({ isActive }) =>
                isActive
                  ? "text-primary"
                  : "text-foreground/70 hover:text-primary transition-colors"
              }
            >
              {t("navBrowse")}
            </NavLink>

            {profile && (
              <NavLink
                to="/dashboard"
                className={({ isActive }) =>
                  isActive
                    ? "text-primary"
                    : "text-foreground/70 hover:text-primary transition-colors"
                }
              >
                {t("navDashboard")}
              </NavLink>
            )}
          </div>
        </div>

        <div className="flex items-center gap-2.5">
          <button
            onClick={toggleTheme}
            aria-label={t("navToggleTheme")}
            className="size-10 rounded-full border border-border grid place-items-center hover:bg-stone-100 transition-colors overflow-hidden relative"
          >
            <Sun
              className={`size-4 absolute transition-all duration-500 ${
                theme === "dark"
                  ? "opacity-0 -rotate-90 scale-50"
                  : "opacity-100 rotate-0 scale-100 text-accent-foreground"
              }`}
            />
            <Moon
              className={`size-4 absolute transition-all duration-500 ${
                theme === "dark"
                  ? "opacity-100 rotate-0 scale-100 text-primary"
                  : "opacity-0 rotate-90 scale-50"
              }`}
            />
          </button>

          <LanguageSwitcher />

          <NotificationBell />

          {profile ? (
            <AccountMenu profile={profile} onLogout={handleAccountClick} t={t} />
          ) : (
            <Link
              to="/auth/login"
              aria-label={t("navLogin")}
              className="hidden sm:grid size-10 rounded-full border border-border place-items-center hover:bg-stone-100 transition-colors"
            >
              <User className="size-4 text-foreground/70" />
            </Link>
          )}

          <Link
            to="/report"
            className="inline-flex items-center gap-1.5 bg-primary text-primary-foreground px-5 py-2.5 rounded-full text-sm font-semibold hover:opacity-90 transition-all hover:-translate-y-0.5 shadow-glow"
          >
            <Sparkles className="size-3.5" />
            {t("ctaStart")}
          </Link>
        </div>
      </div>
    </nav>
  );
}

// Full name if we have one, otherwise the identifier the person actually
// recognizes as "them" — never the raw GUID-adjacent userName as a first
// resort when a real name exists, so the menu doesn't read like a debug
// panel of every profile field at once.
function displayName(profile) {
  return [profile.name, profile.surname].filter(Boolean).join(" ").trim() || profile.userName;
}

function initials(profile) {
  const name = [profile.name, profile.surname].filter(Boolean).join(" ").trim();
  const source = name || profile.userName || profile.email || "?";
  const parts = source.trim().split(/\s+/).filter(Boolean);
  const letters = parts.length > 1 ? parts[0][0] + parts[1][0] : source.slice(0, 2);
  return letters.toUpperCase();
}

function AccountMenu({ profile, onLogout, t }) {
  const [open, setOpen] = useState(false);
  const name = displayName(profile);
  // Only worth a second line if it actually adds information beyond the
  // name already shown — no point echoing "Asma" under "Asma".
  const secondaryLine = profile.email || (name !== profile.userName ? profile.userName : null);

  useEffect(() => {
    if (!open) return;
    function onKeyDown(e) {
      if (e.key === "Escape") setOpen(false);
    }
    document.addEventListener("keydown", onKeyDown);
    return () => document.removeEventListener("keydown", onKeyDown);
  }, [open]);

  return (
    <div className="relative">
      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        aria-haspopup="menu"
        aria-expanded={open}
        aria-label={name}
        className="hidden sm:flex items-center gap-1.5 h-10 ps-1 pe-2.5 rounded-full border border-border hover:border-primary/30 hover:bg-stone-50 transition-colors"
      >
        <span className="grid size-8 place-items-center rounded-full bg-primary/10 text-primary text-xs font-bold font-mono">
          {initials(profile)}
        </span>
        <ChevronDown
          className={`size-3.5 text-foreground/50 transition-transform duration-200 ${open ? "rotate-180" : ""}`}
        />
      </button>

      {open && (
        <>
          <div className="fixed inset-0 z-40" onClick={() => setOpen(false)} />

          <div
            role="menu"
            className="absolute end-0 top-full mt-3 w-72 z-50 rounded-[1.25rem] border border-border bg-card shadow-luxe overflow-hidden animate-rise-in"
          >
            <div className="flex items-center gap-3 px-5 py-4">
              <span className="grid size-11 shrink-0 place-items-center rounded-full bg-primary/10 text-primary font-bold font-mono">
                {initials(profile)}
              </span>
              <div className="min-w-0">
                <p className="text-sm font-bold truncate">{name}</p>
                {secondaryLine && (
                  <p className="text-xs text-muted-foreground truncate" dir="ltr">
                    {secondaryLine}
                  </p>
                )}
              </div>
            </div>

            <div className="h-px bg-border mx-5" />

            <div className="p-1.5">
              <Link
                to="/dashboard"
                role="menuitem"
                onClick={() => setOpen(false)}
                className="flex items-center gap-2.5 px-3.5 py-2.5 rounded-xl text-sm font-medium hover:bg-stone-50 transition-colors"
              >
                <LayoutDashboard className="size-4 text-muted-foreground" />
                {t("navDashboard")}
              </Link>

              <button
                type="button"
                role="menuitem"
                onClick={() => {
                  setOpen(false);
                  onLogout();
                }}
                className="w-full flex items-center gap-2.5 px-3.5 py-2.5 rounded-xl text-sm font-medium text-error hover:bg-error-tint transition-colors"
              >
                <LogOut className="size-4" />
                {t("navLogout")}
              </button>
            </div>
          </div>
        </>
      )}
    </div>
  );
}