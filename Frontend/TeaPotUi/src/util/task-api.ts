import type { Task, TaskIntensity } from "./types";
const API_BASE = import.meta.env.VITE_API_BASE_URL ?? "";

/**
 * Maps a backend UserTask object to the frontend Task type.
 *
 * CRITICAL: This function preserves the server's authoritative time estimate.
 * The backend calculates timeEstimate based on scheduling logic; frontend should
 * never overwrite this with a derived value from local UI state. This ensures
 * that task scheduling remains consistent across sessions.
 *
 * @param raw - Raw response object from backend API with properties:
 *   - id: Unique task identifier (UUID)
 *   - name: Task name/title
 *   - description: Task description (optional, defaults to empty string)
 *   - timeEstimate: PostgreSQL interval string (HH:MM:SS or D.HH:MM:SS format)
 *   - isFixed: Boolean indicating if task has fixed time allocation
 *   - priority: "low" | "medium" | "high"
 *   - intensity: "low" | "normal" | "high" (effort/complexity level)
 *   - status: "todo" | "in-progress" | "done"
 *   - workProfileId: Associated work profile ID
 *   - earlyStart: ISO date string (earliest start time from scheduling)
 *   - earlyFinish: ISO date string (earliest finish time from scheduling)
 *   - deadline: ISO date string (user-specified deadline)
 *   - createdAt: ISO date string (task creation timestamp)
 *   - dependsOnTaskIds: Array of task IDs this task depends on
 *
 * @returns Mapped Task object ready for frontend consumption
 */
const fromApi = (raw: Record<string, unknown>): Task => {
  // Preserve the authoritative time estimate from the server so later edits do not
  // replace it with a derived value from local UI state. This is CRITICAL for
  // maintaining consistency across the application's scheduling logic.
  const timeEstimateMs = raw.timeEstimate
    ? intervalToMs(raw.timeEstimate as string)
    : undefined;

  return {
    id: raw.id as string,
    name: raw.name as string,
    description: (raw.description as string | undefined) ?? "",
    // Use earlyStart or fallback to createdAt; both are ISO date strings from backend
    startDate: new Date(
      (raw.earlyStart as string) ?? (raw.createdAt as string),
    ),
    // Use earlyFinish or fallback to deadline; represents the calculated finish time
    endDate: new Date((raw.earlyFinish as string) ?? (raw.deadline as string)),
    // deadline: User-specified deadline (distinct from calculated earlyFinish)
    deadline: raw.deadline ? new Date(raw.deadline as string) : undefined,
    // isFixed: Indicates whether task must be scheduled at a specific time (vs flexible)
    isFixed: raw.isFixed as boolean,
    priority: (raw.priority as Task["priority"]) ?? "medium",
    // intensity: Effort/complexity level affecting scheduling algorithm weighting
    intensity: (raw.intensity as TaskIntensity | undefined) ?? "normal",
    status: (raw.status as Task["status"]) ?? "todo",
    org: (raw.organizationId as string | null | undefined) ?? "",
    recurrence: "none",
    dependencies: [],
    // timeEstimateMinutes: Converted from server's interval format; kept in minutes for UI
    timeEstimateMinutes:
      timeEstimateMs !== undefined
        ? Math.round(timeEstimateMs / 60_000)
        : undefined,
  };
};

/**
 * Fetches all tasks for the user's personal work profile.
 *
 * This function performs TWO operations:
 * 1. Maps backend UserTask objects to frontend Task type
 * 2. Resolves dependency IDs into concrete Task references
 *
 * The dependency resolution is necessary because the backend returns only
 * dependency IDs (dependsOnTaskIds), but the frontend needs the full Task
 * objects for UI display and scheduling logic.
 *
 * @param workProfileId - UUID of the work profile to fetch tasks for
 * @param token - accesstoken for the api
 * @returns Promise resolving to array of Task objects with resolved dependencies
 * @throws Error if fetch fails (includes HTTP status in error message)
 *
 * @example
 * const tasks = await fetchTasks(workProfileId);
 * const taskWithDeps = tasks.find(t => t.id === 'abc123');
 * // taskWithDeps.dependencies will contain actual Task objects, not IDs
 */
export async function fetchTasks(
  workProfileId: string,
  token: string,
): Promise<Task[]> {
  const res = await fetch(
    `${API_BASE}/api/task/${encodeURIComponent(workProfileId)}`,
    {
      headers: {
        Authorization: `Bearer ${token}`,
      },
    },
  );
  if (!res.ok) throw new Error(`fetchTasks failed: ${res.status}`);
  const raw = (await res.json()) as Record<string, unknown>[];
  const tasks = raw.map(fromApi);

  // Build a map for O(1) dependency lookup
  const byId = new Map(tasks.map((t) => [t.id!, t]));

  // Resolve dependency IDs returned from server to concrete Task references
  raw.forEach((r, i) => {
    const ids = (r.dependsOnTaskIds as string[] | undefined) ?? [];
    tasks[i].dependencies = ids
      .map((id) => byId.get(id))
      .filter((t): t is Task => t !== undefined);
  });

  return tasks;
}

/**
 * Fetches a single task by ID.
 *
 * Used when updating or inspecting a specific task. Unlike fetchTasks(),
 * this does not resolve dependencies because it operates on a single task.
 *
 * @param workProfileId - UUID of the work profile
 * @param taskId - UUID of the task to fetch
 * @param token - accesstoken for the api
 * @returns Promise resolving to the Task object
 * @throws Error if task not found or fetch fails
 */
export async function fetchTask(
  workProfileId: string,
  taskId: string,
  token: string,
): Promise<Task> {
  const res = await fetch(
    `${API_BASE}/api/task/${encodeURIComponent(workProfileId)}/${encodeURIComponent(taskId)}`,
    { headers: { Authorization: `Bearer ${token}` } },
  );
  if (!res.ok) throw new Error(`fetchTask failed: ${res.status}`);
  const raw = (await res.json()) as Record<string, unknown>;
  return fromApi(raw);
}

/**
 * Builds the request body shared by both createTask and updateTask.
 *
 * IMPORTANT TIME HANDLING:
 * - Uses stored timeEstimateMinutes (from server) if available; otherwise
 *   derives estimate from the edited time range (endDate - startDate).
 * - The fallback is ONLY reliable for newly created tasks where both
 *   timestamps were explicitly chosen by the user.
 * - For updates, always preserve the server's timeEstimate.
 *
 * Backend field mapping:
 * - timeEstimate → msToInterval() → PostgreSQL interval format
 * - deadline → task's final deadline (user-specified)
 * - earlyStart/earlyFinish → calculated scheduling boundaries
 * - status → task status (todo, in-progress, done)
 *
 * @param task - The Task object to serialize for backend
 * @returns Request body object compatible with backend API
 *
 * @remarks
 * The request body contains redundant timestamp fields because the backend
 * uses them for different scheduling purposes:
 * - earlyStart/earlyFinish: earliest feasible scheduling window
 * - lateStart/lateFinish: latest feasible scheduling window
 * - deadline: ultimate deadline constraint
 */
function buildTaskBody(task: Task) {
  const start = task.startDate.toISOString();
  // Only send a deadline when the user actually set one. Falling back to endDate
  // would mean that after Auto-Schedule (where endDate becomes the planned
  // earlyFinish) every subsequent save would silently turn the planned finish
  // into a hard deadline, which then breaks re-planning.
  const deadline = task.deadline ? task.deadline.toISOString() : null;
  // Window for the (frontend-only) ES/EF/LS/LF fields: prefer an explicit deadline,
  // otherwise use the current endDate so the row is still well-formed for the API.
  const windowEnd = (task.deadline ?? task.endDate).toISOString();

  // Use the stored estimate if available; otherwise derive it from the edited time range.
  // The fallback is only reliable for newly created tasks where both timestamps were chosen explicitly.
  const estimateMs =
    task.timeEstimateMinutes !== undefined
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
    earlyFinish: windowEnd,
    lateStart: start,
    lateFinish: windowEnd,
    organizationId: task.org && task.org.length > 0 ? task.org : null,
    dependsOnTaskIds: task.dependencies
      .map((d) => d.id)
      .filter((id): id is string => Boolean(id)),
  };
}

/**
 * Creates a new task and returns it with the server-assigned ID.
 *
 * Flow:
 * 1. Build request body from Task object using buildTaskBody()
 * 2. POST to /api/task/{workProfileId}
 * 3. Parse response and map back to Task type
 * 4. Return mapped task with server-assigned properties (id, timestamps)
 *
 * @param workProfileId - UUID of work profile to add task to
 * @param task - Task object to create (id will be assigned by server)
 * @param token - accesstoken for the api
 * @returns Promise resolving to created Task with server-assigned id and timestamps
 * @throws Error with descriptive message including response text on failure
 */
export async function createTask(
  workProfileId: string,
  task: Task,
  token: string,
): Promise<Task> {
  const body = buildTaskBody(task);

  const res = await fetch(
    `${API_BASE}/api/task/${encodeURIComponent(workProfileId)}`,
    {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Authorization: `Bearer ${token}`,
      },
      body: JSON.stringify(body),
    },
  );
  if (!res.ok) {
    const text = await res.text().catch(() => res.statusText);
    throw new Error(`createTask failed: ${text}`);
  }
  const raw = (await res.json()) as Record<string, unknown>;
  return fromApi(raw);
}

/**
 * Updates an existing task (any field including status).
 *
 * Flow:
 * 1. Build request body from modified Task object
 * 2. PUT to /api/task/{workProfileId}/{taskId}
 * 3. Parse response and map back to Task type
 * 4. Return updated Task with server-computed properties
 *
 * NOTE: Does not perform optimistic updates. If update fails, local state
 * is not automatically reverted—the caller is responsible for error handling.
 *
 * @param workProfileId - UUID of the work profile
 * @param taskId - UUID of the task to update
 * @param task - Updated Task object with desired field values
 * @param token - accesstoken for the api
 * @returns Promise resolving to updated Task from server
 * @throws Error if update fails
 */
export async function updateTask(
  workProfileId: string,
  taskId: string,
  task: Task,
  token: string,
): Promise<Task> {
  const body = buildTaskBody(task);

  const res = await fetch(
    `${API_BASE}/api/task/${encodeURIComponent(workProfileId)}/${encodeURIComponent(taskId)}`,
    {
      method: "PUT",
      headers: {
        "Content-Type": "application/json",
        Authorization: `Bearer ${token}`,
      },
      body: JSON.stringify(body),
    },
  );
  if (!res.ok) throw new Error(`updateTask failed: ${res.status}`);
  const raw = (await res.json()) as Record<string, unknown>;
  return fromApi(raw);
}

/**
 * Deletes a task by ID.
 *
 * Sends DELETE request to backend. No response body is expected.
 *
 * @param workProfileId - UUID of the work profile
 * @param taskId - UUID of task to delete
 * @param token - accesstoken for the api
 * @throws Error if deletion fails
 */
export async function deleteTask(
  workProfileId: string,
  taskId: string,
  token: string,
): Promise<void> {
  const res = await fetch(
    `${API_BASE}/api/task/${encodeURIComponent(workProfileId)}/${encodeURIComponent(taskId)}`,
    { method: "DELETE", headers: { Authorization: `Bearer ${token}` } },
  );
  if (!res.ok) throw new Error(`deleteTask failed: ${res.status}`);
}

export type TaskBlock = {
  taskId: string;
  taskName: string;
  taskStatus: string;
  startDate: Date;
  endDate: Date;
  // isFixed: Whether task is allocated a fixed time slot vs. flexible scheduling
  isFixed: boolean;
};

/**
 * Fetches all task blocks for the work profile (for calendar rendering).
 *
 * This is DISTINCT from fetchTasks():
 * - fetchTasks(): Returns user-created Task objects with descriptions, priorities, etc.
 * - fetchBlocks(): Returns scheduler-computed TaskBlock entries (time slots on calendar)
 *
 * TaskBlocks represent the OUTPUT of the scheduling algorithm—when and where
 * tasks are actually scheduled on the calendar. They have:
 * - startDate/endDate: Calculated by the scheduling algorithm
 * - isFixed: Whether task was locked to a fixed time
 * - No priority/intensity/description (calendar view doesn't need those)
 *
 * Blocks are fetched separately because:
 * 1. They may change without task changes (re-scheduling algorithm run)
 * 2. Calendar view only needs time and status; full task details are in tasks.tsx
 * 3. Enables efficient calendar updates without refetching all task metadata
 *
 * @param workProfileId - UUID of work profile
 * @param token - accesstoken for the api
 * @returns Promise resolving to array of TaskBlock objects
 * @throws Error if fetch fails
 */
export async function fetchBlocks(
  workProfileId: string,
  token: string,
): Promise<TaskBlock[]> {
  const res = await fetch(
    `${API_BASE}/api/planning/${encodeURIComponent(workProfileId)}/blocks`,
    { headers: { Authorization: `Bearer ${token}` } },
  );
  if (!res.ok) throw new Error(`fetchBlocks failed: ${res.status}`);
  const raw = (await res.json()) as Array<{
    taskId: string;
    taskName: string;
    taskStatus: string;
    startDate: string;
    endDate: string;
    isFixed: boolean;
  }>;
  return raw.map((b) => ({
    taskId: b.taskId,
    taskName: b.taskName,
    taskStatus: b.taskStatus,
    startDate: new Date(b.startDate),
    endDate: new Date(b.endDate),
    isFixed: b.isFixed,
  }));
}

/**
 * Converts a millisecond duration to a PostgreSQL interval string (HH:MM:SS format).
 *
 * Used when sending time estimates to backend. PostgreSQL uses interval type
 * for durations, formatted as:
 * - "HH:MM:SS" for durations less than 1 day
 * - "D.HH:MM:SS" for durations >= 1 day (e.g., "1.02:30:45" = 1 day, 2 hours, 30 min, 45 sec)
 *
 * IMPORTANT: Npgsql (PostgreSQL .NET driver) expects this exact format.
 *
 * @param ms - Duration in milliseconds
 * @returns Formatted interval string compatible with PostgreSQL
 *
 * @example
 * msToInterval(3661000) // "01:01:01" (1 hour, 1 minute, 1 second)
 * msToInterval(90061000) // "1.01:01:01" (1 day, 1 hour, 1 minute, 1 second)
 */
function msToInterval(ms: number): string {
  const totalSeconds = Math.floor(ms / 1000);
  const days = Math.floor(totalSeconds / 86400);
  const timePart = totalSeconds % 86400;
  const hours = Math.floor(timePart / 3600);
  const minutes = Math.floor((timePart % 3600) / 60);
  const seconds = timePart % 60;
  // Include day prefix only if >= 1 day; pad hours/minutes/seconds to 2 digits
  return `${days > 0 ? `${days}.` : ""}${String(hours).padStart(2, "0")}:${String(minutes).padStart(2, "0")}:${String(seconds).padStart(2, "0")}`;
}

/**
 * Converts a PostgreSQL interval string to milliseconds.
 *
 * Handles both Npgsql serialization formats:
 * - "HH:MM:SS" for durations < 1 day
 * - "D.HH:MM:SS" for durations >= 1 day
 *
 * Npgsql formats TimeSpan duration fields using these conventions.
 * This function reverses that serialization for JavaScript consumption.
 *
 * @param interval - PostgreSQL interval string (HH:MM:SS or D.HH:MM:SS)
 * @returns Duration in milliseconds
 *
 * @example
 * intervalToMs("01:01:01") // 3661000 ms (1 hour, 1 minute, 1 second)
 * intervalToMs("1.02:30:45") // 95445000 ms (1 day, 2 hours, 30 minutes, 45 seconds)
 *
 * @remarks
 * Robust parsing:
 * - Handles missing seconds (defaults to 0)
 * - Correctly parses day prefix with decimal separator
 * - Returns milliseconds for precision in scheduling calculations
 */
function intervalToMs(interval: string): number {
  // Npgsql serializes TimeSpan as "D.HH:MM:SS" for durations >= 1 day, or "HH:MM:SS" otherwise.
  const parts = interval.split(".");
  let days = 0;
  let timePart = interval;

  // If we have a dot AND the first part is numeric, it's the day prefix
  if (parts.length === 2 && parts[0].match(/^\d+$/)) {
    days = parseInt(parts[0], 10);
    timePart = parts[1];
  }

  // Parse HH:MM:SS (seconds are optional for compatibility)
  const [h, m, s] = timePart.split(":").map(Number);

  // Convert to milliseconds: days + hours + minutes + seconds
  return (days * 86400 + h * 3600 + m * 60 + (s ?? 0)) * 1000;
}
