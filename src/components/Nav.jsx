import { useEffect, useState, useRef } from "react";
import { Link, NavLink, useNavigate } from "react-router-dom";
import { useI18n } from "../lib/useI18n";
import { useTheme } from "../lib/useTheme";
import { useAuth } from "../lib/useAuth";
import { useConversations } from "../lib/useConversations";
import {
  Sun,
  Moon,
  User,
  LogOut,
  LayoutDashboard,
  MessageSquare,
  ChevronDown,
  Menu,
} from "lucide-react";
import Logo from "./Logo";
import NotificationBell from "./NotificationBell";
import LanguageSwitcher from "./LanguageSwitcher";
import MobileMenu from "./MobileMenu";

const desktopNavClass = ({ isActive }) =>
  [
    "relative inline-flex h-10 items-center whitespace-nowrap px-1 text-sm transition-colors duration-200",
    "after:absolute after:inset-x-1 after:-bottom-[1px] after:h-[2px] after:rounded-full after:transition-all after:duration-200",
    isActive
      ? "font-semibold text-foreground after:bg-primary"
      : "font-medium text-foreground/60 hover:text-foreground after:bg-transparent",
  ].join(" ");

export default function Nav() {
  const { t } = useI18n();
  const { theme, toggleTheme } = useTheme();
  const { profile, logout } = useAuth();
  const { totalUnread } = useConversations();
  const navigate = useNavigate();
  const [mobileOpen, setMobileOpen] = useState(false);
  const mobileMenuTriggerRef = useRef(null);

  function handleAccountClick() {
    if (profile) {
      navigate("/", { replace: true });
      logout();
    }
  }

  return (
    <nav className="sticky top-0 z-50 w-full border-b border-border/80 bg-background/90 backdrop-blur-xl">
      <div className="mx-auto flex h-16 max-w-[1440px] items-center justify-between px-4 sm:h-[72px] sm:px-6 lg:px-8">
        {/* Brand + primary navigation */}
        <div className="flex min-w-0 items-center gap-8 lg:gap-10">
          <Link
            to="/"
            aria-label={t("navHome")}
            className="group flex shrink-0 items-center focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/20 rounded-lg"
          >
            <Logo
              tone={theme === "dark" ? "night" : "default"}
              wordmark
              className="size-6 sm:size-7"
              wordmarkClassName="text-xl sm:text-2xl"
            />
          </Link>

          <div className="hidden md:flex items-center gap-5 lg:gap-7">
            <NavLink to="/" className={desktopNavClass}>
              {t("navHome")}
            </NavLink>

            <NavLink to="/report" className={desktopNavClass}>
              {t("navReport")}
            </NavLink>

            <NavLink to="/search" className={desktopNavClass}>
              {t("navSearch")}
            </NavLink>

            {profile && (
              <NavLink to="/browse" className={desktopNavClass}>
                {t("navBrowse")}
              </NavLink>
            )}

            {profile && (
              <NavLink to="/dashboard" className={desktopNavClass}>
                {t("navDashboard")}
              </NavLink>
            )}
          </div>
        </div>

        {/* Utility actions — grouped by purpose for clearer visual hierarchy */}
        <div className="flex shrink-0 items-center gap-1 sm:gap-1.5">
          {/* Account — identity control, intentionally the only pill-like utility */}
          {profile ? (
            <AccountMenu profile={profile} onLogout={handleAccountClick} t={t} />
          ) : (
            <Link
              to="/auth/login"
              aria-label={t("navLogin")}
              className="hidden size-10 place-items-center rounded-xl text-foreground/60 transition-colors duration-200 hover:bg-stone-100 hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/20 md:grid"
            >
              <User className="size-[18px]" strokeWidth={1.8} />
            </Link>
          )}

          {/* Identity / communication divider */}
          <span
            aria-hidden="true"
            className="mx-1 hidden h-5 w-px bg-border/80 md:block"
          />

          {/* Communication actions — icon-led, no permanent circular borders */}
          {profile && (
            <NavLink
              to="/messages"
              aria-label={t("navMessages")}
              title={t("navMessages")}
              className={({ isActive }) =>
                `relative hidden size-10 place-items-center rounded-xl transition-all duration-200 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/20 md:grid ${
                  isActive
                    ? "bg-primary/[0.08] text-primary"
                    : "text-foreground/55 hover:bg-stone-100 hover:text-foreground"
                }`
              }
            >
              <MessageSquare className="size-[18px]" strokeWidth={1.75} />
              {totalUnread > 0 && (
                <span className="absolute end-1 top-1 inline-flex min-h-[15px] min-w-[15px] items-center justify-center rounded-full bg-accent px-1 text-[9px] font-bold leading-none text-white ring-2 ring-background">
                  {totalUnread > 9 ? "9+" : totalUnread}
                </span>
              )}
            </NavLink>
          )}

          <NotificationBell />

          {/* Communication / preferences divider */}
          <span
            aria-hidden="true"
            className="mx-1 hidden h-5 w-px bg-border/80 md:block"
          />

          {/* Preferences */}
          <LanguageSwitcher />

          <button
            type="button"
            onClick={toggleTheme}
            aria-label={t("navToggleTheme")}
            className="relative hidden size-10 place-items-center overflow-hidden rounded-xl text-foreground/55 transition-all duration-200 hover:bg-stone-100 hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/20 md:grid"
          >
            <Sun
              className={`absolute size-[18px] transition-all duration-300 ${
                theme === "dark"
                  ? "scale-75 -rotate-90 opacity-0"
                  : "rotate-0 scale-100 text-amber-500 opacity-100"
              }`}
              strokeWidth={1.9}
            />
            <Moon
              className={`absolute size-[18px] transition-all duration-300 ${
                theme === "dark"
                  ? "rotate-0 scale-100 text-primary opacity-100"
                  : "scale-75 rotate-90 opacity-0"
              }`}
              strokeWidth={1.8}
            />
          </button>

          <button
            ref={mobileMenuTriggerRef}
            type="button"
            onClick={() => setMobileOpen((v) => !v)}
            aria-label={t(mobileOpen ? "navCloseMenu" : "navOpenMenu")}
            aria-expanded={mobileOpen}
            aria-controls="mobile-menu-panel"
            className="grid size-11 place-items-center rounded-full transition-colors hover:bg-stone-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/20 md:hidden"
          >
            <Menu className="size-5" />
          </button>
        </div>
      </div>

      <MobileMenu
        open={mobileOpen}
        onClose={() => setMobileOpen(false)}
        triggerRef={mobileMenuTriggerRef}
        profile={profile}
        onLogout={handleAccountClick}
        t={t}
        theme={theme}
        toggleTheme={toggleTheme}
        messagesUnread={totalUnread}
      />
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
        className="hidden h-10 items-center gap-1.5 rounded-full border border-border/80 bg-background ps-1 pe-2.5 transition-all duration-200 hover:border-primary/25 hover:bg-stone-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/20 md:flex"
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
