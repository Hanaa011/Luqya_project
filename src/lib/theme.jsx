import { useEffect, useState } from "react";
import { ThemeContext } from "./themeContext";

export function ThemeProvider({ children }) {
  const [theme, setThemeState] = useState(() => {
    const saved = window.localStorage.getItem("luqya-theme");
    if (saved === "light" || saved === "dark") return saved;

    const prefersDark = window.matchMedia?.("(prefers-color-scheme: dark)").matches;
    return prefersDark ? "dark" : "light";
  });

  useEffect(() => {
    document.documentElement.classList.toggle("dark", theme === "dark");
  }, [theme]);

  const setTheme = (next) => {
    setThemeState(next);
    window.localStorage.setItem("luqya-theme", next);
  };

  const toggleTheme = () => {
    setTheme(theme === "dark" ? "light" : "dark");
  };

  return (
    <ThemeContext.Provider value={{ theme, setTheme, toggleTheme }}>
      {children}
    </ThemeContext.Provider>
  );
}
