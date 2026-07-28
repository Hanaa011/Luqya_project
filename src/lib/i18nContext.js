import { createContext } from "react";
import { LOCALES, DEFAULT_LOCALE, getLocaleMeta } from "../i18n/locales";

export const I18nContext = createContext({
  locale: DEFAULT_LOCALE,
  lang: DEFAULT_LOCALE,
  setLocale: () => {},
  setLang: () => {},
  locales: LOCALES,
  dir: "rtl",
  meta: getLocaleMeta(DEFAULT_LOCALE),
  t: (key) => key,
  tr: (map) => map[DEFAULT_LOCALE],
});
