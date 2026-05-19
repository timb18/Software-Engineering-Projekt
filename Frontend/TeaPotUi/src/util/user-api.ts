const API_BASE = import.meta.env.VITE_API_BASE_URL ?? "";

/**
 * User profile information returned from backend.
 *
 * Fields:
 * - id: Unique user identifier (UUID)
 * - username: Unique username (immutable after creation)
 * - displayName: User's display name for UI (editable)
 * - email: User's email address (used for authentication and invitations)
 * - profileImageUrl: Optional avatar URL
 * - timezone: IANA timezone string (e.g., "Europe/Berlin", "America/New_York")
 *   Used for scheduling and calendar display
 * - breakColor: Serialized color preference for break events (JSON string or null)
 * - orgColors: Serialized color preferences for organizations (JSON string or null)
 */
export type UserProfile = {
  id: string;
  username: string;
  displayName: string;
  email: string;
  profileImageUrl?: string;
  timezone: string;
  // These are serialized as JSON strings for storage; parsed by color-prefs utilities
  breakColor?: string | null;
  orgColors?: string | null;
};

/**
 * Payload for ensuring a user exists in the system (create or get).
 *
 * Used during OAuth authentication flow:
 * 1. Auth0 provides user info (email, displayName, picture URL)
 * 2. Frontend calls ensureUser() with this payload
 * 3. Backend creates user if not found, or returns existing user
 *
 * Fields:
 * - email: User's email (required, lowercased by backend)
 * - authProviderSubject: OAuth provider's unique ID (e.g., Auth0 sub)
 * - displayName: User's display name from OAuth provider
 * - profileImageUrl: Avatar URL from OAuth provider
 */
export type EnsureUserPayload = {
  email: string;
  authProviderSubject?: string;
  displayName?: string;
  profileImageUrl?: string;
};

/**
 * Response from ensureUser() call.
 *
 * Fields:
 * - userId: The user's ID (newly created or existing)
 * - workProfileId: The user's default/personal work profile ID (or null if not set)
 */
export type EnsureUserResponse = {
  userId: string;
  workProfileId: string | null;
};

/**
 * Ensures a user exists in the system (create if not found, return if exists).
 *
 * This is the FIRST step in authentication flow:
 * 1. Auth0 authenticates the user and returns token
 * 2. Frontend extracts user info from Auth0 token claims
 * 3. Frontend calls ensureUser() to sync user with backend
 * 4. Backend creates or fetches user, links OAuth provider
 * 5. Frontend stores userId and workProfileId for later API calls
 *
 * Flow:
 * - POST to /api/auth/ensure with EnsureUserPayload
 * - Backend returns userId (newly created or existing) and workProfileId
 * - No error if user already exists; just returns existing user
 *
 * @param payload - EnsureUserPayload with email and optional OAuth details
 * @param token - accesstoken for the api
 * @returns Promise resolving to EnsureUserResponse with userId and workProfileId
 * @throws Error if request fails (network, server error)
 *
 * @remarks
 * Safe to call multiple times with same email—backend ensures idempotency.
 */
export async function ensureUser(
  payload: EnsureUserPayload,
  token: string,
): Promise<EnsureUserResponse> {
  const res = await fetch(`${API_BASE}/api/auth/ensure`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${token}`,
    },
    body: JSON.stringify(payload),
  });

  if (!res.ok) {
    throw new Error(`ensureUser failed: ${res.status} ${res.statusText}`);
  }

  return res.json() as Promise<EnsureUserResponse>;
}

/**
 * Fetches the complete user profile by ID.
 *
 * Includes all user settings:
 * - Display name, email, timezone
 * - Color preferences (for breaks and organizations)
 * - Profile image URL
 *
 * Used after authentication to load user settings into Zustand store.
 *
 * @param userId - UUID of user to fetch
 * @param token - accesstoken for the api
 * @returns Promise resolving to UserProfile with all user settings
 * @throws Error if user not found or fetch fails
 */
export async function fetchUserProfile(
  userId: string,
  token: string,
): Promise<UserProfile> {
  const res = await fetch(
    `${API_BASE}/api/user/${encodeURIComponent(userId)}/profile`,
    { headers: { Authorization: `Bearer ${token}` } },
  );

  if (!res.ok) {
    throw new Error(`fetchUserProfile failed: ${res.status} ${res.statusText}`);
  }

  return res.json() as Promise<UserProfile>;
}

/**
 * Updates the user's profile settings.
 *
 * Allows updating:
 * - displayName: Public name shown in UI and to other users
 * - email: Primary email address (may require verification)
 * - timezone: IANA timezone for scheduling and display
 * - profileImageUrl: Avatar URL
 * - breakColor: Serialized color preference for break events
 * - orgColors: Serialized color preferences for organizations
 *
 * NOTE: id and username are immutable after user creation and cannot be updated.
 *
 * Flow:
 * 1. Call updateUserProfile() with modified UserProfile (minus id/username)
 * 2. Backend validates timezone (must be IANA format)
 * 3. Backend validates email if changed (may require Auth0 sync)
 * 4. Returns updated UserProfile with server-computed fields
 *
 * @param userId - UUID of user to update
 * @param profile - UserProfile excluding id and username (both immutable)
 * @param token - accesstoken for the api
 * @returns Promise resolving to updated UserProfile from backend
 * @throws Error with backend message if update fails (validation, auth, etc.)
 *
 * @remarks
 * Timezone validation:
 * - Must be a valid IANA timezone string (e.g., "Europe/Berlin", "UTC")
 * - Invalid timezone will result in 400 Bad Request error
 * - Used for calendar display and task scheduling
 *
 * Email updates:
 * - If email differs from authenticated user, may require verification
 * - Backend may sync change with Auth0 provider
 */
export async function updateUserProfile(
  userId: string,
  profile: Omit<UserProfile, "id" | "username">,
  token: string,
): Promise<UserProfile> {
  const res = await fetch(
    `${API_BASE}/api/user/${encodeURIComponent(userId)}/profile`,
    {
      method: "PUT",
      headers: {
        "Content-Type": "application/json",
        Authorization: `Bearer ${token}`,
      },
      body: JSON.stringify(profile),
    },
  );

  if (!res.ok) {
    const text = await res.text().catch(() => res.statusText);
    throw new Error(text || `updateUserProfile failed: ${res.status}`);
  }

  return res.json() as Promise<UserProfile>;
}
