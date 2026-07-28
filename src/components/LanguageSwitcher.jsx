import { useEffect, useRef, useState } from "react";
import { Check, Globe, Search } from "lucide-react";
import { useI18n } from "../lib/useI18n";

export default function LanguageSwitcher() {
  const { locale, setLocale, locales, meta, dir } = useI18n();
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState("");
  const panelRef = useRef(null);

  useEffect(() => {
    if (!open) return undefined;

    function handleKey(event) {
      if (event.key === "Escape") {
        setOpen(false);
        setQuery("");
      }
    }

    window.addEventListener("keydown", handleKey);
    return () => window.removeEventListener("keydown", handleKey);
  }, [open]);

  function close() {
    setOpen(false);
    setQuery("");
  }

  const filtered = locales.filter((l) => {
    const q = query.trim().toLowerCase();
    if (!q) return true;
    return (
      l.nativeName.toLowerCase().includes(q) ||
      l.englishName.toLowerCase().includes(q) ||
      l.code.includes(q)
    );
  });

  return (
    <div className="relative" ref={panelRef}>
      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        aria-haspopup="listbox"
        aria-expanded={open}
        className="h-10 inline-flex items-center gap-2 px-3.5 rounded-full border border-border hover:bg-stone-100 transition-colors"
      >
        <span className="text-base leading-none">{meta.flag}</span>
        <span className="text-[11px] font-mono uppercase tracking-widest hidden sm:inline">
          {meta.code}
        </span>
        <Globe className="size-3.5 text-muted-foreground sm:hidden" />
      </button>

      {open && (
        <>
          <div className="fixed inset-0 z-40" onClick={close} />

          <div
            role="listbox"
            dir={dir}
            className="absolute end-0 top-full mt-3 w-72 z-50 rounded-2xl border border-border bg-card shadow-luxe overflow-hidden animate-rise-in"
          >
            <div className="p-3 border-b border-border">
              <div className="relative">
                <Search className="absolute top-1/2 -translate-y-1/2 start-3.5 size-3.5 text-muted-foreground" />
                <input
                  autoFocus
                  value={query}
                  onChange={(e) => setQuery(e.target.value)}
                  placeholder={
                    locale === "ar"
                      ? "ابحث عن لغة..."
                      : locale === "ur"
                      ? "زبان تلاش کریں..."
                      : "Search languages..."
                  }
                  className="w-full ps-9 pe-3 py-2.5 rounded-xl bg-stone-50 border border-stone-200 text-sm focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/10 transition-all"
                />
              </div>
            </div>

            <ul className="max-h-72 overflow-y-auto py-1.5">
              {filtered.map((l) => {
                const active = l.code === locale;
                return (
                  <li key={l.code}>
                    <button
                      type="button"
                      role="option"
                      aria-selected={active}
                      onClick={() => {
                        setLocale(l.code);
                        setOpen(false);
                      }}
                      className={`w-full flex items-center gap-3 px-4 py-3 text-start transition-colors ${
                        active ? "bg-primary/5" : "hover:bg-stone-50"
                      }`}
                    >
                      <span className="text-xl leading-none">{l.flag}</span>

                      <span className="flex-1 min-w-0">
                        <span
                          className={`block text-sm font-semibold truncate ${l.font}`}
                          dir={l.dir}
                        >
                          {l.nativeName}
                        </span>
                        <span className="block text-xs text-muted-foreground">
                          {l.englishName}
                        </span>
                      </span>

                      {active && <Check className="size-4 text-primary shrink-0" />}
                    </button>
                  </li>
                );
              })}

              {filtered.length === 0 && (
                <li className="px-4 py-6 text-center text-sm text-muted-foreground">
                  {locale === "ar" ? "لا نتائج" : locale === "ur" ? "کوئی نتیجہ نہیں" : "No languages found"}
                </li>
              )}
            </ul>

            <div className="px-4 py-3 border-t border-border text-[10px] font-mono uppercase tracking-widest text-muted-foreground/70">
              {locale === "ar"
                ? "المزيد من اللغات قريبًا"
                : locale === "ur"
                ? "مزید زبانیں جلد آ رہی ہیں"
                : "More languages coming soon"}
            </div>
          </div>
        </>
      )}
    </div>
  );
}
