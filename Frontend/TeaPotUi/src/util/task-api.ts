import type { Task, TaskIntensity } from "./types";
const API_BASE = import.meta.env.VITE_API_BASE_URL ?? "";

/** Maps a backend UserTask object to the frontend Task type. */
const fromApi = (raw: Record<string, unknown>): Task => {
  // Preserve the authoritative time estimate from the server so that subsequent
  // updateTask calls never replace it with a derived (and potentially wrong) value.
  const timeEstimateMs = raw.timeEstimate
    ? intervalToMs(raw.timeEstimate as string)
    : undefined;

  return {
    id: raw.id as string,
    name: raw.name as string,
    description: (raw.description as string | undefined) ?? "",
    startDate: new Date((raw.earlyStart as string) ?? (raw.createdAt as string)),
    endDate: new Date((raw.earlyFinish as string) ?? (raw.deadline as string)),
    deadline: raw.deadline ? new Date(raw.deadline as string) : undefined,
    isFixed: raw.isFixed as boolean,
    priority: (raw.priority as Task["priority"]) ?? "medium",
    intensity: (raw.intensity as TaskIntensity | undefined) ?? "normal",
    status: (raw.status as Task["status"]) ?? "todo",
    org: raw.workProfileId as string,
    recurrence: "none",
    dependencies: [],
    timeEstimateMinutes: timeEstimateMs !== undefined ? Math.round(timeEstimateMs / 60_000) : undefined,
  };
};

/** Fetches all tasks for the user's personal work profile. */
export async function fetchTasks(workProfileId: string): Promise<Task[]> {
  const res = await fetch(`${API_BASE}/api/task/${encodeURIComponent(workProfileId)}`);
  if (!res.ok) throw new Error(`fetchTasks failed: ${res.status}`);
  const raw = await res.json() as Record<string, unknown>[];
  const tasks = raw.map(fromApi);
  // Resolve dependency IDs returned from the server to full Task objects
  const byId = new Map(tasks.map(t => [t.id!, t]));
  raw.forEach((r, i) => {
    const ids = (r.dependsOnTaskIds as string[] | undefined) ?? [];
    tasks[i].dependencies = ids.map(id => byId.get(id)).filter((t): t is Task => t !== undefined);
  });
  return tasks;
}

/** Fetches a single task by id. */
export async function fetchTask(workProfileId: string, taskId: string): Promise<Task> {
  const res = await fetch(`${API_BASE}/api/task/${encodeURIComponent(workProfileId)}/${encodeURIComponent(taskId)}`);
  if (!res.ok) throw new Error(`fetchTask failed: ${res.status}`);
  const raw = await res.json() as Record<string, unknown>;
  return fromApi(raw);
}

/** Builds the request body shared by createTask and updateTask. */
function buildTaskBody(task: Task) {
  const start = task.startDate.toISOString();
  const deadline = (task.deadline ?? task.endDate).toISOString();

  // Use the stored estimate if available; fall back to deriving it from startDate/endDate
  // (only reliable for new tasks where the user explicitly set start and end).
  const estimateMs = task.timeEstimateMinutes !== undefined
    ? task.timeEstimateMinutes * 60_000
    : task.endDate.getTime() - task.startDate.getTime();

  return {
    name: task.name,
    description: task.description ?? "",
    isFixed: task.isFixed ?? false,
    priority: task.priority ?? "medium",
    intensity: task.intensity ?? "normal",
    timeEstimate: msToInterval(estimateMs),
    deadline,
    status: task.status ?? "todo",
    earlyStart: start,
    earlyFinish: deadline,
    lateStart: start,
    lateFinish: deadline,
    dependsOnTaskIds: task.dependencies.map(d => d.id).filter((id): id is string => Boolean(id)),
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

export type TaskBlock = {
  taskId: string;
  taskName: string;
  taskStatus: string;
  startDate: Date;
  endDate: Date;
  isFixed: boolean;
};

/** Fetches all task blocks for the work profile (for calendar rendering). */
export async function fetchBlocks(workProfileId: string): Promise<TaskBlock[]> {
  const res = await fetch(`${API_BASE}/api/planning/${encodeURIComponent(workProfileId)}/blocks`);
  if (!res.ok) throw new Error(`fetchBlocks failed: ${res.status}`);
  const raw = await res.json() as Array<{
    taskId: string;
    taskName: string;
    taskStatus: string;
    startDate: string;
    endDate: string;
    isFixed: boolean;
  }>;
  return raw.map(b => ({
    taskId: b.taskId,
    taskName: b.taskName,
    taskStatus: b.taskStatus,
    startDate: new Date(b.startDate),
    endDate: new Date(b.endDate),
    isFixed: b.isFixed,
  }));
}

/** Converts a millisecond duration to a PostgreSQL interval string (HH:MM:SS). */
function msToInterval(ms: number): string {
  const totalSeconds = Math.floor(ms / 1000);
  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = totalSeconds % 60;
  return `${String(hours).padStart(2, "0")}:${String(minutes).padStart(2, "0")}:${String(seconds).padStart(2, "0")}`;
}

/** Converts a PostgreSQL interval string (HH:MM:SS or D.HH:MM:SS) to milliseconds. */
function intervalToMs(interval: string): number {
  // Npgsql serialises TimeSpan as "D.HH:MM:SS" for durations ≥ 1 day, or "HH:MM:SS" otherwise.
  const parts = interval.split(".");
  let days = 0;
  let timePart = interval;
  if (parts.length === 2 && parts[0].match(/^\d+$/)) {
    days = parseInt(parts[0], 10);
    timePart = parts[1];
  }
  const [h, m, s] = timePart.split(":").map(Number);
  return ((days * 86400) + (h * 3600) + (m * 60) + (s ?? 0)) * 1000;
}
