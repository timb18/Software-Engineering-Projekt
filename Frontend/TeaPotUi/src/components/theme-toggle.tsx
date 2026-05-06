import { useEffect, useState } from "react";

type Theme = "dark" | "light";

const STORAGE_KEY = "teapot-theme";

const getStoredTheme = (): Theme => {
  if (typeof window === "undefined") {
    return "dark";
  }

  return window.localStorage.getItem(STORAGE_KEY) === "light" ? "light" : "dark";
};

const ThemeToggle = () => {
  const [theme, setTheme] = useState<Theme>(getStoredTheme);

  useEffect(() => {
    document.documentElement.dataset.theme = theme;
    window.localStorage.setItem(STORAGE_KEY, theme);
  }, [theme]);

  const isLight = theme === "light";

  return (
    <button
      type="button"
      className="theme-toggle"
      onClick={() => setTheme(isLight ? "dark" : "light")}
      aria-pressed={isLight}
      aria-label={isLight ? "Switch to dark mode" : "Switch to light mode"}
      title={isLight ? "Switch to dark mode" : "Switch to light mode"}
    >
      <span className="theme-toggle__track" aria-hidden="true">
        <span className="theme-toggle__thumb">
          <span className="theme-toggle__icon">{isLight ? "D" : "L"}</span>
        </span>
      </span>
      <span className="theme-toggle__label">{isLight ? "Dark" : "Light"}</span>
    </button>
  );
};

export default ThemeToggle;
