import dayjs from "dayjs";
import { useMemo, useState, type FC } from "react";
import useUserStore from "../../stores/user-store";
import type { Task } from "../../util/types";

const TaskBoard: FC = () => {
  const { user, setUser } = useUserStore();

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
  const [filterStatus, setFilterStatus] = useState<"all" | "todo" | "in-progress" | "done">("all");
  const [error, setError] = useState<string | undefined>();
  const [status, setStatus] = useState<string | undefined>();

  if (!user) return <></>;

  const dependencyOptions = useMemo(() => user.tasks ?? [], [user.tasks]);
  const assigneeOptions = useMemo(() => {
    const emails = new Set<string>();
    emails.add(user.email);
    user.teams?.forEach((team) => {
      team.users.forEach((u) => emails.add(u.email));
    });
    return Array.from(emails);
  }, [user.email, user.teams]);

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
    const startDate = deadline.subtract(form.durationMinutes, "minute").toDate();

    const dependencies = dependencyOptions.filter((t) => form.dependencies.includes(t.name));
    const selectedAssignee = filterAssignee === "all" ? user.email : filterAssignee;

    const newTask: Task = {
      name: form.name.trim(),
      description: form.description.trim(),
      startDate,
      endDate,
      isFixed: form.isFixed,
      priority: form.priority,
      status: form.status ?? "todo",
      assigneeEmail: selectedAssignee,
      recurrence: "none",
      dependencies,
    };

    const conflicts = (user.tasks ?? []).filter((t) => {
      if (t.assigneeEmail && selectedAssignee && t.assigneeEmail !== selectedAssignee) return false;
      const s = dayjs(t.startDate);
      const e = dayjs(t.endDate);
      return s.isBefore(endDate) && e.isAfter(startDate);
    });
    if (conflicts.length > 0) {
      setError(`Overlap with ${conflicts.length} task(s). Consider rescheduling.`);
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

  const filteredTasks = (user.tasks ?? []).filter((t) => {
    const byStatus = filterStatus === "all" || (t.status ?? "todo") === filterStatus;
    const byAssignee = filterAssignee === "all" || t.assigneeEmail === filterAssignee;
    return byStatus && byAssignee;
  });

  return (
    <div className="grid h-full w-full grid-rows-[3.5rem_1fr] gap-6 p-6 text-slate-50">
      <div className="flex items-center justify-between">
        <div className="flex flex-col gap-1">
          <span className="text-xs uppercase tracking-[0.28em] text-emerald-300">Tasks</span>
          <h1 className="text-4xl font-semibold leading-tight">Task Board</h1>
          <span className="text-sm text-slate-400">Status, Assignee, Create & Manage</span>
        </div>
        <div className="flex items-center gap-2 text-xs text-slate-400">
          <select
            value={filterStatus}
            onChange={(e) => setFilterStatus(e.target.value as typeof filterStatus)}
            className="rounded-full border border-slate-800 bg-slate-900/70 px-3 py-1 outline-none"
          >
            <option value="all">All statuses</option>
            <option value="todo">To Do</option>
            <option value="in-progress">In Progress</option>
            <option value="done">Done</option>
          </select>
          <select
            value={filterAssignee}
            onChange={(e) => setFilterAssignee(e.target.value as typeof filterAssignee)}
            className="rounded-full border border-slate-800 bg-slate-900/70 px-3 py-1 outline-none"
          >
            <option value="all">All assignees</option>
            {assigneeOptions.map((email) => (
              <option key={email} value={email}>
                {email}
              </option>
            ))}
          </select>
        </div>
      </div>

      <div className="grid grid-cols-[1.1fr_0.9fr] gap-5 max-xl:grid-cols-1">
        <div className="rounded-3xl border border-slate-800 bg-slate-900/70 p-5 shadow-xl backdrop-blur">
          <div className="flex items-center justify-between">
            <div className="text-lg font-semibold text-slate-50">Board</div>
            <div className="text-xs text-slate-400">Drag & drop not implemented yet (demo)</div>
          </div>
          <div className="mt-4 grid grid-cols-3 gap-4 max-lg:grid-cols-1">
            {["todo", "in-progress", "done"].map((col) => (
              <div key={col} className="rounded-2xl border border-slate-800 bg-slate-900/80 p-4">
                <div className="flex items-center justify-between text-sm font-semibold text-slate-100">
                  <span className="uppercase tracking-[0.14em] text-slate-400">{col.replace("-", " ")}</span>
                  <span className="rounded-full bg-slate-800 px-2 py-1 text-[11px] text-slate-300">
                    {filteredTasks.filter((t) => (t.status ?? "todo") === col).length}
                  </span>
                </div>
                <div className="mt-3 flex flex-col gap-2 text-sm text-slate-200">
                  {filteredTasks
                    .filter((t) => (t.status ?? "todo") === col)
                    .map((t) => (
                      <div
                        key={`${t.name}-${t.startDate.toString()}-board`}
                        className="rounded-xl border border-slate-800 bg-slate-900/70 p-3 shadow-sm"
                      >
                        <div className="flex items-center justify-between gap-2 text-sm font-semibold text-slate-50">
                          <span className="truncate">{t.name}</span>
                          <div className="flex items-center gap-2">
                            {t.isFixed && (
                              <span className="rounded-full bg-emerald-500/20 px-2 py-1 text-[11px] uppercase tracking-wide text-emerald-50">
                                Fixed
                              </span>
                            )}
                            <span className="rounded-full bg-emerald-400/15 px-2 py-1 text-[11px] uppercase tracking-wide text-emerald-100">
                              {t.priority ?? "medium"}
                            </span>
                          </div>
                        </div>
                        <div className="text-xs text-slate-400">
                          {dayjs(t.startDate).format("DD MMM, HH:mm")}
                        </div>
                        {t.assigneeEmail && (
                          <div className="text-xs text-slate-300">{t.assigneeEmail}</div>
                        )}
                      </div>
                    ))}
                  {filteredTasks.filter((t) => (t.status ?? "todo") === col).length === 0 && (
                    <div className="rounded-xl border border-dashed border-slate-800 bg-slate-900/60 p-3 text-xs text-slate-500">
                      No tasks here.
                    </div>
                  )}
                </div>
              </div>
            ))}
          </div>
        </div>

        <div className="flex h-full flex-col gap-4 rounded-3xl border border-slate-800 bg-slate-900/80 p-5 shadow-xl backdrop-blur">
          <div className="text-lg font-semibold text-slate-50">Task anlegen</div>
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
            <label className="text-xs uppercase tracking-[0.14em] text-slate-500">Title</label>
            <input
              value={form.name}
              onChange={(e) => setForm({ ...form, name: e.target.value })}
              className="rounded-xl border border-slate-800 bg-slate-900/80 px-3 py-2 text-slate-50 outline-none ring-emerald-400/40 focus:border-emerald-400/60 focus:ring"
              placeholder="Task title"
            />
          </div>

          <div className="flex flex-col gap-1">
            <label className="text-xs uppercase tracking-[0.14em] text-slate-500">Description</label>
            <textarea
              value={form.description}
              onChange={(e) => setForm({ ...form, description: e.target.value })}
              className="min-h-22.5 rounded-xl border border-slate-800 bg-slate-900/80 px-3 py-2 text-slate-50 outline-none ring-emerald-400/40 focus:border-emerald-400/60 focus:ring"
              placeholder="What needs to be done"
            />
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div className="flex flex-col gap-1">
              <label className="text-xs uppercase tracking-[0.14em] text-slate-500">Duration (min)</label>
              <input
                type="number"
                min={1}
                value={form.durationMinutes}
                onChange={(e) => setForm({ ...form, durationMinutes: Number(e.target.value) })}
                className="rounded-xl border border-slate-800 bg-slate-900/80 px-3 py-2 text-slate-50 outline-none ring-emerald-400/40 focus:border-emerald-400/60 focus:ring"
              />
            </div>
            <div className="flex flex-col gap-1">
              <label className="text-xs uppercase tracking-[0.14em] text-slate-500">Priority</label>
              <select
                value={form.priority}
                onChange={(e) => setForm({ ...form, priority: e.target.value as Task["priority"] })}
                className="rounded-xl border border-slate-800 bg-slate-900/80 px-3 py-2 text-slate-50 outline-none ring-emerald-400/40 focus:border-emerald-400/60 focus:ring"
              >
                <option value="low">Low</option>
                <option value="medium">Medium</option>
                <option value="high">High</option>
              </select>
            </div>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div className="flex flex-col gap-1">
              <label className="text-xs uppercase tracking-[0.14em] text-slate-500">Status</label>
              <select
                value={form.status ?? "todo"}
                onChange={(e) => setForm({ ...form, status: e.target.value as Task["status"] })}
                className="rounded-xl border border-slate-800 bg-slate-900/80 px-3 py-2 text-slate-50 outline-none ring-emerald-400/40 focus:border-emerald-400/60 focus:ring"
              >
                <option value="todo">To Do</option>
                <option value="in-progress">In Progress</option>
                <option value="done">Done</option>
              </select>
            </div>
            <div className="flex flex-col gap-1">
              <label className="text-xs uppercase tracking-[0.14em] text-slate-500">Assignee</label>
              <select
                value={filterAssignee}
                onChange={(e) => setFilterAssignee(e.target.value as typeof filterAssignee)}
                className="rounded-xl border border-slate-800 bg-slate-900/80 px-3 py-2 text-slate-50 outline-none ring-emerald-400/40 focus:border-emerald-400/60 focus:ring"
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
              <span className="text-sm font-semibold text-slate-100">Fixed timeslot</span>
              <span className="text-[11px] text-slate-500">Use for standups and meetings that must stay at their time.</span>
            </div>
          </label>

          <div className="flex flex-col gap-1">
            <label className="text-xs uppercase tracking-[0.14em] text-slate-500">Deadline</label>
            <input
              type="datetime-local"
              value={form.deadline}
              onChange={(e) => setForm({ ...form, deadline: e.target.value })}
              className="rounded-xl border border-slate-800 bg-slate-900/80 px-3 py-2 text-slate-50 outline-none ring-emerald-400/40 focus:border-emerald-400/60 focus:ring"
            />
          </div>

          <div className="flex flex-col gap-2">
            <label className="text-xs uppercase tracking-[0.14em] text-slate-500">Dependencies</label>
            <div className="flex flex-col gap-2 rounded-xl border border-slate-800 bg-slate-900/60 p-3 max-h-44 overflow-y-auto">
              {dependencyOptions.length === 0 && (
                <div className="text-xs text-slate-500">No tasks available yet.</div>
              )}
              {dependencyOptions.map((dep) => {
                const checked = form.dependencies.includes(dep.name);
                return (
                  <label key={dep.name} className="flex items-center gap-2 text-sm text-slate-200">
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
    </div>
  );
};

export default TaskBoard;
