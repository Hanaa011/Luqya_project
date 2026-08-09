import { useEffect, useRef } from "react";
import { NavLink, Link } from "react-router-dom";
import { Sparkles, Sun, Moon, User, LogOut, LayoutDashboard, ListChecks } from "lucide-react";

// Full name if we have one, otherwise the identifier the person actually
// recognizes as "them" — matches Nav.jsx's own AccountMenu so mobile and
// desktop never disagree about what to call the user.
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

const linkClass = ({ isActive }) =>
  `flex items-center gap-3 px-4 py-3 rounded-xl text-[15px] font-semibold transition-colors ${
    isActive ? "bg-primary/10 text-primary" : "text-foreground/80 hover:bg-stone-100"
  }`;

const FOCUSABLE_SELECTOR =
  'a[href], button:not([disabled]), [tabindex]:not([tabindex="-1"])';

/**
 * `open` only ever controls visibility/behavior via CSS + effects — this
 * component stays mounted at all times (no `if (!open) return null`), so
 * closing can actually play an exit transition instead of vanishing
 * instantly. Previously it unmounted immediately on close, which is why
 * there was never a slide-out/fade-out — there was nothing left to
 * animate by the time `open` became false.
 */
export default function MobileMenu({ open, onClose, triggerRef, profile, onLogout, t, theme, toggleTheme }) {
  const panelRef = useRef(null);

  // Background scroll lock while the panel is open, restored the instant
  // it closes (not deferred until the exit animation finishes — the page
  // underneath should scroll again as soon as the user has dismissed it).
  useEffect(() => {
    if (!open) return undefined;
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    return () => {
      document.body.style.overflow = previousOverflow;
    };
  }, [open]);

  // Focus management: move focus into the panel on open, return it to
  // whatever opened the panel (the hamburger button) on close — never
  // left stranded on a now-hidden/inert element.
  useEffect(() => {
    if (open) {
      const firstFocusable = panelRef.current?.querySelector(FOCUSABLE_SELECTOR);
      firstFocusable?.focus();
    } else {
      triggerRef?.current?.focus();
    }
  }, [open, triggerRef]);

  // Escape closes; Tab/Shift+Tab are trapped inside the panel while open
  // so keyboard focus can never silently land on inert content behind
  // the backdrop.
  useEffect(() => {
    if (!open) return undefined;

    function onKeyDown(e) {
      if (e.key === "Escape") {
        onClose();
        return;
      }

      if (e.key !== "Tab" || !panelRef.current) return;

      const focusable = Array.from(panelRef.current.querySelectorAll(FOCUSABLE_SELECTOR));
      if (focusable.length === 0) return;

      const first = focusable[0];
      const last = focusable[focusable.length - 1];

      if (e.shiftKey && document.activeElement === first) {
        e.preventDefault();
        last.focus();
      } else if (!e.shiftKey && document.activeElement === last) {
        e.preventDefault();
        first.focus();
      }
    }

    document.addEventListener("keydown", onKeyDown);
    return () => document.removeEventListener("keydown", onKeyDown);
  }, [open, onClose]);

  const name = profile ? displayName(profile) : null;
  const secondaryLine = profile ? profile.email || (name !== profile.userName ? profile.userName : null) : null;

  return (
    <div className="md:hidden" aria-hidden={!open} inert={!open}>
      {/* Full-page backdrop, dimming + blurring everything below the nav
          bar. Tapping it closes the menu (in addition to: tapping the
          hamburger again, picking a nav item, or pressing Escape). */}
      <div
        className={`fixed inset-0 top-16 sm:top-20 z-[55] bg-black/60 backdrop-blur-sm transition-opacity duration-300 ease-out ${
          open ? "opacity-100" : "pointer-events-none opacity-0"
        }`}
        onClick={onClose}
      />

      <div
        id="mobile-menu-panel"
        ref={panelRef}
        role="dialog"
        aria-modal="true"
        className={`fixed inset-x-3 top-[4.25rem] sm:top-[5.25rem] z-[60] max-h-[calc(100vh-6rem)] overflow-y-auto rounded-[1.75rem] border border-border bg-card shadow-luxe transition-all duration-300 ease-out ${
          open
            ? "opacity-100 translate-y-0 scale-100"
            : "pointer-events-none opacity-0 -translate-y-2 scale-[0.98]"
        }`}
      >
        {profile && (
          <>
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
          </>
        )}

        <nav className="p-3 space-y-1">
          <NavLink to="/" onClick={onClose} className={linkClass}>
            {t("navHome")}
          </NavLink>
          <NavLink to="/report" onClick={onClose} className={linkClass}>
            {t("navReport")}
          </NavLink>
          <NavLink to="/search" onClick={onClose} className={linkClass}>
            <Sparkles className="size-4" />
            {t("navSearch")}
          </NavLink>

          {profile && (
            <NavLink to="/browse" onClick={onClose} className={linkClass}>
              <ListChecks className="size-4" />
              {t("navBrowse")}
            </NavLink>
          )}

          {profile && (
            <NavLink to="/dashboard" onClick={onClose} className={linkClass}>
              <LayoutDashboard className="size-4" />
              {t("navDashboard")}
            </NavLink>
          )}
        </nav>

        <div className="h-px bg-border mx-5" />

        <div className="p-3 space-y-1">
          <button
            type="button"
            onClick={toggleTheme}
            className="w-full flex items-center gap-3 px-4 py-3 rounded-xl text-[15px] font-semibold text-foreground/80 hover:bg-stone-100 transition-colors"
          >
            {theme === "dark" ? <Sun className="size-4" /> : <Moon className="size-4" />}
            {theme === "dark" ? t("navLightMode") : t("navDarkMode")}
          </button>

          {profile ? (
            <button
              type="button"
              onClick={() => {
                onClose();
                onLogout();
              }}
              className="w-full flex items-center gap-3 px-4 py-3 rounded-xl text-[15px] font-semibold text-error hover:bg-error-tint transition-colors"
            >
              <LogOut className="size-4" />
              {t("navLogout")}
            </button>
          ) : (
            <Link
              to="/auth/login"
              onClick={onClose}
              className="w-full flex items-center gap-3 px-4 py-3 rounded-xl text-[15px] font-semibold text-foreground/80 hover:bg-stone-100 transition-colors"
            >
              <User className="size-4" />
              {t("navLogin")}
            </Link>
          )}
        </div>
      </div>
    </div>
  );
}
