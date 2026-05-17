const API_BASE = import.meta.env.VITE_API_BASE_URL ?? "";

export type UserProfile = {
  id: string;
  username: string;
  displayName: string;
  email: string;
  profileImageUrl?: string;
  timezone: string;
  breakColor?: string | null;
  orgColors?: string | null;
};

export type EnsureUserPayload = {
  email: string;
  authProviderSubject?: string;
  displayName?: string;
  profileImageUrl?: string;
};

export type EnsureUserResponse = {
  userId: string;
  workProfileId: string | null;
};

export async function ensureUser(payload: EnsureUserPayload): Promise<EnsureUserResponse> {
  const res = await fetch(`${API_BASE}/api/auth/ensure`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload),
  });

  if (!res.ok) {
    throw new Error(`ensureUser failed: ${res.status} ${res.statusText}`);
  }

  return res.json() as Promise<EnsureUserResponse>;
}

export async function fetchUserProfile(userId: string): Promise<UserProfile> {
  const res = await fetch(`${API_BASE}/api/user/${encodeURIComponent(userId)}/profile`);

  if (!res.ok) {
    throw new Error(`fetchUserProfile failed: ${res.status} ${res.statusText}`);
  }

  return res.json() as Promise<UserProfile>;
}

export async function updateUserProfile(
  userId: string,
  profile: Omit<UserProfile, "id" | "username">,
): Promise<UserProfile> {
  const res = await fetch(`${API_BASE}/api/user/${encodeURIComponent(userId)}/profile`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(profile),
  });

  if (!res.ok) {
    const text = await res.text().catch(() => res.statusText);
    throw new Error(text || `updateUserProfile failed: ${res.status}`);
  }

  return res.json() as Promise<UserProfile>;
}
