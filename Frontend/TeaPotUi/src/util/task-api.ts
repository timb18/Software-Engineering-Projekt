import type { Task } from "./types";

const API_BASE = import.meta.env.VITE_API_BASE_URL ?? "";

export type EnsureUserResponse = {
  userId: string;
  workProfileId: string;
};

/** After Auth0 login: find-or-create the backend user record, returns IDs for subsequent calls. */
export async function ensureUser(email: string): Promise<EnsureUserResponse> {
  const res = await fetch(`${API_BASE}/api/auth/ensure`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ email }),
  });
  if (!res.ok) throw new Error(`ensureUser failed: ${res.status} ${res.statusText}`);
  return res.json() as Promise<EnsureUserResponse>;
}

/** Maps a backend UserTask object to the frontend Task type. */
const fromApi = (raw: Record<string, unknown>): Task => ({
  id: raw.id as string,
  name: raw.name as string,
  description: (raw.description as string | undefined) ?? "",
  startDate: new Date((raw.earlyStart as string) ?? (raw.createdAt as string)),
  endDate: new Date((raw.deadline as string) ?? (raw.earlyFinish as string)),
  deadline: raw.deadline ? new Date(raw.deadline as string) : undefined,
  isFixed: raw.isFixed as boolean,
  priority: (raw.priority as Task["priority"]) ?? "medium",
  status: (raw.status as Task["status"]) ?? "todo",
  org: raw.workProfileId as string,
  recurrence: "none",
  dependencies: [],
});

/** Fetches all tasks for the user's personal work profile. */
export async function fetchTasks(workProfileId: string): Promise<Task[]> {
  const res = await fetch(`${API_BASE}/api/task/${encodeURIComponent(workProfileId)}`);
  if (!res.ok) throw new Error(`fetchTasks failed: ${res.status}`);
  const raw = await res.json() as Record<string, unknown>[];
  return raw.map(fromApi);
}

/** Builds the request body shared by createTask and updateTask. */
function buildTaskBody(task: Task) {
  const start = task.startDate.toISOString();
  const deadline = (task.deadline ?? task.endDate).toISOString();
  return {
    name: task.name,
    description: task.description ?? "",
    isFixed: task.isFixed ?? false,
    priority: task.priority ?? "medium",
    intensity: "normal",
    timeEstimate: msToInterval(task.endDate.getTime() - task.startDate.getTime()),
    deadline,
    status: task.status ?? "todo",
    earlyStart: start,
    earlyFinish: deadline,
    lateStart: start,
    lateFinish: deadline,
  };
}

/** Creates a new task and returns it with the server-assigned id. */
export async function createTask(workProfileId: string, task: Task): Promise<Task> {
  const body = buildTaskBody(task);

  const res = await fetch(`${API_BASE}/api/task/${encodeURIComponent(workProfileId)}`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
  if (!res.ok) {
    const text = await res.text().catch(() => res.statusText);
    throw new Error(`createTask failed: ${text}`);
  }
  const raw = await res.json() as Record<string, unknown>;
  return fromApi(raw);
}

/** Updates a task's status (or any other field). */
export async function updateTask(workProfileId: string, taskId: string, task: Task): Promise<Task> {
  const body = buildTaskBody(task);

  const res = await fetch(
    `${API_BASE}/api/task/${encodeURIComponent(workProfileId)}/${encodeURIComponent(taskId)}`,
    { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) },
  );
  if (!res.ok) throw new Error(`updateTask failed: ${res.status}`);
  const raw = await res.json() as Record<string, unknown>;
  return fromApi(raw);
}

/** Deletes a task by id. */
export async function deleteTask(workProfileId: string, taskId: string): Promise<void> {
  const res = await fetch(
    `${API_BASE}/api/task/${encodeURIComponent(workProfileId)}/${encodeURIComponent(taskId)}`,
    { method: "DELETE" },
  );
  if (!res.ok) throw new Error(`deleteTask failed: ${res.status}`);
}

/** Converts a millisecond duration to a PostgreSQL interval string (HH:MM:SS). */
function msToInterval(ms: number): string {
  const totalSeconds = Math.floor(ms / 1000);
  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = totalSeconds % 60;
  return `${String(hours).padStart(2, "0")}:${String(minutes).padStart(2, "0")}:${String(seconds).padStart(2, "0")}`;
}
