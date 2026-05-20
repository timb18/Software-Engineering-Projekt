import type { WorkProfile } from "./types";

/**
 * API Base URL for work profile endpoints.
 * 
 * Set via VITE_API_BASE_URL environment variable in production.
 * In development, requests are proxied through Vite's dev server
 * (configured in vite.config.ts).
 * 
 * If not set, defaults to empty string (same-origin requests).
 */
const API_BASE = import.meta.env.VITE_API_BASE_URL ?? "";

/**
 * Fetches the saved work profile for a user from the backend.
 * 
 * Each user has ONE saved work profile (their main schedule).
 * The frontend may have local unsaved edits, but this fetches the
 * server's authoritative copy.
 * 
 * Response handling:
 * - 200 OK: Returns the work profile as JSON
 * - 204 No Content: User has no saved profile yet (returns null)
 * - Other errors: Throws with HTTP status
 * 
 * Used during:
 * - User initialization (load their work profile)
 * - Refresh/refetch operations
 * - Conflict resolution (server copy when discarding local changes)
 * 
 * @param userId - UUID of user to fetch profile for
 * @returns Promise resolving to WorkProfile, or null if none saved
 * @throws Error if fetch fails (excluding 204)
 */
export async function fetchWorkProfile(
  userId: string,
  _organizationId?: string | null,
): Promise<WorkProfile | null> {
  const url = `${API_BASE}/api/workprofile/${encodeURIComponent(userId)}`;
  let res: Response;
  try {
    res = await fetch(url);
  } catch (error) {
    throw new Error(
      `Backend not reachable while loading the work profile. Make sure the API is running at ${API_BASE || "the Vite proxy target (http://localhost:5186)"}.`,
      { cause: error },
    );
  }

  if (res.status === 204) return null; // No profile saved yet
  if (!res.ok) throw new Error(`Failed to fetch work profile: ${res.status} ${res.statusText}`);

  return res.json() as Promise<WorkProfile>;
}

/**
 * Saves (creates or replaces) the work profile for a user on the backend.
 * 
 * This is a PUT operation (idempotent): if profile exists, it's replaced;
 * if not, it's created.
 * 
 * The entire profile is replaced—this is NOT a merge/patch operation.
 * The frontend is responsible for maintaining unsaved edits locally
 * and sending the complete profile when saving.
 * 
 * Flow:
 * 1. Frontend has unsaved WorkProfile in local state
 * 2. User clicks "Save"
 * 3. Frontend calls saveWorkProfile() with complete profile
 * 4. Backend validates and replaces entire profile
 * 5. Returns saved profile (may have server-computed fields)
 * 6. Frontend updates saved state and clears dirty flag
 * 
 * Error handling:
 * - On failure, extracts error message from response text
 * - Throws descriptive error (no side effects on error)
 * 
 * @param userId - UUID of user
 * @param profile - Complete WorkProfile to save (all 7 days, all blocks/breaks)
 * @returns Promise resolving to saved WorkProfile from server
 * @throws Error with message if save fails
 */
export async function saveWorkProfile(
  userId: string,
  profile: WorkProfile,
  _organizationId?: string | null,
): Promise<WorkProfile> {
  const url = `${API_BASE}/api/workprofile/${encodeURIComponent(userId)}`;
  let res: Response;
  try {
    res = await fetch(url, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(profile),
    });
  } catch (error) {
    throw new Error(
      `Backend not reachable while saving the work profile. Make sure the API is running at ${API_BASE || "the Vite proxy target (http://localhost:5186)"}.`,
      { cause: error },
    );
  }

  if (!res.ok) {
    const text = await res.text().catch(() => res.statusText);
    throw new Error(`Failed to save work profile: ${text}`);
  }

  return res.json() as Promise<WorkProfile>;
}
