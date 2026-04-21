import type { WorkProfile } from "./types";

// The backend base URL is set via the VITE_API_BASE_URL env variable in production,
// or proxied locally through Vite's dev server (see vite.config.ts).
const API_BASE = import.meta.env.VITE_API_BASE_URL ?? "";

/**
 * Fetches the saved work profile for a user from the backend.
 * Returns null if no profile has been saved yet (204 No Content).
 */
export async function fetchWorkProfile(userId: string): Promise<WorkProfile | null> {
  const res = await fetch(`${API_BASE}/api/workprofile/${encodeURIComponent(userId)}`);

  if (res.status === 204) return null;
  if (!res.ok) throw new Error(`Failed to fetch work profile: ${res.status} ${res.statusText}`);

  return res.json() as Promise<WorkProfile>;
}

/**
 * Saves (creates or replaces) the work profile for a user on the backend.
 * Returns the saved profile as confirmed by the server.
 */
export async function saveWorkProfile(userId: string, profile: WorkProfile): Promise<WorkProfile> {
  const res = await fetch(`${API_BASE}/api/workprofile/${encodeURIComponent(userId)}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ ...profile, userId }),
  });

  if (!res.ok) {
    const text = await res.text().catch(() => res.statusText);
    throw new Error(`Failed to save work profile: ${text}`);
  }

  return res.json() as Promise<WorkProfile>;
}
