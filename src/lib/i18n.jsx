import { useEffect, useState } from "react";
import { DEFAULT_LOCALE, LOCALES, getLocaleMeta } from "../i18n/locales";
import { resources } from "../i18n/resources";
import { setAcceptLanguage } from "../api/httpClient";
import { I18nContext } from "./i18nContext";
import { translate, pick } from "./translate";

const STORAGE_KEY = "luqya-locale";

export function I18nProvider({ children }) {
  const [locale, setLocaleState] = useState(() => {
    const saved = window.localStorage.getItem(STORAGE_KEY);
    return saved && resources[saved] ? saved : DEFAULT_LOCALE;
  });

  useEffect(() => {
    const meta = getLocaleMeta(locale);
    document.documentElement.lang = locale;
    document.documentElement.dir = meta.dir;
    document.documentElement.setAttribute("data-locale", locale);
    setAcceptLanguage(locale);
  }, [locale]);

  function setLocale(nextCode) {
    if (!resources[nextCode]) return;
    setLocaleState(nextCode);
    window.localStorage.setItem(STORAGE_KEY, nextCode);
  }

  const meta = getLocaleMeta(locale);

  const value = {
    locale,
    lang: locale, // alias — every existing page already destructures `lang`
    setLocale,
    setLang: setLocale, // alias — same reasoning
    locales: LOCALES,
    dir: meta.dir,
    meta,
    t: (key, vars) => translate(locale, key, vars),
    tr: (map) => pick(locale, map),
  };

  return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>;
}
