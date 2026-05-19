/**
 * RGB color representation used throughout the application.
 * 
 * Values:
 * - r, g, b: 0-255 intensity values
 * 
 * Used for:
 * - Organization event colors on calendar
 * - Break event colors on calendar
 * - User color preferences stored in localStorage and database
 */
export type RgbColor = { r: number; g: number; b: number };

/**
 * Default organization color (emerald-500 in Tailwind).
 * Used when user hasn't set a custom color for an organization.
 */
export const DEFAULT_ORG_COLOR: RgbColor = { r: 16, g: 185, b: 129 };   // emerald

/**
 * Default break event color (amber-400 in Tailwind).
 * Used when user hasn't set a custom color for breaks.
 */
export const DEFAULT_BREAK_COLOR: RgbColor = { r: 245, g: 158, b: 11 }; // amber
export const DEFAULT_BLOCKER_COLOR: RgbColor = { r: 139, g: 92, b: 246 }; // violet

/**
 * Generates localStorage key for organization color preference.
 * 
 * Format: "teapot-color-org-{orgId}"
 * 
 * @param orgId - Organization ID
 * @returns localStorage key for this organization's color
 */
const orgKey = (orgId: string) => `teapot-color-org-${orgId}`;

/**
 * Fixed localStorage key for break event color.
 * 
 * There's only one break color setting (global across all work profiles).
 */
const BREAK_KEY = "teapot-color-breaks";
const BLOCKER_KEY = "teapot-color-blockers";

/**
 * Type guard: Validates if an unknown value is a valid RgbColor.
 * 
 * Checks for presence and correct types of r, g, b properties.
 * 
 * @param value - Value to validate
 * @returns true if value is a valid RgbColor
 */
const isRgbColor = (value: unknown): value is RgbColor => {
  const color = value as Partial<RgbColor> | undefined;
  return (
    typeof color?.r === "number" &&
    typeof color.g === "number" &&
    typeof color.b === "number"
  );
};

/**
 * Parses a color preference from JSON string (or returns fallback).
 * 
 * Used to load color from:
 * - localStorage (as JSON string)
 * - User profile from API (serialized as JSON)
 * - User input (JSON color picker output)
 * 
 * Robust parsing:
 * - Returns fallback on parse error
 * - Returns fallback if parsed value is not valid RgbColor
 * - Never throws; always returns valid RgbColor
 * 
 * @param raw - JSON string containing color, or null/undefined
 * @param fallback - Default color if parsing fails
 * @returns Parsed RgbColor or fallback
 */
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

/**
 * Parses organization color preferences from JSON string.
 * 
 * Expects format: { "orgId1": { r, g, b }, "orgId2": { r, g, b }, ... }
 * 
 * Robust parsing:
 * - Skips invalid colors (non-object, invalid RgbColor)
 * - Returns empty object on parse error or null input
 * - Never throws; always returns valid Record<string, RgbColor>
 * 
 * @param raw - JSON string containing org→color map, or null/undefined
 * @returns Record mapping orgIds to RgbColor (empty if invalid)
 */
export function parseOrgColorPreferences(
  raw: string | null | undefined,
): Record<string, RgbColor> {
  if (!raw) return {};

  try {
    const parsed = JSON.parse(raw);
    // Must be object (not array, not null)
    if (!parsed || typeof parsed !== "object" || Array.isArray(parsed)) return {};

    // Filter to only valid RgbColor entries
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

/**
 * Serializes a single color preference to JSON string.
 * 
 * Used when storing color in:
 * - localStorage (as string)
 * - User profile (sent to API)
 * 
 * @param color - RgbColor to serialize
 * @returns JSON string representation
 */
export function serializeColorPreference(color: RgbColor): string {
  return JSON.stringify(color);
}

/**
 * Serializes organization color preferences to JSON string.
 * 
 * Reverses parseOrgColorPreferences().
 * 
 * @param colors - Record mapping orgIds to RgbColor
 * @returns JSON string representation
 */
export function serializeOrgColorPreferences(
  colors: Record<string, RgbColor>,
): string {
  return JSON.stringify(colors);
}

/**
 * Applies stored color preferences to localStorage (side effect).
 * 
 * Loads color settings from user profile (as JSON strings) and
 * persists them in localStorage for fast access during session.
 * 
 * Called during user initialization to sync database colors → localStorage.
 * 
 * @param breakColor - Serialized break color from user profile
 * @param orgColors - Serialized org colors from user profile
 */
export function applyStoredColorPreferences(
  breakColor: string | null | undefined,
  blockerColor: string | null | undefined,
  orgColors: string | null | undefined,
): void {
  setBreakColor(parseColorPreference(breakColor, DEFAULT_BREAK_COLOR));
  setBlockerColor(parseColorPreference(blockerColor, DEFAULT_BLOCKER_COLOR));
  for (const [orgId, color] of Object.entries(parseOrgColorPreferences(orgColors))) {
    setOrgColor(orgId, color);
  }
}

/**
 * Gets organization color from localStorage (or default).
 * 
 * Looks up color in localStorage by key, with fallback to DEFAULT_ORG_COLOR.
 * 
 * Safe:
 * - Catches localStorage errors (e.g., in private browsing)
 * - Never throws
 * - Always returns valid RgbColor
 * 
 * @param orgId - Organization ID to look up color for
 * @returns RgbColor (stored or default)
 */
export function getOrgColor(orgId: string): RgbColor {
  try {
    const raw = localStorage.getItem(orgKey(orgId));
    if (raw) return JSON.parse(raw) as RgbColor;
  } catch { /* ignore localStorage errors */ }
  return { ...DEFAULT_ORG_COLOR };
}

/**
 * Sets organization color in localStorage and notifies listeners.
 * 
 * Side effects:
 * 1. Saves color to localStorage
 * 2. Dispatches "teapot-colors-changed" event for reactive updates
 *    (components listen to this to re-render when colors change)
 * 
 * @param orgId - Organization ID
 * @param color - RgbColor to store
 */
export function setOrgColor(orgId: string, color: RgbColor): void {
  localStorage.setItem(orgKey(orgId), JSON.stringify(color));
  // Notify all listeners (components, other tabs) that colors changed
  window.dispatchEvent(new Event("teapot-colors-changed"));
}

/**
 * Gets break event color from localStorage (or default).
 * 
 * There's only one break color setting (applied to all breaks globally).
 * 
 * Safe:
 * - Catches localStorage errors
 * - Never throws
 * - Always returns valid RgbColor
 * 
 * @returns RgbColor for break events (stored or default)
 */
export function getBreakColor(): RgbColor {
  try {
    const raw = localStorage.getItem(BREAK_KEY);
    if (raw) return JSON.parse(raw) as RgbColor;
  } catch { /* ignore localStorage errors */ }
  return { ...DEFAULT_BREAK_COLOR };
}

/**
 * Sets break event color in localStorage and notifies listeners.
 * 
 * Side effects:
 * 1. Saves color to localStorage
 * 2. Dispatches "teapot-colors-changed" event for reactive updates
 * 
 * @param color - RgbColor for breaks
 */
export function setBreakColor(color: RgbColor): void {
  localStorage.setItem(BREAK_KEY, JSON.stringify(color));
  // Notify all listeners that colors changed
  window.dispatchEvent(new Event("teapot-colors-changed"));
}

export function getBlockerColor(): RgbColor {
  try {
    const raw = localStorage.getItem(BLOCKER_KEY);
    if (raw) return JSON.parse(raw) as RgbColor;
  } catch { /* ignore */ }
  return { ...DEFAULT_BLOCKER_COLOR };
}

export function setBlockerColor(color: RgbColor): void {
  localStorage.setItem(BLOCKER_KEY, JSON.stringify(color));
  window.dispatchEvent(new Event("teapot-colors-changed"));
}
/**
 * Converts RGB color to CSS rgba() string with optional opacity.
 * 
 * Format: "rgba(r, g, b, alpha)"
 * 
 * Used for:
 * - Calendar event styling (background colors)
 * - Theme colors in components
 * - CSS-in-JS color values
 * 
 * @param c - RgbColor to convert
 * @param alpha - Optional opacity (0-1, default 1)
 * @returns CSS rgba string
 * 
 * @example
 * rgbToCss({ r: 16, g: 185, b: 129 }, 0.8) // "rgba(16, 185, 129, 0.8)"
 */
export function rgbToCss(c: RgbColor, alpha = 1): string {
  return `rgba(${c.r}, ${c.g}, ${c.b}, ${alpha})`;
}

/**
 * Determines if a color should be considered "dark" (requires light text overlay).
 * 
 * Algorithm: Luminance calculation using ITU-R BT.601 luma formula.
 * Formula: L = 0.2126*R + 0.7152*G + 0.0722*B
 * 
 * Threshold: Luminance < 88 is considered dark.
 * This ensures sufficient contrast with white text (#f8fafc).
 * 
 * @param c - RgbColor to analyze
 * @returns true if color is dark (needs light text), false if light
 */
export function isDarkColor(c: RgbColor): boolean {
  // ITU-R BT.601 luminance formula (weights human perception)
  const luminance = 0.2126 * c.r + 0.7152 * c.g + 0.0722 * c.b;
  return luminance < 88;
}

/**
 * Selects contrast-appropriate text color based on background color.
 * 
 * Applies WCAG contrast guidelines:
 * - Dark background (#f8fafc = light gray, for dark colors)
 * - Light background (#0f172a = dark slate, for light colors)
 * 
 * Used for calendar event text, block labels, etc.
 * Ensures 4.5:1 minimum contrast ratio for readability.
 * 
 * @param c - Background RgbColor
 * @returns Text color as hex string (#f8fafc or #0f172a)
 */
export function readableTextColor(c: RgbColor): string {
  return isDarkColor(c) ? "#f8fafc" : "#0f172a";
}

/**
 * Converts RGB color to hexadecimal string.
 * 
 * Format: "#RRGGBB" (6 hex digits)
 * 
 * Used for:
 * - Color picker input values
 * - URL parameters
 * - HTML color attributes
 * 
 * Clamping:
 * - RGB values are clamped to 0-255 range before conversion
 * - Handles out-of-range inputs gracefully
 * 
 * @param c - RgbColor to convert
 * @returns Hex string (e.g., "#10b981")
 * 
 * @example
 * rgbToHex({ r: 16, g: 185, b: 129 }) // "#10b981"
 */
export function rgbToHex(c: RgbColor): string {
  return (
    "#" +
    [c.r, c.g, c.b]
      .map((v) => Math.max(0, Math.min(255, v)).toString(16).padStart(2, "0"))
      .join("")
  );
}

/**
 * Converts hexadecimal color string to RGB.
 * 
 * Handles both formats:
 * - 6-digit: "#RRGGBB" (e.g., "#10b981")
 * - 3-digit: "#RGB" → expanded to "#RRGGBB" (e.g., "#0f0" → "#00ff00")
 * 
 * Robust parsing:
 * - Strips "#" prefix if present
 * - Defaults to 0 for unparseable components
 * - Never throws
 * 
 * @param hex - Hex color string (with or without "#")
 * @returns RgbColor with r, g, b values (0-255)
 * 
 * @example
 * hexToRgb("#10b981") // { r: 16, g: 185, b: 129 }
 * hexToRgb("#f0f") // { r: 255, g: 0, b: 255 }
 */
export function hexToRgb(hex: string): RgbColor {
  // Remove "#" prefix if present
  const clean = hex.replace("#", "");
  
  // Expand 3-digit hex (#RGB) to 6-digit (#RRGGBB)
  const full = clean.length === 3
    ? clean.split("").map((c) => c + c).join("")
    : clean;
  
  // Parse 2-character hex segments, default to 0 on error
  return {
    r: parseInt(full.slice(0, 2), 16) || 0,
    g: parseInt(full.slice(2, 4), 16) || 0,
    b: parseInt(full.slice(4, 6), 16) || 0,
  };
}
