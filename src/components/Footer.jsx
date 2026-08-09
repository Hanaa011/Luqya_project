import { Link } from "react-router-dom";
import { useI18n } from "../lib/useI18n";
import { useAuth } from "../lib/useAuth";
import Logo from "./Logo";

export default function Footer() {
  const { t } = useI18n();
  const { profile } = useAuth();

  const year = new Date().getFullYear();

  const linkClass =
    "inline-block py-1 text-sm text-muted-foreground hover:text-primary transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40 focus-visible:ring-offset-2 rounded-sm";

  return (
    <footer className="border-t border-border bg-background">
      <div className="max-w-7xl mx-auto px-6 py-12 lg:py-14">
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-5 gap-10 lg:gap-8">
          {/* Brand */}
          <div className="sm:col-span-2 lg:col-span-2">
            <Link
              to="/"
              className="inline-flex items-center gap-3 mb-4 group rounded-md focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
             
            >
              <Logo
                wordmark
                className="size-7"
                wordmarkClassName="text-2xl"
              />

              <span className="font-body text-base font-medium text-foreground/40 group-hover:text-foreground/60 transition-colors">
                Luqya
              </span>
            </Link>

            <p className="text-sm text-muted-foreground max-w-sm leading-7">
              {t("footTag")}
            </p>
          </div>

          {/* Navigation */}
          <div>
            <h5 className="font-bold mb-4 text-sm text-foreground">
              {t("footNavigation")}
            </h5>

            <ul className="space-y-2">
              <li>
                <Link to="/" className={linkClass}>
                  {t("navHome")}
                </Link>
              </li>

              <li>
                <Link to="/report" className={linkClass}>
                  {t("navReport")}
                </Link>
              </li>

              <li>
                <Link to="/search" className={linkClass}>
                  {t("navSearch")}
                </Link>
              </li>
            </ul>
          </div>

          {/* Account */}
          <div>
            <h5 className="font-bold mb-4 text-sm text-foreground">
              {t("footAccount")}
            </h5>

            <ul className="space-y-2">
              {profile ? (
                <>
                  <li>
                    <Link to="/browse" className={linkClass}>
                      {t("navBrowse")}
                    </Link>
                  </li>

                  <li>
                    <Link to="/dashboard" className={linkClass}>
                      {t("navDashboard")}
                    </Link>
                  </li>
                </>
              ) : (
                <>
                  <li>
                    <Link to="/auth/login" className={linkClass}>
                      {t("footLogin")}
                    </Link>
                  </li>

                  <li>
                    <Link to="/auth/register" className={linkClass}>
                      {t("footRegister")}
                    </Link>
                  </li>
                </>
              )}
            </ul>
          </div>

          {/* Copyright */}
          <div className="sm:col-span-2 lg:col-span-1 flex lg:justify-end">
            <p className="text-xs text-muted-foreground/70 leading-relaxed lg:text-end">
              {t("footRights").replace("{year}", year)}
            </p>
          </div>
        </div>
      </div>
    </footer>
  );
}