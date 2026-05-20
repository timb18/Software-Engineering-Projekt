import { afterEach, describe, expect, it, vi } from "vitest";
import { ensureUser, fetchUserProfile, updateUserProfile } from "./user-api";

const USER_ID = "11111111-1111-1111-1111-111111111111";
const API_BASE = import.meta.env.VITE_API_BASE_URL ?? "";

const profile = {
  id: USER_ID,
  username: "anna",
  displayName: "Anna Example",
  email: "anna@example.com",
  profileImageUrl: "https://example.com/avatar.png",
  timezone: "Europe/Berlin",
};

const mockFetch = (data: unknown, ok = true, status = 200) =>
  vi.fn().mockResolvedValue({
    ok,
    status,
    statusText: ok ? "OK" : "Bad Request",
    json: () => Promise.resolve(data),
    text: () => Promise.resolve(typeof data === "string" ? data : JSON.stringify(data)),
  });

afterEach(() => {
  vi.restoreAllMocks();
});

describe("ensureUser", () => {
  it("POSTs the auth-aware payload to the backend", async () => {
    globalThis.fetch = mockFetch({ userId: USER_ID, workProfileId: "wp-1" });

    await ensureUser({
      email: "anna@example.com",
      authProviderSubject: "auth0|123",
      displayName: "Anna Example",
      profileImageUrl: "https://example.com/avatar.png",
    }, "test-token");

    const [url, init] = vi.mocked(globalThis.fetch).mock.calls[0];
    expect(url).toBe(`${API_BASE}/api/auth/ensure`);
    expect(init?.method).toBe("POST");
    expect(JSON.parse(init?.body as string)).toEqual({
      email: "anna@example.com",
      authProviderSubject: "auth0|123",
      displayName: "Anna Example",
      profileImageUrl: "https://example.com/avatar.png",
    });
  });
});

describe("fetchUserProfile", () => {
  it("loads the profile by backend user id", async () => {
    globalThis.fetch = mockFetch(profile);

    const result = await fetchUserProfile(USER_ID, "test-token");

    expect(vi.mocked(globalThis.fetch).mock.calls[0][0]).toBe(`${API_BASE}/api/user/${USER_ID}/profile`);
    expect(result).toEqual(profile);
  });
});

describe("updateUserProfile", () => {
  it("PUTs the editable fields and returns the saved profile", async () => {
    globalThis.fetch = mockFetch(profile);

    const result = await updateUserProfile(USER_ID, {
      displayName: "Anna Example",
      email: "anna@example.com",
      profileImageUrl: "https://example.com/avatar.png",
      timezone: "Europe/Berlin",
    }, "test-token");

    const [url, init] = vi.mocked(globalThis.fetch).mock.calls[0];
    expect(url).toBe(`${API_BASE}/api/user/${USER_ID}/profile`);
    expect(init?.method).toBe("PUT");
    expect(JSON.parse(init?.body as string)).toEqual({
      displayName: "Anna Example",
      email: "anna@example.com",
      profileImageUrl: "https://example.com/avatar.png",
      timezone: "Europe/Berlin",
    });
    expect(result).toEqual(profile);
  });

  it("throws the backend validation message on failure", async () => {
    globalThis.fetch = mockFetch("Email format is invalid.", false, 400);

    await expect(
      updateUserProfile(USER_ID, {
        displayName: "Anna Example",
        email: "not-an-email",
        profileImageUrl: undefined,
        timezone: "Europe/Berlin",
      }, "test-token"),
    ).rejects.toThrow("Email format is invalid.");
  });
});
