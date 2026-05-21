import { afterEach, describe, expect, it, vi } from "vitest";
import { deleteOrganization, fetchOrganizationsByUserEmail } from "./org-api";

const mockFetch = (data: unknown, ok = true, status = 200) =>
  vi.fn().mockResolvedValue({
    ok,
    status,
    statusText: ok ? "OK" : "Server Error",
    json: () => Promise.resolve(data),
  });

describe("fetchOrganizationsByUserEmail", () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("maps backend organizations to frontend orgs with organizer roles and invites", async () => {
    globalThis.fetch = mockFetch([
      {
        id: "org-1",
        name: "Northwind Labs",
        workProfileId: "work-profile-1",
        users: [
          {
            id: "user-1",
            email: "owner@example.com",
            username: "owner",
            role: "organizer",
          },
        ],
        invites: [
          {
            id: "invite-1",
            organizationId: "org-1",
            email: "new@example.com",
            status: "open",
            invitationLink: "https://example.com/invite",
          },
        ],
      },
    ]);

    const result = await fetchOrganizationsByUserEmail("owner@example.com", "test-token");

    expect(result).toEqual([
      {
        id: "org-1",
        name: "Northwind Labs",
        workProfileId: "work-profile-1",
        users: [
          {
            id: "user-1",
            email: "owner@example.com",
            username: "owner",
            displayName: "owner",
            role: "admin",
            orgs: [],
            tasks: [],
            invites: [],
          },
        ],
        adminEmails: ["owner@example.com"],
        invites: [
          {
            id: "invite-1",
            organizationId: "org-1",
            orgId: "org-1",
            orgName: "Northwind Labs",
            email: "new@example.com",
            firstName: undefined,
            lastName: undefined,
            status: "pending",
            invitationUrl: "https://example.com/invite",
          },
        ],
      },
    ]);
  });
});

describe("deleteOrganization", () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("sends initiator, confirmation text, and auth token", async () => {
    globalThis.fetch = vi.fn().mockResolvedValue({
      ok: true,
      status: 204,
      statusText: "No Content",
      text: () => Promise.resolve(""),
    });

    await deleteOrganization(
      {
        initiatorUserId: "user-1",
        organizationId: "org-1",
        confirmationText: "Northwind Labs",
      },
      "test-token",
    );

    expect(globalThis.fetch).toHaveBeenCalledWith(
      expect.stringContaining("/api/Organization/org-1"),
      {
        method: "DELETE",
        headers: {
          "Content-Type": "application/json",
          Authorization: "Bearer test-token",
        },
        body: JSON.stringify({
          initiatorUserId: "user-1",
          confirmationText: "Northwind Labs",
        }),
      },
    );
  });
});
