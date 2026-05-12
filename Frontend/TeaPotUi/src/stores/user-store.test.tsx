import { renderHook, act, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import useUserStore, { initForUser } from "./user-store";
import { defaultUser } from "../util/default-data";
import type { Task, User } from "../util/types";

vi.mock("../util/user-api", () => ({
  ensureUser: vi.fn(),
  fetchUserProfile: vi.fn(),
}));

vi.mock("../util/task-api", () => ({
  fetchTasks: vi.fn(),
  createTask: vi.fn(),
  updateTask: vi.fn(),
  deleteTask: vi.fn(),
}));

vi.mock("../util/work-profile-api", () => ({
  fetchWorkProfile: vi.fn(),
}));

vi.mock("../util/org-api", () => ({
  fetchOrganizationsByUserEmail: vi.fn(),
}));

import { ensureUser, fetchUserProfile } from "../util/user-api";
import { fetchTasks } from "../util/task-api";
import { fetchWorkProfile } from "../util/work-profile-api";
import { fetchOrganizationsByUserEmail } from "../util/org-api";

describe("user-store initForUser", () => {
  afterEach(() => {
    vi.clearAllMocks();
    const { result } = renderHook(() => useUserStore());
    act(() => {
      result.current.setUser(defaultUser);
    });
  });

  it("loads the saved work profile from the backend using the backend user id", async () => {
    const backendUserId = "11111111-2222-3333-4444-555555555555";
    const backendWorkProfileId = "99999999-8888-7777-6666-555555555555";
    const savedWorkProfile = {
      plannerViewStart: "07:00",
      plannerViewEnd: "21:00",
      maxDailyLoad: "03:00:00",
      days: [
        {
          day: "Mon" as const,
          blocks: [
            {
              id: "block-1",
              companyId: "org-1",
              companyName: "Northwind Labs",
              startTime: "09:00",
              endTime: "12:00",
            },
          ],
          breaks: [],
        },
      ],
    };
    const organizations = [
      {
        id: "org-1",
        name: "Northwind Labs",
        users: [],
        adminEmails: ["test@example.com"],
        invites: [],
      },
    ];

    vi.mocked(ensureUser).mockResolvedValue({
      userId: backendUserId,
      workProfileId: backendWorkProfileId,
    });
    vi.mocked(fetchUserProfile).mockResolvedValue({
      id: backendUserId,
      username: "test",
      displayName: "Test User",
      email: "test@example.com",
      profileImageUrl: undefined,
      timezone: "Europe/Berlin",
    });
    vi.mocked(fetchTasks).mockResolvedValue([]);
    vi.mocked(fetchWorkProfile).mockResolvedValue(savedWorkProfile);
    vi.mocked(fetchOrganizationsByUserEmail).mockResolvedValue(organizations);

    const { result } = renderHook(() => useUserStore());

    await act(async () => {
      await initForUser("auth0|abc123", "test@example.com");
    });

    expect(vi.mocked(fetchWorkProfile)).toHaveBeenCalledWith(backendUserId);
    expect(vi.mocked(fetchOrganizationsByUserEmail)).toHaveBeenCalledWith(
      "test@example.com",
    );

    await waitFor(() => {
      expect(result.current.user.id).toBe(backendUserId);
      expect(result.current.workProfileId).toBe(backendWorkProfileId);
      expect(result.current.user.workProfile).toEqual(savedWorkProfile);
      expect(result.current.user.plannerViewStart).toBe("07:00");
      expect(result.current.user.plannerViewEnd).toBe("21:00");
      expect(result.current.user.workCapacityHours).toBe(3);
      expect(result.current.user.workStart).toBe("09:00");
      expect(result.current.user.workEnd).toBe("12:00");
      expect(result.current.user.orgs).toEqual(organizations);
    });
  });

  it("setActiveOrganization updates activeOrganizationId and planner info", async () => {
    const { result } = renderHook(() => useUserStore());
    vi.mocked(fetchTasks).mockResolvedValue([]);

    const orgA = {
      id: "org-a",
      name: "A",
      workProfileId: "wp-a",
      users: [],
      invites: [],
    };
    const orgB = {
      id: "org-b",
      name: "B",
      workProfileId: "wp-b",
      users: [],
      invites: [],
    };
    const user: User = {
      id: "u1",
      email: "u@x.test",
      orgs: [orgA, orgB],
      tasks: [],
      invites: [],
    };

    act(() => result.current.setUser(user));

    await act(async () => {
      await result.current.setActiveOrganization("org-b");
    });

    expect(result.current.activeOrganizationId).toBe("org-b");
    expect(result.current.workProfileId).toBe("wp-b");
  });

  it("does not reload data when the selected organization is already active", async () => {
    const { result } = renderHook(() => useUserStore());

    const orgB = {
      id: "org-b",
      name: "B",
      workProfileId: "wp-b",
      users: [],
      invites: [],
    };

    const user: User = {
      id: "u1",
      email: "u@x.test",
      orgs: [orgB],
      tasks: [],
      invites: [],
    };

    act(() => {
      result.current.setUser(user);
    });

    await act(async () => {
      await result.current.setActiveOrganization("org-b");
    });

    vi.clearAllMocks();

    await act(async () => {
      await result.current.setActiveOrganization("org-b");
    });

    expect(fetchTasks).not.toHaveBeenCalled();
  });

  it("loads tasks when switching to a new organization", async () => {
    const { result } = renderHook(() => useUserStore());

    const orgA = {
      id: "org-a",
      name: "A",
      workProfileId: "wp-a",
      users: [],
      invites: [],
    };

    const orgB = {
      id: "org-b",
      name: "B",
      workProfileId: "wp-b",
      users: [],
      invites: [],
    };

    const user: User = {
      id: "u1",
      email: "u@x.test",
      orgs: [orgA, orgB],
      tasks: [],
      invites: [],
    };

    const loadedTasks = [
      {
        id: "t1",
        name: "Task 1",
        description: "",
        startDate: new Date(),
        endDate: new Date(),
        org: "org-b",
        dependencies: [],
      },
    ] satisfies Task[];

    vi.mocked(fetchTasks).mockResolvedValue(loadedTasks);

    act(() => {
      result.current.setUser(user);
    });

    await act(async () => {
      await result.current.setActiveOrganization("org-b");
    });

    expect(fetchTasks).toHaveBeenCalledWith("wp-b");
    expect(result.current.activeOrganizationId).toBe("org-b");
    expect(result.current.workProfileId).toBe("wp-b");
    expect(result.current.user.tasks).toEqual(loadedTasks);
  });

  it("does not switch when selected organization is missing", async () => {
    const { result } = renderHook(() => useUserStore());

    const orgA = {
      id: "org-a",
      name: "A",
      workProfileId: "wp-a",
      users: [],
      invites: [],
    };

    const orgB = {
      id: "org-b",
      name: "B",
      workProfileId: "wp-b",
      users: [],
      invites: [],
    };

    const user: User = {
      id: "u1",
      email: "u@x.test",
      orgs: [orgA, orgB],
      tasks: [],
      invites: [],
    };

    vi.mocked(fetchTasks).mockResolvedValue([]);

    act(() => {
      result.current.setUser(user);
    });

    await act(async () => {
      await result.current.setActiveOrganization("missing-org");
    });

    expect(fetchTasks).not.toHaveBeenCalled();
    expect(result.current.activeOrganizationId).toBe("org-a");
    expect(result.current.workProfileId).toBe("wp-a");
  });

  it("rejects when loading tasks for new organization fails", async () => {
    const { result } = renderHook(() => useUserStore());

    const orgA = {
      id: "org-a",
      name: "A",
      workProfileId: "wp-a",
      users: [],
      invites: [],
    };

    const orgB = {
      id: "org-b",
      name: "B",
      workProfileId: "wp-b",
      users: [],
      invites: [],
    };

    const user: User = {
      id: "u1",
      email: "u@x.test",
      orgs: [orgA, orgB],
      tasks: [],
      invites: [],
    };

    vi.mocked(fetchTasks).mockRejectedValue(new Error("network down"));

    act(() => {
      result.current.setUser(user);
    });

    await expect(result.current.setActiveOrganization("org-b")).rejects.toThrow(
      "network down",
    );

    expect(result.current.activeOrganizationId).toBe("org-a");
    expect(result.current.workProfileId).toBe("wp-a");
  });

  it("clears active context and tasks when organization is set to null", async () => {
    const { result } = renderHook(() => useUserStore());

    const orgA = {
      id: "org-a",
      name: "A",
      workProfileId: "wp-a",
      users: [],
      invites: [],
    };

    const user: User = {
      id: "u1",
      email: "u@x.test",
      orgs: [orgA],
      tasks: [
        {
          id: "t1",
          name: "Task 1",
          description: "",
          startDate: new Date(),
          endDate: new Date(),
          org: "org-a",
          dependencies: [],
        },
      ],
      invites: [],
    };

    act(() => {
      result.current.setUser(user);
    });

    await act(async () => {
      await result.current.setActiveOrganization(null);
    });

    expect(fetchTasks).not.toHaveBeenCalled();
    expect(result.current.activeOrganizationId).toBeNull();
    expect(result.current.workProfileId).toBeNull();
    expect(result.current.user.tasks).toEqual([]);
  });

  it("switches active organization without loading tasks when workProfileId is missing", async () => {
    const { result } = renderHook(() => useUserStore());

    const orgA = {
      id: "org-a",
      name: "A",
      workProfileId: "wp-a",
      users: [],
      invites: [],
    };

    const orgB = {
      id: "org-b",
      name: "B",
      workProfileId: null,
      users: [],
      invites: [],
    };

    const user: User = {
      id: "u1",
      email: "u@x.test",
      orgs: [orgA, orgB],
      tasks: [],
      invites: [],
    };

    act(() => {
      result.current.setUser(user);
    });

    await act(async () => {
      await result.current.setActiveOrganization("org-b");
    });

    expect(fetchTasks).not.toHaveBeenCalled();
    expect(result.current.activeOrganizationId).toBe("org-b");
    expect(result.current.workProfileId).toBe("wp-a");
  });

  it("switches organization without reloading when both organizations share the same workProfileId", async () => {
    const { result } = renderHook(() => useUserStore());

    const orgA = {
      id: "org-a",
      name: "A",
      workProfileId: "wp-shared",
      users: [],
      invites: [],
    };

    const orgB = {
      id: "org-b",
      name: "B",
      workProfileId: "wp-shared",
      users: [],
      invites: [],
    };

    const user: User = {
      id: "u1",
      email: "u@x.test",
      orgs: [orgA, orgB],
      tasks: [],
      invites: [],
    };

    act(() => {
      result.current.setUser(user);
    });

    await act(async () => {
      await result.current.setActiveOrganization("org-b");
    });

    expect(fetchTasks).not.toHaveBeenCalled();
    expect(result.current.activeOrganizationId).toBe("org-b");
    expect(result.current.workProfileId).toBe("wp-shared");
  });
});
