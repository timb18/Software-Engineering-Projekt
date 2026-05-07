import dayjs from "dayjs";
import { createPortal } from "react-dom";
import { useEffect, useMemo, useRef, useState, type FC } from "react";
import useUserStore from "../../stores/user-store";
import EditTaskModal from "../EditTaskModal";
import type { Task } from "../../util/types";

// ── Types ─────────────────────────────────────────────────────────────────────

type SortCriteria = "timeSlot" | "deadline" | "priority" | "name";
type SortDirection = "asc" | "desc";
type StatusFilter = "active" | "todo" | "in-progress" | "done";

const PAGE_SIZE = 15;

// ── Constants ────────────────────────────────────────────────────────────────

const PRIORITY_ORDER: Record<NonNullable<Task["priority"]>, number> = {
  low: 1,
  medium: 2,
  high: 3,
};

const PRIORITY_BADGE: Record<string, string> = {
  high: "border border-rose-500/30 bg-rose-500/20 text-rose-300",
  medium: "border border-amber-500/30 bg-amber-500/20 text-amber-300",
  low: "border border-slate-600/40 bg-slate-600/30 text-slate-400",
};

const STATUS_BADGE: Record<string, string> = {
  todo: "bg-slate-700/60 text-slate-300",
  "in-progress": "bg-blue-500/20 text-blue-300",
  done: "bg-emerald-500/20 text-emerald-300",
};

const STATUS_LABEL: Record<string, string> = {
  todo: "To Do",
  "in-progress": "In Progress",
  done: "Done",
};

const SORT_LABEL: Record<SortCriteria, string> = {
  timeSlot: "Time",
  deadline: "Deadline",
  priority: "Priority",
  name: "Name",
};

const TEMPLATES = [
  { label: "Daily standup", minutes: 15, fixed: true },
  { label: "Weekly review", minutes: 60, fixed: true },
  { label: "Focus block", minutes: 90, fixed: false },
  { label: "1:1 Meeting", minutes: 45, fixed: true },
];

// ── Create Task Modal ─────────────────────────────────────────────────────────

interface CreateTaskModalProps {
  onClose: () => void;
}

const CreateTaskModal: FC<CreateTaskModalProps> = ({ onClose }) => {
  const { user, addTask } = useUserStore();
  const overlayRef = useRef<HTMLDivElement>(null);

  const [form, setForm] = useState({
    name: "",
    description: "",
    durationMinutes: 60,
    priority: "medium" as NonNullable<Task["priority"]>,
    status: "todo" as NonNullable<Task["status"]>,
    intensity: "normal" as NonNullable<Task["intensity"]>,
    deadline: "",
    startDate: "",
    isFixed: false,
    dependencies: [] as string[],
  });
  const [error, setError] = useState<string>();
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    const fn = (e: KeyboardEvent) => {
      if (e.key === "Escape") onClose();
    };
    document.addEventListener("keydown", fn);
    return () => document.removeEventListener("keydown", fn);
  }, [onClose]);

  const dependencyOptions = useMemo(() => user.tasks ?? [], [user.tasks]);

  const applyTemplate = (tpl: (typeof TEMPLATES)[number]) => {
    setForm((f) => ({
      ...f,
      name: tpl.label,
      durationMinutes: tpl.minutes,
      isFixed: tpl.fixed,
    }));
  };

  const submit = async () => {
    setError(undefined);
    if (!form.name.trim()) {
      setError("Title is required.");
      return;
    }
    if (!form.deadline) {
      setError(form.isFixed ? "End time is required." : "Deadline is required.");
      return;
    }
    if (form.isFixed && !form.startDate) {
      setError("Start time is required for fixed tasks.");
      return;
    }
    if (!form.isFixed && form.durationMinutes <= 0) {
      setError("Duration must be greater than 0 minutes.");
      return;
    }

    const deadline = dayjs(form.deadline);
    if (!deadline.isValid()) {
      setError("Invalid date/time.");
      return;
    }

    let startDate: Date;
    let endDate: Date;
    if (form.isFixed) {
      const start = dayjs(form.startDate);
      if (!start.isValid()) {
        setError("Start time is invalid.");
        return;
      }
      if (!deadline.isAfter(start)) {
        setError("End time must be after start time.");
        return;
      }
      startDate = start.toDate();
      endDate = deadline.toDate();
    } else {
      endDate = deadline.toDate();
      startDate = deadline.subtract(form.durationMinutes, "minute").toDate();
    }

    const newTask: Task = {
      name: form.name.trim(),
      description: form.description.trim(),
      startDate,
      endDate,
      isFixed: form.isFixed,
      priority: form.priority,
      status: form.status,
      intensity: form.intensity,
      org: user.orgs?.[0]?.id ?? "",
      recurrence: "none",
      dependencies: dependencyOptions.filter((t) =>
        form.dependencies.includes(t.name),
      ),
    };

    setSaving(true);
    try {
      await addTask(newTask);
      onClose();
    } catch {
      setError("Failed to save task. Please try again.");
    } finally {
      setSaving(false);
    }
  };

  const fieldClass =
    "rounded-xl border border-slate-800 bg-slate-800/60 px-3 py-2 text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring transition text-sm";

  return createPortal(
    <div
      ref={overlayRef}
      className="fixed inset-0 z-50 flex items-end justify-center bg-black/60 backdrop-blur-sm sm:items-center"
      onClick={(e) => {
        if (e.target === overlayRef.current) onClose();
      }}
    >
      <div className="flex w-full max-w-2xl max-h-[90dvh] flex-col gap-4 overflow-y-auto rounded-t-3xl border border-slate-700 bg-slate-900 p-6 shadow-2xl sm:rounded-3xl">
        {/* Header */}
        <div className="flex items-center justify-between">
          <h2 className="text-xl font-semibold text-slate-50">New Task</h2>
          <button
            onClick={onClose}
            className="rounded-xl p-1.5 text-slate-400 transition hover:bg-slate-800 hover:text-slate-200"
            aria-label="Close dialog"
          >
            ✕
          </button>
        </div>

        {/* Quick templates */}
        <div>
          <div className="mb-2 text-xs tracking-[0.14em] text-slate-500 uppercase">
            Quick templates
          </div>
          <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
            {TEMPLATES.map((tpl) => (
              <button
                key={tpl.label}
                type="button"
                onClick={() => applyTemplate(tpl)}
                className="rounded-xl border border-slate-800 bg-slate-800/60 px-3 py-2 text-left transition hover:border-emerald-300/50 hover:bg-slate-700/60 hover:text-emerald-100"
              >
                <div className="text-xs font-medium text-slate-200">
                  {tpl.label}
                </div>
                <div className="text-[11px] text-slate-500">{tpl.minutes} min</div>
              </button>
            ))}
          </div>
        </div>

        {/* Title */}
        <div className="flex flex-col gap-1">
          <label className="text-xs tracking-[0.14em] text-slate-500 uppercase">
            Title <span className="text-rose-400">*</span>
          </label>
          <input
            autoFocus
            value={form.name}
            onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))}
            className={fieldClass}
            placeholder="What needs to be done?"
          />
        </div>

        {/* Description */}
        <div className="flex flex-col gap-1">
          <label className="text-xs tracking-[0.14em] text-slate-500 uppercase">
            Description
          </label>
          <textarea
            rows={3}
            value={form.description}
            onChange={(e) =>
              setForm((f) => ({ ...f, description: e.target.value }))
            }
            className={`${fieldClass} resize-none`}
            placeholder="Optional details…"
          />
        </div>

        {/* Fixed timeslot toggle */}
        <label className="flex cursor-pointer items-center gap-3 rounded-xl border border-slate-800 bg-slate-800/40 px-3 py-2.5 transition hover:border-emerald-400/30">
          <input
            type="checkbox"
            checked={form.isFixed}
            onChange={(e) =>
              setForm((f) => ({ ...f, isFixed: e.target.checked }))
            }
            className="size-4 accent-emerald-400"
          />
          <div className="flex flex-col leading-tight">
            <span className="text-sm font-semibold text-slate-100">
              Fixed timeslot
            </span>
            <span className="text-[11px] text-slate-500">
              Standups, meetings – must stay at their scheduled time.
            </span>
          </div>
        </label>

        {/* Date fields – adapt based on fixed vs flexible */}
        {form.isFixed ? (
          <div className="grid grid-cols-2 gap-3">
            <div className="flex flex-col gap-1">
              <label className="text-xs tracking-[0.14em] text-slate-500 uppercase">
                Start <span className="text-rose-400">*</span>
              </label>
              <input
                type="datetime-local"
                value={form.startDate}
                onChange={(e) =>
                  setForm((f) => ({ ...f, startDate: e.target.value }))
                }
                className={fieldClass}
              />
            </div>
            <div className="flex flex-col gap-1">
              <label className="text-xs tracking-[0.14em] text-slate-500 uppercase">
                End <span className="text-rose-400">*</span>
              </label>
              <input
                type="datetime-local"
                value={form.deadline}
                onChange={(e) =>
                  setForm((f) => ({ ...f, deadline: e.target.value }))
                }
                className={fieldClass}
              />
            </div>
          </div>
        ) : (
          <div className="grid grid-cols-2 gap-3">
            <div className="flex flex-col gap-1">
              <label className="text-xs tracking-[0.14em] text-slate-500 uppercase">
                Duration (min) <span className="text-rose-400">*</span>
              </label>
              <input
                type="number"
                min={1}
                value={form.durationMinutes}
                onChange={(e) =>
                  setForm((f) => ({
                    ...f,
                    durationMinutes: Number(e.target.value),
                  }))
                }
                className={fieldClass}
              />
            </div>
            <div className="flex flex-col gap-1">
              <label className="text-xs tracking-[0.14em] text-slate-500 uppercase">
                Deadline <span className="text-rose-400">*</span>
              </label>
              <input
                type="datetime-local"
                value={form.deadline}
                onChange={(e) =>
                  setForm((f) => ({ ...f, deadline: e.target.value }))
                }
                className={fieldClass}
              />
            </div>
          </div>
        )}

        {/* Priority + Status + Intensity */}
        <div className="grid grid-cols-2 gap-3">
          <div className="flex flex-col gap-1">
            <label className="text-xs tracking-[0.14em] text-slate-500 uppercase">
              Priority
            </label>
            <select
              value={form.priority}
              onChange={(e) =>
                setForm((f) => ({
                  ...f,
                  priority: e.target.value as NonNullable<Task["priority"]>,
                }))
              }
              className={fieldClass}
            >
              <option value="low">Low</option>
              <option value="medium">Medium</option>
              <option value="high">High</option>
            </select>
          </div>
          <div className="flex flex-col gap-1">
            <label className="text-xs tracking-[0.14em] text-slate-500 uppercase">
              Status
            </label>
            <select
              value={form.status}
              onChange={(e) =>
                setForm((f) => ({
                  ...f,
                  status: e.target.value as NonNullable<Task["status"]>,
                }))
              }
              className={fieldClass}
            >
              <option value="todo">To Do</option>
              <option value="in-progress">In Progress</option>
            </select>
          </div>
        </div>
        <div className="flex flex-col gap-1">
          <label className="text-xs tracking-[0.14em] text-slate-500 uppercase">
            Intensity
          </label>
          <div className="grid grid-cols-3 gap-2">
            {(["light", "normal", "intensive"] as const).map((lvl) => (
              <button
                key={lvl}
                type="button"
                onClick={() => setForm((f) => ({ ...f, intensity: lvl }))}
                className={`rounded-xl border px-3 py-2 text-sm font-medium transition ${
                  form.intensity === lvl
                    ? lvl === "light"
                      ? "border-sky-400/50 bg-sky-400/15 text-sky-200"
                      : lvl === "normal"
                        ? "border-emerald-400/50 bg-emerald-400/15 text-emerald-200"
                        : "border-rose-400/50 bg-rose-400/15 text-rose-200"
                    : "border-slate-800 bg-slate-800/40 text-slate-400 hover:border-slate-600 hover:text-slate-200"
                }`}
              >
                {lvl === "light" ? "🌤 Light" : lvl === "normal" ? "⚡ Normal" : "🔥 Intensive"}
              </button>
            ))}
          </div>
        </div>

        {/* Dependencies */}
        {dependencyOptions.length > 0 && (
          <div className="flex flex-col gap-1">
            <label className="text-xs tracking-[0.14em] text-slate-500 uppercase">
              Dependencies
            </label>
            <div className="flex max-h-36 flex-col gap-2 overflow-y-auto rounded-xl border border-slate-800 bg-slate-800/40 p-3">
              {dependencyOptions.map((dep) => (
                <label
                  key={dep.name}
                  className="flex cursor-pointer items-center gap-2 text-sm text-slate-200"
                >
                  <input
                    type="checkbox"
                    checked={form.dependencies.includes(dep.name)}
                    onChange={(e) => {
                      const next = e.target.checked
                        ? [...form.dependencies, dep.name]
                        : form.dependencies.filter((n) => n !== dep.name);
                      setForm((f) => ({ ...f, dependencies: next }));
                    }}
                    className="accent-emerald-400"
                  />
                  <span>{dep.name}</span>
                </label>
              ))}
            </div>
          </div>
        )}

        {/* Error */}
        {error && (
          <div className="rounded-xl border border-rose-500/30 bg-rose-500/10 px-3 py-2 text-sm text-rose-300">
            {error}
          </div>
        )}

        {/* Actions */}
        <div className="flex gap-3 pt-1">
          <button
            type="button"
            onClick={onClose}
            className="flex-1 rounded-xl border border-slate-700 py-2 text-sm text-slate-300 transition hover:bg-slate-800"
          >
            Cancel
          </button>
          <button
            type="button"
            onClick={submit}
            disabled={saving}
            className="flex-1 rounded-xl border border-emerald-300/60 bg-emerald-400/15 py-2 text-sm font-semibold text-emerald-100 shadow-sm transition hover:bg-emerald-400/25 disabled:opacity-50"
          >
            {saving ? "Saving…" : "Create Task"}
          </button>
        </div>
      </div>
    </div>,
    document.body,
  );
};

// ── Task Board ────────────────────────────────────────────────────────────────

const TaskBoard: FC = () => {
  const { user } = useUserStore();

  const [selectedTask, setSelectedTask] = useState<Task | null>(null);
  const [createOpen, setCreateOpen] = useState(false);
  const [searchQuery, setSearchQuery] = useState("");
  const [statusFilter, setStatusFilter] = useState<StatusFilter>("active");
  const [page, setPage] = useState(1);
  const [priorityFilter, setPriorityFilter] = useState<
    NonNullable<Task["priority"]> | "all"
  >("all");
  const [orgFilter, setOrgFilter] = useState<string | "all">("all");
  const [sortCriteria, setSortCriteria] = useState<SortCriteria>("timeSlot");
  const [sortDirection, setSortDirection] = useState<SortDirection>("asc");
  const [showFilters, setShowFilters] = useState(false);

  const hasActiveFilters =
    priorityFilter !== "all" || orgFilter !== "all" || searchQuery !== "";

  const toggleSort = (criteria: SortCriteria) => {
    if (sortCriteria === criteria) {
      setSortDirection((d) => (d === "asc" ? "desc" : "asc"));
    } else {
      setSortCriteria(criteria);
      setSortDirection("asc");
    }
  };

  const resetFilters = () => {
    setPriorityFilter("all");
    setOrgFilter("all");
    setSearchQuery("");
    setPage(1);
  };

  const filtered = useMemo(() => {
    const q = searchQuery.toLowerCase();
    return (user.tasks ?? []).filter((t) => {
      if (
        q &&
        !t.name.toLowerCase().includes(q) &&
        !t.description?.toLowerCase().includes(q)
      )
        return false;
      if (statusFilter === "active") {
        if (t.status === "done") return false;
      } else if (statusFilter !== "done" && (t.status ?? "todo") !== statusFilter) {
        return false;
      } else if (statusFilter === "done" && t.status !== "done") {
        return false;
      }
      if (priorityFilter !== "all" && t.priority !== priorityFilter)
        return false;
      if (orgFilter !== "all" && t.org !== orgFilter) return false;
      return true;
    });
  }, [user.tasks, searchQuery, statusFilter, priorityFilter, orgFilter]);

  const sorted = useMemo(() => {
    const dir = sortDirection === "asc" ? 1 : -1;
    return [...filtered].sort((a, b) => {
      switch (sortCriteria) {
        case "timeSlot":
          return (
            (new Date(a.startDate).getTime() -
              new Date(b.startDate).getTime()) *
            dir
          );
        case "deadline":
          return (
            ((a.deadline ? new Date(a.deadline).getTime() : Infinity) -
              (b.deadline ? new Date(b.deadline).getTime() : Infinity)) *
            dir
          );
        case "priority":
          return (
            ((PRIORITY_ORDER[a.priority ?? "medium"] ?? 2) -
              (PRIORITY_ORDER[b.priority ?? "medium"] ?? 2)) *
            dir
          );
        case "name":
          return a.name.localeCompare(b.name) * dir;
        default:
          return 0;
      }
    });
  }, [filtered, sortCriteria, sortDirection]);

  const statusCounts = useMemo(() => {
    const tasks = user.tasks ?? [];
    const active = tasks.filter((t) => t.status !== "done");
    return {
      active: active.length,
      todo: tasks.filter((t) => (t.status ?? "todo") === "todo").length,
      "in-progress": tasks.filter((t) => t.status === "in-progress").length,
      done: tasks.filter((t) => t.status === "done").length,
    };
  }, [user.tasks]);

  const totalTasks = user.tasks?.length ?? 0;
  const visibleTasks = sorted.slice(0, page * PAGE_SIZE);
  const hasMore = sorted.length > visibleTasks.length;

  return (
    <div className="flex min-h-full w-full flex-col gap-5 p-6 text-slate-50">
      {/* Page header */}
      <div className="flex items-start justify-between gap-4">
        <div className="flex flex-col gap-1">
          <span className="text-xs tracking-[0.28em] text-emerald-300 uppercase">
            Tasks
          </span>
          <h1 className="text-4xl leading-tight font-semibold">Task List</h1>
          <span className="text-sm text-slate-400">
            {sorted.length === totalTasks
              ? `${totalTasks} task${totalTasks !== 1 ? "s" : ""}`
              : `${sorted.length} of ${totalTasks} tasks`}
          </span>
        </div>
        <button
          onClick={() => setCreateOpen(true)}
          className="mt-1 flex shrink-0 items-center gap-2 rounded-2xl border border-emerald-300/60 bg-emerald-400/15 px-5 py-2.5 text-sm font-semibold text-emerald-100 shadow-sm transition hover:bg-emerald-400/25 active:scale-95"
        >
          <span className="text-lg leading-none">+</span>
          New Task
        </button>
      </div>

      {/* Search bar + filter toggle */}
      <div className="flex gap-3">
        <div className="relative flex-1">
          <span className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-sm text-slate-500">
            🔍
          </span>
          <input
            type="search"
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            placeholder="Search tasks…"
            className="w-full rounded-2xl border border-slate-800 bg-slate-900/80 py-2.5 pl-9 pr-4 text-sm text-slate-50 ring-emerald-400/40 outline-none transition focus:border-emerald-400/60 focus:ring"
          />
        </div>
        <button
          onClick={() => setShowFilters((v) => !v)}
          className={`flex shrink-0 items-center gap-2 rounded-2xl border px-4 py-2 text-sm font-medium transition ${
            showFilters || hasActiveFilters
              ? "border-emerald-400/40 bg-emerald-400/10 text-emerald-200"
              : "border-slate-800 bg-slate-900/80 text-slate-300 hover:border-slate-600"
          }`}
        >
          <span>⚙</span>
          <span>Filters</span>
          {hasActiveFilters && (
            <span className="size-2 rounded-full bg-emerald-400" />
          )}
        </button>
      </div>

      {/* Collapsible filter panel */}
      {showFilters && (
        <div className="grid grid-cols-2 gap-3 rounded-2xl border border-slate-800 bg-slate-900/60 p-4 sm:grid-cols-3">
          <div className="flex flex-col gap-1">
            <label className="text-xs tracking-[0.14em] text-slate-500 uppercase">
              Priority
            </label>
            <select
              value={priorityFilter}
              onChange={(e) =>
                setPriorityFilter(e.target.value as typeof priorityFilter)
              }
              className="rounded-xl border border-slate-800 bg-slate-900/80 px-3 py-2 text-sm text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
            >
              <option value="all">All priorities</option>
              <option value="high">High</option>
              <option value="medium">Medium</option>
              <option value="low">Low</option>
            </select>
          </div>
          <div className="flex flex-col gap-1">
            <label className="text-xs tracking-[0.14em] text-slate-500 uppercase">
              Organization
            </label>
            <select
              value={orgFilter}
              onChange={(e) => setOrgFilter(e.target.value)}
              className="rounded-xl border border-slate-800 bg-slate-900/80 px-3 py-2 text-sm text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
            >
              <option value="all">All orgs</option>
              {user.orgs.map((org) => (
                <option key={org.id} value={org.id}>
                  {org.name}
                </option>
              ))}
            </select>
          </div>
          <div className="col-span-2 flex items-end sm:col-span-1">
            <button
              onClick={resetFilters}
              className="w-full rounded-xl border border-rose-300/40 bg-rose-500/10 py-2 text-sm font-medium text-rose-200 transition hover:bg-rose-500/20"
            >
              Reset filters
            </button>
          </div>
        </div>
      )}

      {/* Status tabs */}
      <div className="flex gap-1 overflow-x-auto rounded-2xl border border-slate-800 bg-slate-900/60 p-1">
        {(["active", "todo", "in-progress", "done"] as StatusFilter[]).map((s) => (
          <button
            key={s}
            onClick={() => { setStatusFilter(s); setPage(1); }}
            className={`flex min-w-max flex-1 items-center justify-center gap-1.5 rounded-xl px-3 py-2 text-sm font-medium transition ${
              statusFilter === s
                ? s === "done"
                  ? "bg-slate-600/40 text-slate-300 shadow-sm"
                  : "bg-emerald-400/20 text-emerald-200 shadow-sm"
                : "text-slate-400 hover:text-slate-200"
            }`}
          >
            {s === "active" ? "Active" : STATUS_LABEL[s]}
            <span
              className={`rounded-md px-1.5 py-0.5 text-xs ${
                statusFilter === s
                  ? s === "done"
                    ? "bg-slate-600/40 text-slate-400"
                    : "bg-emerald-400/20 text-emerald-300"
                  : "bg-slate-800 text-slate-500"
              }`}
            >
              {statusCounts[s]}
            </span>
          </button>
        ))}
      </div>

      {/* Sort toolbar */}
      <div className="flex flex-wrap items-center gap-2">
        <span className="text-xs text-slate-500">Sort by:</span>
        {(["timeSlot", "deadline", "priority", "name"] as SortCriteria[]).map(
          (c) => (
            <button
              key={c}
              onClick={() => toggleSort(c)}
              className={`flex items-center gap-1 rounded-xl border px-3 py-1.5 text-xs font-medium transition ${
                sortCriteria === c
                  ? "border-emerald-400/40 bg-emerald-400/10 text-emerald-200"
                  : "border-slate-800 bg-slate-900/60 text-slate-400 hover:border-slate-600 hover:text-slate-200"
              }`}
            >
              {SORT_LABEL[c]}
              {sortCriteria === c ? (
                <span>{sortDirection === "asc" ? "↑" : "↓"}</span>
              ) : (
                <span className="opacity-30">↕</span>
              )}
            </button>
          ),
        )}
      </div>

      {/* Task list */}
      {sorted.length === 0 ? (
        <div className="flex flex-col items-center justify-center gap-4 rounded-3xl border border-dashed border-slate-800 py-16 text-center">
          <div className="text-4xl opacity-30">📋</div>
          <div className="text-slate-400">
            {totalTasks === 0 ? (
              <>
                No tasks yet.
                <br />
                Create your first task to get started.
              </>
            ) : (
              "No tasks match your current filters."
            )}
          </div>
          {totalTasks === 0 && (
            <button
              onClick={() => setCreateOpen(true)}
              className="rounded-2xl border border-emerald-300/60 bg-emerald-400/15 px-5 py-2 text-sm font-semibold text-emerald-100 transition hover:bg-emerald-400/25"
            >
              + New Task
            </button>
          )}
          {totalTasks > 0 && hasActiveFilters && (
            <button
              onClick={resetFilters}
              className="rounded-2xl border border-slate-700 px-4 py-1.5 text-sm text-slate-300 transition hover:bg-slate-800"
            >
              Clear filters
            </button>
          )}
        </div>
      ) : (
        <div className="flex flex-col gap-3">
          {visibleTasks.map((task, i) => {
            const now = dayjs();
            const isOverdue =
              task.deadline &&
              dayjs(task.deadline).isBefore(now) &&
              task.status !== "done";
            const isDueToday =
              !isOverdue &&
              task.deadline &&
              dayjs(task.deadline).isSame(now, "day") &&
              task.status !== "done";

            return (
              <div
                key={task.id ?? `${task.name}-${i}`}
                onClick={() => setSelectedTask(task)}
                className={`group cursor-pointer rounded-2xl border bg-slate-900/70 p-4 shadow-sm transition hover:shadow-md hover:bg-slate-800/80 ${
                  isOverdue
                    ? "border-rose-500/30 hover:border-rose-400/50"
                    : isDueToday
                      ? "border-amber-500/30 hover:border-amber-400/50"
                      : "border-slate-800 hover:border-emerald-400/30"
                }`}
              >
                <div className="flex items-start justify-between gap-3">
                  {/* Left: date, title, description */}
                  <div className="flex min-w-0 flex-col gap-1">
                    <div className="flex flex-wrap items-center gap-x-2 gap-y-0.5">
                      {isOverdue && (
                        <span className="text-[10px] font-semibold tracking-wider text-rose-400 uppercase">
                          Overdue
                        </span>
                      )}
                      {isDueToday && (
                        <span className="text-[10px] font-semibold tracking-wider text-amber-400 uppercase">
                          Due Today
                        </span>
                      )}
                      <span className="text-xs text-slate-500">
                        {dayjs(task.startDate).format("ddd, DD MMM")}
                      </span>
                      <span className="text-xs text-slate-600">·</span>
                      <span className="text-xs text-slate-500">
                        {dayjs(task.startDate).format("HH:mm")} –{" "}
                        {dayjs(task.endDate).format("HH:mm")}
                      </span>
                    </div>
                    <div className="truncate text-base font-semibold text-slate-50 transition group-hover:text-emerald-100">
                      {task.name}
                    </div>
                    {task.description && (
                      <div className="line-clamp-2 text-sm text-slate-400">
                        {task.description}
                      </div>
                    )}
                    {task.deadline && (
                      <div
                        className={`mt-1 text-xs ${
                          isOverdue
                            ? "text-rose-400"
                            : isDueToday
                              ? "text-amber-400"
                              : "text-slate-500"
                        }`}
                      >
                        Deadline: {dayjs(task.deadline).format("DD MMM, HH:mm")}
                      </div>
                    )}
                  </div>

                  {/* Right: badges */}
                  <div className="flex shrink-0 flex-col items-end gap-1.5">
                    {task.priority && (
                      <span
                        className={`rounded-lg px-2 py-0.5 text-xs font-medium ${PRIORITY_BADGE[task.priority]}`}
                      >
                        {task.priority.charAt(0).toUpperCase() +
                          task.priority.slice(1)}
                      </span>
                    )}
                    <span
                      className={`rounded-lg px-2 py-0.5 text-xs font-medium ${STATUS_BADGE[task.status ?? "todo"]}`}
                    >
                      {STATUS_LABEL[task.status ?? "todo"]}
                    </span>
                    {task.isFixed && (
                      <span className="rounded-lg border border-violet-500/30 bg-violet-500/10 px-2 py-0.5 text-xs font-medium text-violet-300">
                        Fixed
                      </span>
                    )}
                  </div>
                </div>
              </div>
            );
          })}
          {hasMore && (
            <button
              onClick={() => setPage((p) => p + 1)}
              className="mt-2 w-full rounded-2xl border border-slate-800 bg-slate-900/60 py-3 text-sm text-slate-400 transition hover:border-slate-600 hover:text-slate-200"
            >
              Load more ({sorted.length - visibleTasks.length} remaining)
            </button>
          )}
        </div>
      )}

      {createOpen && (
        <CreateTaskModal onClose={() => setCreateOpen(false)} />
      )}
      {selectedTask && (
        <EditTaskModal
          task={selectedTask}
          onClose={() => setSelectedTask(null)}
        />
      )}
    </div>
  );
};

export default TaskBoard;
