import { Link } from "react-router-dom";
import { useI18n } from "../lib/useI18n";
import Logo from "./Logo";

export default function Footer() {
  const { t } = useI18n();

  return (
    <footer className="py-20 border-t border-border bg-background">
      <div className="max-w-7xl mx-auto px-6 grid grid-cols-2 md:grid-cols-4 lg:grid-cols-6 gap-12">
        <div className="col-span-2">
          <div className="flex items-baseline gap-2.5 mb-5">
            <Logo tone="default" wordmark className="size-7" wordmarkClassName="text-2xl" />

            <span className="font-body text-base font-medium text-foreground/40">
              Luqya
            </span>
          </div>

          <p className="text-sm text-muted-foreground max-w-xs leading-relaxed">
            {t("footTag")}
          </p>
        </div>

        <div>
          <h5 className="font-bold mb-4 text-sm">
            {t("footProduct")}
          </h5>

          <ul className="text-sm text-muted-foreground space-y-2.5">
            <li>
              <a
                href="#features"
                className="hover:text-primary transition-colors"
              >
                {t("footFeatures")}
              </a>
            </li>

            <li>
              <a
                href="#how"
                className="hover:text-primary transition-colors"
              >
                {t("footAiLogic")}
              </a>
            </li>

            <li>
              <a
                href="#security"
                className="hover:text-primary transition-colors"
              >
                {t("footSecurity")}
              </a>
            </li>
          </ul>
        </div>

        <div>
          <h5 className="font-bold mb-4 text-sm">
            {t("footCompany")}
          </h5>

          <ul className="text-sm text-muted-foreground space-y-2.5">
            <li>
              <a
                href="#about"
                className="hover:text-primary transition-colors"
              >
                {t("footAbout")}
              </a>
            </li>

            <li>
              <a
                href="#leap"
                className="hover:text-primary transition-colors"
              >
                {t("footLeap")}
              </a>
            </li>

            <li>
              <a
                href="#careers"
                className="hover:text-primary transition-colors"
              >
                {t("footCareers")}
              </a>
            </li>
          </ul>
        </div>

        <div className="col-span-2 flex flex-col items-start md:items-end">
          <div className="flex gap-3 mb-6">
            <a
              href="#"
              className="size-10 rounded-full border border-border flex items-center justify-center hover:bg-stone-100 transition-colors font-mono text-sm"
            >
              𝕏
            </a>

            <a
              href="#"
              className="size-10 rounded-full border border-border flex items-center justify-center hover:bg-stone-100 transition-colors font-mono text-xs font-bold"
            >
              in
            </a>
          </div>

          <p className="text-[10px] font-mono text-muted-foreground uppercase tracking-widest">
            {t("footRights")}
          </p>

          <Link
            to="/admin/notify"
            className="mt-3 text-[10px] font-mono text-muted-foreground/50 uppercase tracking-widest hover:text-primary transition-colors"
          >
            Staff · Notification console
          </Link>
        </div>
      </div>
    </footer>
  );
}