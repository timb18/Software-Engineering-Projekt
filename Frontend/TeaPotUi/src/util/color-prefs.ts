export type RgbColor = { r: number; g: number; b: number };

export const DEFAULT_ORG_COLOR: RgbColor = { r: 16, g: 185, b: 129 };   // emerald
export const DEFAULT_BREAK_COLOR: RgbColor = { r: 245, g: 158, b: 11 }; // amber

const orgKey = (orgId: string) => `teapot-color-org-${orgId}`;
const BREAK_KEY = "teapot-color-breaks";

const isRgbColor = (value: unknown): value is RgbColor => {
  const color = value as Partial<RgbColor> | undefined;
  return (
    typeof color?.r === "number" &&
    typeof color.g === "number" &&
    typeof color.b === "number"
  );
};

export function parseColorPreference(
  raw: string | null | undefined,
  fallback: RgbColor,
): RgbColor {
  if (!raw) return { ...fallback };

  try {
    const parsed = JSON.parse(raw);
    return isRgbColor(parsed) ? parsed : { ...fallback };
  } catch {
    return { ...fallback };
  }
}

export function parseOrgColorPreferences(
  raw: string | null | undefined,
): Record<string, RgbColor> {
  if (!raw) return {};

  try {
    const parsed = JSON.parse(raw);
    if (!parsed || typeof parsed !== "object" || Array.isArray(parsed)) return {};

    return Object.entries(parsed).reduce<Record<string, RgbColor>>(
      (colors, [orgId, color]) => {
        if (isRgbColor(color)) colors[orgId] = color;
        return colors;
      },
      {},
    );
  } catch {
    return {};
  }
}

export function serializeColorPreference(color: RgbColor): string {
  return JSON.stringify(color);
}

export function serializeOrgColorPreferences(
  colors: Record<string, RgbColor>,
): string {
  return JSON.stringify(colors);
}

export function applyStoredColorPreferences(
  breakColor: string | null | undefined,
  orgColors: string | null | undefined,
): void {
  setBreakColor(parseColorPreference(breakColor, DEFAULT_BREAK_COLOR));
  for (const [orgId, color] of Object.entries(parseOrgColorPreferences(orgColors))) {
    setOrgColor(orgId, color);
  }
}

export function getOrgColor(orgId: string): RgbColor {
  try {
    const raw = localStorage.getItem(orgKey(orgId));
    if (raw) return JSON.parse(raw) as RgbColor;
  } catch { /* ignore */ }
  return { ...DEFAULT_ORG_COLOR };
}

export function setOrgColor(orgId: string, color: RgbColor): void {
  localStorage.setItem(orgKey(orgId), JSON.stringify(color));
  window.dispatchEvent(new Event("teapot-colors-changed"));
}

export function getBreakColor(): RgbColor {
  try {
    const raw = localStorage.getItem(BREAK_KEY);
    if (raw) return JSON.parse(raw) as RgbColor;
  } catch { /* ignore */ }
  return { ...DEFAULT_BREAK_COLOR };
}

export function setBreakColor(color: RgbColor): void {
  localStorage.setItem(BREAK_KEY, JSON.stringify(color));
  window.dispatchEvent(new Event("teapot-colors-changed"));
}

export function rgbToCss(c: RgbColor, alpha = 1): string {
  return `rgba(${c.r}, ${c.g}, ${c.b}, ${alpha})`;
}

export function isDarkColor(c: RgbColor): boolean {
  const luminance = 0.2126 * c.r + 0.7152 * c.g + 0.0722 * c.b;
  return luminance < 88;
}

export function readableTextColor(c: RgbColor): string {
  return isDarkColor(c) ? "#f8fafc" : "#0f172a";
}

export function rgbToHex(c: RgbColor): string {
  return (
    "#" +
    [c.r, c.g, c.b]
      .map((v) => Math.max(0, Math.min(255, v)).toString(16).padStart(2, "0"))
      .join("")
  );
}

export function hexToRgb(hex: string): RgbColor {
  const clean = hex.replace("#", "");
  const full = clean.length === 3
    ? clean.split("").map((c) => c + c).join("")
    : clean;
  return {
    r: parseInt(full.slice(0, 2), 16) || 0,
    g: parseInt(full.slice(2, 4), 16) || 0,
    b: parseInt(full.slice(4, 6), 16) || 0,
  };
}
