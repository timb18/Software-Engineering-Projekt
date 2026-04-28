import { describe, it, expect, vi, afterEach } from "vitest";
import { ensureUser, fetchTasks, createTask, updateTask, deleteTask } from "./task-api";
import type { Task } from "./types";

// ── Helpers ───────────────────────────────────────────────────────────────────

const WORK_PROFILE_ID = "11111111-0000-0000-0000-000000000000";
const TASK_ID = "22222222-0000-0000-0000-000000000000";

/** Mock a global fetch that resolves with the given data. */
const mockFetch = (data: unknown, ok = true, status = 200) =>
  vi.fn().mockResolvedValue({
    ok,
    status,
    statusText: ok ? "OK" : "Server Error",
    json: () => Promise.resolve(data),
    text: () => Promise.resolve(JSON.stringify(data)),
  });

/** A minimal Task object that satisfies the Task type. */
const makeTask = (overrides: Partial<Task> = {}): Task => ({
  id: TASK_ID,
  name: "Test Task",
  description: "A description",
  startDate: new Date("2026-04-25T09:00:00Z"),
  endDate: new Date("2026-04-25T10:00:00Z"),
  deadline: new Date("2026-04-25T18:00:00Z"),
  isFixed: false,
  priority: "medium",
  status: "todo",
  org: WORK_PROFILE_ID,
  recurrence: "none",
  dependencies: [],
  ...overrides,
});

/** A raw backend response that mirrors the shape UserTask serialises to. */
const apiTask = {
  id: TASK_ID,
  name: "Test Task",
  description: "A description",
  earlyStart: "2026-04-25T09:00:00Z",
  earlyFinish: "2026-04-25T10:00:00Z",
  deadline: "2026-04-25T18:00:00Z",
  isFixed: false,
  priority: "medium",
  status: "todo",
  workProfileId: WORK_PROFILE_ID,
  createdAt: "2026-04-25T08:00:00Z",
};

afterEach(() => {
  vi.restoreAllMocks();
});

// ── ensureUser ────────────────────────────────────────────────────────────────

describe("ensureUser", () => {
  it("POSTs to /api/auth/ensure with the email in the body", async () => {
    const response = { userId: "user-1", workProfileId: WORK_PROFILE_ID };
    globalThis.fetch = mockFetch(response);

    await ensureUser("test@example.com");

    const [url, init] = vi.mocked(globalThis.fetch).mock.calls[0];
    expect(url).toBe("/api/auth/ensure");
    expect(init?.method).toBe("POST");
    expect(JSON.parse(init?.body as string)).toEqual({ email: "test@example.com" });
  });

  it("returns userId and workProfileId on success", async () => {
    const response = { userId: "user-1", workProfileId: WORK_PROFILE_ID };
    globalThis.fetch = mockFetch(response);

    const result = await ensureUser("test@example.com");

    expect(result).toEqual(response);
  });

  it("throws when the response is not ok", async () => {
    globalThis.fetch = mockFetch(null, false, 500);

    await expect(ensureUser("test@example.com")).rejects.toThrow("ensureUser failed");
  });
});

// ── fetchTasks ────────────────────────────────────────────────────────────────

describe("fetchTasks", () => {
  it("GETs /api/task/{workProfileId}", async () => {
    globalThis.fetch = mockFetch([apiTask]);

    await fetchTasks(WORK_PROFILE_ID);

    const [url] = vi.mocked(globalThis.fetch).mock.calls[0];
    expect(url).toBe(`/api/task/${WORK_PROFILE_ID}`);
  });

  it("maps backend fields to frontend Task shape", async () => {
    globalThis.fetch = mockFetch([apiTask]);

    const tasks = await fetchTasks(WORK_PROFILE_ID);

    expect(tasks).toHaveLength(1);
    const task = tasks[0];
    expect(task.id).toBe(TASK_ID);
    expect(task.name).toBe("Test Task");
    expect(task.priority).toBe("medium");
    expect(task.status).toBe("todo");
    expect(task.startDate).toBeInstanceOf(Date);
  });

  it("throws when the response is not ok", async () => {
    globalThis.fetch = mockFetch(null, false, 404);

    await expect(fetchTasks(WORK_PROFILE_ID)).rejects.toThrow("fetchTasks failed");
  });
});

// ── createTask ────────────────────────────────────────────────────────────────

describe("createTask", () => {
  it("POSTs to /api/task/{workProfileId}", async () => {
    globalThis.fetch = mockFetch(apiTask);

    await createTask(WORK_PROFILE_ID, makeTask());

    const [url, init] = vi.mocked(globalThis.fetch).mock.calls[0];
    expect(url).toBe(`/api/task/${WORK_PROFILE_ID}`);
    expect(init?.method).toBe("POST");
  });

  it("returns the saved task with the server-assigned id", async () => {
    globalThis.fetch = mockFetch({ ...apiTask, id: "server-assigned-id" });

    const result = await createTask(WORK_PROFILE_ID, makeTask());

    expect(result.id).toBe("server-assigned-id");
  });

  it("sends priority and status from the task", async () => {
    globalThis.fetch = mockFetch(apiTask);
    const task = makeTask({ priority: "high", status: "in-progress" });

    await createTask(WORK_PROFILE_ID, task);

    const body = JSON.parse(vi.mocked(globalThis.fetch).mock.calls[0][1]!.body as string);
    expect(body.priority).toBe("high");
    expect(body.status).toBe("in-progress");
  });

  it("always sends intensity as 'normal'", async () => {
    globalThis.fetch = mockFetch(apiTask);

    await createTask(WORK_PROFILE_ID, makeTask());

    const body = JSON.parse(vi.mocked(globalThis.fetch).mock.calls[0][1]!.body as string);
    expect(body.intensity).toBe("normal");
  });

  it("sends timeEstimate as HH:MM:SS interval string", async () => {
    globalThis.fetch = mockFetch(apiTask);
    // endDate - startDate = 1 hour = "01:00:00"
    const task = makeTask({
      startDate: new Date("2026-04-25T09:00:00Z"),
      endDate: new Date("2026-04-25T10:00:00Z"),
    });

    await createTask(WORK_PROFILE_ID, task);

    const body = JSON.parse(vi.mocked(globalThis.fetch).mock.calls[0][1]!.body as string);
    expect(body.timeEstimate).toBe("01:00:00");
  });

  it("throws with error text when response is not ok", async () => {
    globalThis.fetch = vi.fn().mockResolvedValue({
      ok: false,
      status: 400,
      statusText: "Bad Request",
      text: () => Promise.resolve("Validation failed"),
    });

    await expect(createTask(WORK_PROFILE_ID, makeTask())).rejects.toThrow("createTask failed");
  });
});

// ── updateTask ────────────────────────────────────────────────────────────────

describe("updateTask", () => {
  it("PUTs to /api/task/{workProfileId}/{taskId}", async () => {
    globalThis.fetch = mockFetch(apiTask);

    await updateTask(WORK_PROFILE_ID, TASK_ID, makeTask());

    const [url, init] = vi.mocked(globalThis.fetch).mock.calls[0];
    expect(url).toBe(`/api/task/${WORK_PROFILE_ID}/${TASK_ID}`);
    expect(init?.method).toBe("PUT");
  });

  it("returns the updated task mapped to the frontend Task shape", async () => {
    globalThis.fetch = mockFetch({ ...apiTask, status: "done" });

    const result = await updateTask(WORK_PROFILE_ID, TASK_ID, makeTask({ status: "done" }));

    expect(result.status).toBe("done");
  });

  it("throws when response is not ok", async () => {
    globalThis.fetch = mockFetch(null, false, 404);

    await expect(updateTask(WORK_PROFILE_ID, TASK_ID, makeTask())).rejects.toThrow("updateTask failed");
  });
});

// ── deleteTask ────────────────────────────────────────────────────────────────

describe("deleteTask", () => {
  it("sends DELETE to /api/task/{workProfileId}/{taskId}", async () => {
    globalThis.fetch = mockFetch(null, true, 204);

    await deleteTask(WORK_PROFILE_ID, TASK_ID);

    const [url, init] = vi.mocked(globalThis.fetch).mock.calls[0];
    expect(url).toBe(`/api/task/${WORK_PROFILE_ID}/${TASK_ID}`);
    expect(init?.method).toBe("DELETE");
  });

  it("resolves without a return value on success", async () => {
    globalThis.fetch = mockFetch(null, true, 204);

    await expect(deleteTask(WORK_PROFILE_ID, TASK_ID)).resolves.toBeUndefined();
  });

  it("throws when response is not ok", async () => {
    globalThis.fetch = mockFetch(null, false, 404);

    await expect(deleteTask(WORK_PROFILE_ID, TASK_ID)).rejects.toThrow("deleteTask failed");
  });
});
