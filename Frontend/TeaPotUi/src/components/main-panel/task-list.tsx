import dayjs from "dayjs";
import { useMemo, useState, type FC } from "react";
import useUserStore from "../../stores/user-store";
import type { Task } from "../../util/types";
import { getDefaults } from "../../util/default-data";

type Sort = {
  criteria: SortCriteria;
  direction: SortDirection;
};

type SortCriteria = "deadline" | "org" | "timeSlot" | "priority";

type SortDirection = "ascending" | "descending";

const sortDefault: Sort = {
  criteria: "timeSlot",
  direction: "ascending",
};

const TaskBoard: FC = () => {
  const { user, setUser, saveTask, removeTask } = useUserStore();

  const [editingTask, setEditingTask] = useState<Task | null>(null);
  const [editForm, setEditForm] = useState({
    name: "",
    description: "",
    start: "",
    end: "",
    priority: "medium" as Task["priority"],
    status: "todo" as Task["status"],
    isFixed: false,
  });
  const [editError, setEditError] = useState<string | undefined>();

  const [form, setForm] = useState({
    name: "",
    description: "",
    durationMinutes: 60,
    priority: "medium" as Task["priority"],
    status: "todo" as Task["status"],
    deadline: "",
    dependencies: [] as string[],
    isFixed: false,
  });
  const [filterAssignee, setFilterAssignee] = useState<string | "all">("all");
  const [listSort, setListSort] = useState<Sort>(sortDefault);

  const [error, setError] = useState<string | undefined>();
  const [status, setStatus] = useState<string | undefined>();

  const dependencyOptions = useMemo(() => user.tasks ?? [], [user.tasks]);
  const assigneeOptions = useMemo(() => {
    const emails = new Set<string>();
    emails.add(user.email);
    user.orgs?.forEach((team) => {
      team.users.forEach((u) => emails.add(u.email));
    });
    return Array.from(emails);
  }, [user.email, user.orgs]);

  const openEdit = (task: Task) => {
    setEditingTask(task);
    setEditError(undefined);
    setEditForm({
      name: task.name,
      description: task.description ?? "",
      start: dayjs(task.startDate).format("YYYY-MM-DDTHH:mm"),
      end: dayjs(task.endDate).format("YYYY-MM-DDTHH:mm"),
      priority: task.priority ?? "medium",
      status: task.status ?? "todo",
      isFixed: !!task.isFixed,
    });
  };

  const saveEdit = () => {
    setEditError(undefined);
    if (!editingTask) return;
    if (!editForm.name.trim()) { setEditError("Title is required."); return; }
    const start = dayjs(editForm.start);
    const end = dayjs(editForm.end);
    if (!start.isValid() || !end.isValid()) { setEditError("Start and end must be valid."); return; }
    if (!end.isAfter(start)) { setEditError("End must be after start."); return; }

    const updatedTask: Task = {
      ...editingTask,
      name: editForm.name.trim(),
      description: editForm.description.trim(),
      startDate: start.toDate(),
      endDate: end.toDate(),
      priority: editForm.priority,
      status: editForm.status,
      isFixed: editForm.isFixed,
    };

    saveTask(updatedTask).catch((err: unknown) => setEditError(String(err)));
    setEditingTask(null);
  };

  const submitTask = () => {
    setError(undefined);
    setStatus(undefined);

    if (!form.name.trim()) {
      setError("Title is required.");
      return;
    }
    if (!form.deadline) {
      setError("Deadline is required.");
      return;
    }
    if (form.durationMinutes <= 0) {
      setError("Duration must be greater than 0 minutes.");
      return;
    }

    const deadline = dayjs(form.deadline);
    if (!deadline.isValid()) {
      setError("Deadline is invalid.");
      return;
    }

    const endDate = deadline.toDate();
    const startDate = deadline
      .subtract(form.durationMinutes, "minute")
      .toDate();

    const dependencies = dependencyOptions.filter((t) =>
      form.dependencies.includes(t.name),
    );

    const newTask: Task = {
      name: form.name.trim(),
      description: form.description.trim(),
      startDate,
      endDate,
      isFixed: form.isFixed,
      priority: form.priority,
      status: form.status ?? "todo",
      org: getDefaults().orgs[0]?.id ?? "",
      recurrence: "none",
      dependencies,
    };

    const conflicts = (user.tasks ?? []).filter((t) => {
      const s = dayjs(t.startDate);
      const e = dayjs(t.endDate);
      return s.isBefore(endDate) && e.isAfter(startDate);
    });
    if (conflicts.length > 0) {
      setError(
        `Overlap with ${conflicts.length} task(s). Consider rescheduling.`,
      );
    }

    setUser({
      ...user,
      tasks: [...(user.tasks ?? []), newTask],
    });

    setForm({
      name: "",
      description: "",
      durationMinutes: 60,
      priority: "medium",
      status: "todo",
      deadline: "",
      dependencies: [],
      isFixed: false,
    });
    setStatus("Task created");
  };

  const sortFn = (t1: Task, t2: Task) => {
    const sortDirectionMultiplier = listSort.direction === "ascending" ? 1 : -1;
    switch (listSort.criteria) {
      case "timeSlot":
        return (
          (t1.startDate.valueOf() - t2.startDate.valueOf()) *
          sortDirectionMultiplier
        );
      case "deadline":
        return (
          ((t1.deadline?.valueOf() ?? 0) - (t2.deadline?.valueOf() ?? 0)) *
          sortDirectionMultiplier
        );

      case "org":
        if (t1.org < t2.org) {
          return -1 * sortDirectionMultiplier;
        }
        if (t1.org > t2.org) {
          return 1 * sortDirectionMultiplier;
        }
        return 0;

      case "priority":
        if (
          (t1.priority === "low" && t2.priority === "medium") ||
          (t1.priority === "medium" && t2.priority === "high") ||
          (t1.priority === "low" && t2.priority === "high")
        ) {
          return -1 * sortDirectionMultiplier;
        }

        if (
          (t1.priority === "medium" && t2.priority === "low") ||
          (t1.priority === "high" && t2.priority === "medium") ||
          (t1.priority === "high" && t2.priority === "low")
        ) {
          return 1 * sortDirectionMultiplier;
        }

        return 0;

      default:
        return 0;
    }
  };

  const onChangeSortCriteria = (newCriteria: string) => {
    switch (newCriteria) {
      case "timeSlot":
        setListSort({ ...listSort, criteria: "timeSlot" });
        break;

      case "deadLine":
        setListSort({ ...listSort, criteria: "deadline" });
        break;

      case "priority":
        setListSort({ ...listSort, criteria: "priority" });
        break;

      case "org":
        setListSort({ ...listSort, criteria: "org" });
        break;

      default:
        setListSort({ ...listSort, criteria: sortDefault.criteria });
        break;
    }
  };

  const onChangeSortDirection = (newDirection:string) => {
    switch (newDirection) {
      case "ascending":
        setListSort({...listSort, direction: "ascending"})
        break;
    
      case "descending":
        setListSort({...listSort, direction: "descending"})
        break;

      default:
        setListSort({...listSort, direction: sortDefault.direction})
        break;
    }
  }

  return (
    <div className="grid h-full w-full grid-rows-[3.5rem_1fr] gap-6 p-6 text-slate-50">
      <div className="flex items-center justify-between">
        <div className="flex flex-col gap-1">
          <span className="text-xs tracking-[0.28em] text-emerald-300 uppercase">
            Tasks
          </span>
          <h1 className="text-4xl leading-tight font-semibold">Task List</h1>
          <span className="text-sm text-slate-400">
            Filter, Sort, Create & Manage
          </span>
        </div>
      </div>

      <div className="grid grid-cols-[1.1fr_0.9fr] gap-5 max-xl:grid-cols-1">
        <div className="flex flex-col gap-3 rounded-3xl border border-slate-800 bg-slate-900/70 p-5 shadow-xl backdrop-blur">
          <div className="flex items-center justify-between">
            <div className="text-lg font-semibold text-slate-50">Tasks</div>
          </div>
          <div className="flex flex-col gap-4 rounded-3xl border border-slate-800 p-5">
            <form className="flex flex-col gap-3">
              <div className="font-semibold">Filters</div>
              <div className="flex flex-col gap-1">
                <div className="text-sm tracking-[0.14em] text-slate-500">
                  DEADLINE
                </div>
                <div className="flex flex-row gap-4">
                  <div className="flex flex-row items-center gap-1">
                    <div className="text-sm">From</div>
                    <input
                      type="datetime-local"
                      className="rounded-xl border border-slate-800 bg-slate-900/80 px-3 py-2 text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
                    />
                  </div>
                  <div className="flex flex-row items-center gap-1">
                    <div className="text-sm">To</div>
                    <input
                      type="datetime-local"
                      className="rounded-xl border border-slate-800 bg-slate-900/80 px-3 py-2 text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
                    />
                  </div>
                </div>
              </div>
              <div className="flex flex-col gap-1">
                <div className="text-sm tracking-[0.14em] text-slate-500">
                  TIME SLOT
                </div>
                <div className="flex flex-row gap-4">
                  <div className="flex flex-row items-center gap-1">
                    <div className="text-sm">From</div>
                    <input
                      type="datetime-local"
                      className="rounded-xl border border-slate-800 bg-slate-900/80 px-3 py-2 text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
                    />
                  </div>
                  <div className="flex flex-row items-center gap-1">
                    <div className="text-sm">To</div>
                    <input
                      type="datetime-local"
                      className="rounded-xl border border-slate-800 bg-slate-900/80 px-3 py-2 text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
                    />
                  </div>
                </div>
              </div>
              <div className="grid grid-cols-2 gap-3">
                <div className="flex flex-col gap-1">
                  <div className="text-sm tracking-[0.14em] text-slate-500">
                    PRIORITY
                  </div>
                  <select
                    defaultValue="all"
                    className="rounded-xl border border-slate-800 bg-slate-900/80 px-3 py-2 text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
                  >
                    <option value="all">All</option>
                    <option value="high">High</option>
                    <option value="medium">Medium</option>
                    <option value="low">Low</option>
                  </select>
                </div>
                <div className="flex flex-col gap-1">
                  <div className="text-sm tracking-[0.14em] text-slate-500">
                    Organization
                  </div>
                  <select
                    defaultValue="all"
                    className="rounded-xl border border-slate-800 bg-slate-900/80 px-3 py-2 text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
                  >
                    <option value="all">All</option>
                    {user.orgs.map((org) => (
                      <option value={org.name} key={org.name}>
                        {org.name}
                      </option>
                    ))}
                  </select>
                </div>
              </div>
              <button
                type="reset"
                className="rounded-3xl border border-rose-300/60 bg-rose-500/10 py-1 font-semibold text-rose-100 hover:bg-rose-500/20"
              >
                Reset
              </button>
            </form>
            <form className="flex flex-col gap-3">
              <div className="font-semibold">Sort</div>
              <div className="grid grid-cols-2 gap-3">
                <div className="flex flex-col gap-1">
                  <div className="text-sm tracking-[0.14em] text-slate-500">
                    Criteria
                  </div>
                  <select
                    onChange={(e) => onChangeSortCriteria(e.target.value)}
                    defaultValue="timeSlot"
                    className="rounded-xl border border-slate-800 bg-slate-900/80 px-3 py-2 text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
                  >
                    <option value="timeSlot">Time Slot</option>
                    <option value="deadline">Deadline</option>
                    <option value="priority">Priority</option>
                    <option value="org">Organization</option>
                  </select>
                </div>
                <div className="flex flex-col gap-1">
                  <div className="text-sm tracking-[0.14em] text-slate-500">
                    Direction
                  </div>
                  <select
                  onChange={(e) => onChangeSortDirection(e.target.value)}
                    defaultValue="ascending"
                    className="rounded-xl border border-slate-800 bg-slate-900/80 px-3 py-2 text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
                  >
                    <option value="ascending">Ascending</option>
                    <option value="descending">Descending</option>
                  </select>
                </div>
              </div>
              <button
                type="reset"
                className="rounded-3xl border border-rose-300/60 bg-rose-500/10 py-1 font-semibold text-rose-100 hover:bg-rose-500/20"
                onClick={() => setListSort(sortDefault)}
              >
                Reset
              </button>
            </form>
          </div>
          <div className="mt-4 flex flex-col gap-4">
            {user.tasks.sort(sortFn).map((task) => (
              <div
                key={task.name}
                onClick={() => openEdit(task)}
                className="cursor-pointer rounded-3xl border border-slate-800 bg-slate-900/80 p-4 shadow-sm transition hover:border-emerald-400/40 hover:bg-slate-800/80"
              >
                <div className="text-xs tracking-[0.12em] text-emerald-200 uppercase">
                  {dayjs(task.startDate).format("ddd, DD MMM")}
                </div>
                <div className="text-lg font-semibold text-slate-50">
                  {task.name}
                </div>
                <div className="text-xs text-slate-300">
                  {dayjs(task.startDate).format("HH:mm")} -{" "}
                  {dayjs(task.endDate).format("HH:mm")}
                </div>
                {task.description && (
                  <div className="mt-1 text-sm text-slate-400">
                    {task.description}
                  </div>
                )}
              </div>
            ))}
          </div>
        </div>

        <div className="flex h-full flex-col gap-4 rounded-3xl border border-slate-800 bg-slate-900/80 p-5 shadow-xl backdrop-blur">
          <div className="text-lg font-semibold text-slate-50">Add Task</div>
          <div className="grid grid-cols-2 gap-2 text-xs text-slate-400">
            {[
              { label: "Daily standup", minutes: 15, fixed: true },
              { label: "Weekly review", minutes: 60, fixed: true },
              { label: "Focus block", minutes: 90, fixed: false },
              { label: "1:1", minutes: 45, fixed: true },
            ].map((tpl) => (
              <button
                key={tpl.label}
                onClick={() =>
                  setForm({
                    ...form,
                    name: tpl.label,
                    durationMinutes: tpl.minutes,
                    description: "",
                    isFixed: tpl.fixed ?? false,
                  })
                }
                className="rounded-xl border border-slate-800 bg-slate-900/80 px-3 py-2 text-left text-slate-200 transition hover:border-emerald-300/50 hover:text-emerald-100"
              >
                {tpl.label}
              </button>
            ))}
          </div>

          <div className="flex flex-col gap-1">
            <label className="text-xs tracking-[0.14em] text-slate-500 uppercase">
              Title
            </label>
            <input
              value={form.name}
              onChange={(e) => setForm({ ...form, name: e.target.value })}
              className="rounded-xl border border-slate-800 bg-slate-900/80 px-3 py-2 text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
              placeholder="Task title"
            />
          </div>

          <div className="flex flex-col gap-1">
            <label className="text-xs tracking-[0.14em] text-slate-500 uppercase">
              Description
            </label>
            <textarea
              value={form.description}
              onChange={(e) =>
                setForm({ ...form, description: e.target.value })
              }
              className="min-h-22.5 rounded-xl border border-slate-800 bg-slate-900/80 px-3 py-2 text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
              placeholder="What needs to be done"
            />
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div className="flex flex-col gap-1">
              <label className="text-xs tracking-[0.14em] text-slate-500 uppercase">
                Duration (min)
              </label>
              <input
                type="number"
                min={1}
                value={form.durationMinutes}
                onChange={(e) =>
                  setForm({ ...form, durationMinutes: Number(e.target.value) })
                }
                className="rounded-xl border border-slate-800 bg-slate-900/80 px-3 py-2 text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
              />
            </div>
            <div className="flex flex-col gap-1">
              <label className="text-xs tracking-[0.14em] text-slate-500 uppercase">
                Priority
              </label>
              <select
                value={form.priority}
                onChange={(e) =>
                  setForm({
                    ...form,
                    priority: e.target.value as Task["priority"],
                  })
                }
                className="rounded-xl border border-slate-800 bg-slate-900/80 px-3 py-2 text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
              >
                <option value="low">Low</option>
                <option value="medium">Medium</option>
                <option value="high">High</option>
              </select>
            </div>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div className="flex flex-col gap-1">
              <label className="text-xs tracking-[0.14em] text-slate-500 uppercase">
                Status
              </label>
              <select
                value={form.status ?? "todo"}
                onChange={(e) =>
                  setForm({ ...form, status: e.target.value as Task["status"] })
                }
                className="rounded-xl border border-slate-800 bg-slate-900/80 px-3 py-2 text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
              >
                <option value="todo">To Do</option>
                <option value="in-progress">In Progress</option>
                <option value="done">Done</option>
              </select>
            </div>
            <div className="flex flex-col gap-1">
              <label className="text-xs tracking-[0.14em] text-slate-500 uppercase">
                Assignee
              </label>
              <select
                value={filterAssignee}
                onChange={(e) =>
                  setFilterAssignee(e.target.value as typeof filterAssignee)
                }
                className="rounded-xl border border-slate-800 bg-slate-900/80 px-3 py-2 text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
              >
                <option value="all">Anyone</option>
                {assigneeOptions.map((email) => (
                  <option key={email} value={email}>
                    {email}
                  </option>
                ))}
              </select>
            </div>
          </div>

          <label className="flex items-center gap-3 rounded-xl border border-slate-800 bg-slate-900/70 px-3 py-2 text-xs text-slate-300">
            <input
              type="checkbox"
              checked={form.isFixed}
              onChange={(e) => setForm({ ...form, isFixed: e.target.checked })}
            />
            <div className="flex flex-col leading-tight">
              <span className="text-sm font-semibold text-slate-100">
                Fixed timeslot
              </span>
              <span className="text-[11px] text-slate-500">
                Use for standups and meetings that must stay at their time.
              </span>
            </div>
          </label>

          <div className="flex flex-col gap-1">
            <label className="text-xs tracking-[0.14em] text-slate-500 uppercase">
              Deadline
            </label>
            <input
              type="datetime-local"
              value={form.deadline}
              onChange={(e) => setForm({ ...form, deadline: e.target.value })}
              className="rounded-xl border border-slate-800 bg-slate-900/80 px-3 py-2 text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
            />
          </div>

          <div className="flex flex-col gap-2">
            <label className="text-xs tracking-[0.14em] text-slate-500 uppercase">
              Dependencies
            </label>
            <div className="flex max-h-44 flex-col gap-2 overflow-y-auto rounded-xl border border-slate-800 bg-slate-900/60 p-3">
              {dependencyOptions.length === 0 && (
                <div className="text-xs text-slate-500">
                  No tasks available yet.
                </div>
              )}
              {dependencyOptions.map((dep) => {
                const checked = form.dependencies.includes(dep.name);
                return (
                  <label
                    key={dep.name}
                    className="flex items-center gap-2 text-sm text-slate-200"
                  >
                    <input
                      type="checkbox"
                      checked={checked}
                      onChange={(e) => {
                        const next = e.target.checked
                          ? [...form.dependencies, dep.name]
                          : form.dependencies.filter((n) => n !== dep.name);
                        setForm({ ...form, dependencies: next });
                      }}
                    />
                    <span>{dep.name}</span>
                  </label>
                );
              })}
            </div>
          </div>

          {error && <div className="text-sm text-rose-300">{error}</div>}
          {status && <div className="text-sm text-emerald-200">{status}</div>}

          <button
            onClick={submitTask}
            className="mt-1 w-full rounded-xl border border-emerald-300/60 bg-emerald-400/15 px-4 py-2 text-sm font-semibold text-emerald-100 shadow-sm transition hover:bg-emerald-400/25"
          >
            Add task
          </button>
        </div>
      </div>

      {editingTask && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur">
          <div className="w-full max-w-2xl rounded-3xl border border-slate-800 bg-slate-900/95 p-6 shadow-2xl">
            <div className="mb-4 flex items-center justify-between">
              <div>
                <div className="text-xs uppercase tracking-[0.18em] text-emerald-300">Edit task</div>
                <div className="text-2xl font-semibold text-slate-50">{editingTask.name}</div>
              </div>
              <button
                onClick={() => setEditingTask(null)}
                className="rounded-full border border-slate-800 bg-slate-900 px-3 py-1 text-xs text-slate-300 hover:border-emerald-300/60 hover:text-emerald-100"
              >
                Close
              </button>
            </div>

            <div className="grid grid-cols-2 gap-4 text-sm text-slate-200">
              <div className="flex flex-col gap-2">
                <label className="text-xs uppercase tracking-[0.14em] text-slate-500">Title</label>
                <input
                  value={editForm.name}
                  onChange={(e) => setEditForm({ ...editForm, name: e.target.value })}
                  className="rounded-xl border border-slate-800 bg-slate-900 px-3 py-2 text-slate-50 outline-none ring-emerald-400/40 focus:border-emerald-400/60 focus:ring"
                />
              </div>
              <div className="flex flex-col gap-2">
                <label className="text-xs uppercase tracking-[0.14em] text-slate-500">Priority</label>
                <select
                  value={editForm.priority}
                  onChange={(e) => setEditForm({ ...editForm, priority: e.target.value as Task["priority"] })}
                  className="rounded-xl border border-slate-800 bg-slate-900 px-3 py-2 text-slate-50 outline-none ring-emerald-400/40 focus:border-emerald-400/60 focus:ring"
                >
                  <option value="low">Low</option>
                  <option value="medium">Medium</option>
                  <option value="high">High</option>
                </select>
              </div>
              <div className="flex flex-col gap-2">
                <label className="text-xs uppercase tracking-[0.14em] text-slate-500">Status</label>
                <select
                  value={editForm.status}
                  onChange={(e) => setEditForm({ ...editForm, status: e.target.value as Task["status"] })}
                  className="rounded-xl border border-slate-800 bg-slate-900 px-3 py-2 text-slate-50 outline-none ring-emerald-400/40 focus:border-emerald-400/60 focus:ring"
                >
                  <option value="todo">To Do</option>
                  <option value="in-progress">In Progress</option>
                  <option value="done">Done</option>
                </select>
              </div>
              <div className="flex flex-col gap-2">
                <label className="text-xs uppercase tracking-[0.14em] text-slate-500">Start</label>
                <input
                  type="datetime-local"
                  value={editForm.start}
                  onChange={(e) => setEditForm({ ...editForm, start: e.target.value })}
                  className="rounded-xl border border-slate-800 bg-slate-900 px-3 py-2 text-slate-50 outline-none ring-emerald-400/40 focus:border-emerald-400/60 focus:ring"
                />
              </div>
              <div className="flex flex-col gap-2">
                <label className="text-xs uppercase tracking-[0.14em] text-slate-500">End</label>
                <input
                  type="datetime-local"
                  value={editForm.end}
                  onChange={(e) => setEditForm({ ...editForm, end: e.target.value })}
                  className="rounded-xl border border-slate-800 bg-slate-900 px-3 py-2 text-slate-50 outline-none ring-emerald-400/40 focus:border-emerald-400/60 focus:ring"
                />
              </div>
              <div className="flex flex-col gap-2 col-span-2">
                <label className="text-xs uppercase tracking-[0.14em] text-slate-500">Description</label>
                <textarea
                  value={editForm.description}
                  onChange={(e) => setEditForm({ ...editForm, description: e.target.value })}
                  className="min-h-[5rem] rounded-xl border border-slate-800 bg-slate-900 px-3 py-2 text-slate-50 outline-none ring-emerald-400/40 focus:border-emerald-400/60 focus:ring"
                />
              </div>
              <label className="col-span-2 flex items-center gap-3 rounded-xl border border-slate-800 bg-slate-900 px-3 py-2 text-xs text-slate-300">
                <input
                  type="checkbox"
                  checked={editForm.isFixed}
                  onChange={(e) => setEditForm({ ...editForm, isFixed: e.target.checked })}
                />
                <div className="flex flex-col leading-tight">
                  <span className="text-sm font-semibold text-slate-100">Fixed timeslot</span>
                  <span className="text-[11px] text-slate-500">Keeps meeting time locked.</span>
                </div>
              </label>
            </div>

            {editError && <div className="mt-3 text-sm text-rose-300">{editError}</div>}

            <div className="mt-4 flex justify-between gap-3">
              <button
                onClick={() => {
                  if (editingTask?.id) {
                    removeTask(editingTask.id).catch((err: unknown) => setEditError(String(err)));
                  } else {
                    setUser({ ...user, tasks: (user.tasks ?? []).filter((t) => t !== editingTask) });
                  }
                  setEditingTask(null);
                }}
                className="rounded-full border border-rose-800/60 bg-rose-900/30 px-4 py-2 text-sm text-rose-300 hover:bg-rose-900/50"
              >
                Delete
              </button>
              <div className="flex gap-3">
                <button
                  onClick={() => setEditingTask(null)}
                  className="rounded-full border border-slate-700 bg-slate-900 px-4 py-2 text-sm text-slate-300 hover:border-slate-600"
                >
                  Cancel
                </button>
                <button
                  onClick={saveEdit}
                  className="rounded-full border border-emerald-500/60 bg-emerald-500/15 px-5 py-2 text-sm font-semibold text-emerald-100 hover:bg-emerald-500/25"
                >
                  Save
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default TaskBoard;
