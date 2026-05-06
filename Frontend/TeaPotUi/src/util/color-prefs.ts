export type RgbColor = { r: number; g: number; b: number };

export const DEFAULT_ORG_COLOR: RgbColor = { r: 16, g: 185, b: 129 };   // emerald
export const DEFAULT_BREAK_COLOR: RgbColor = { r: 245, g: 158, b: 11 }; // amber

const orgKey = (orgId: string) => `teapot-color-org-${orgId}`;
const BREAK_KEY = "teapot-color-breaks";

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
