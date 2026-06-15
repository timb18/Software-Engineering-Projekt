import { createPortal } from "react-dom";
import type { Task } from "../util/types";
import dayjs from "dayjs";
import { useAuth0 } from "@auth0/auth0-react";
import {
  type FC,
  useRef,
  useState,
  useCallback,
  useEffect,
  useMemo,
} from "react";
import useUserStore from "../stores/user-store";
import type { RecurringBlocker } from "./main-panel/task-list";

const API_BASE = import.meta.env.VITE_API_BASE_URL ?? "";

const WEEKDAYS = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"] as const;
const WEEKDAY_LABELS: Record<string, string> = {
  Mon: "Mo",
  Tue: "Di",
  Wed: "Mi",
  Thu: "Do",
  Fri: "Fr",
  Sat: "Sa",
  Sun: "So",
};

const TEMPLATES = [
  { label: "Daily standup", minutes: 15, fixed: true },
  { label: "Weekly review", minutes: 60, fixed: true },
  { label: "Focus block", minutes: 90, fixed: false },
  { label: "1:1 Meeting", minutes: 45, fixed: true },
];

const emptyBlockerForm = (): RecurringBlocker => ({
  name: "",
  daysOfWeek: "Mon,Tue,Wed,Thu,Fri",
  startTime: "09:00",
  endTime: "10:00",
  validFrom: "",
  validUntil: "",
});

interface CreateTaskModalProps {
  onClose: () => void;
  /** Prefill form values (e.g. from calendar drag selection) */
  initialValues?: {
    startDate?: string;
    endDate?: string;
    isFixed?: boolean;
  };
  /** Work-profile ID – enables the Recurring Blocker tab */
  workProfileId?: string;
}

export const CreateTaskModal: FC<CreateTaskModalProps> = ({
  onClose,
  initialValues,
  workProfileId,
}) => {
  const { getAccessTokenSilently } = useAuth0();
  const { user, addTask, activeOrganizationId } = useUserStore();
  const overlayRef = useRef<HTMLDivElement>(null);

  // Organization selector: lists ALL orgs the user belongs to.
  // The work profile is shared across orgs (per-shift Company tagging),
  // so we only need to tag the task with the chosen org id.
  const availableOrgs = user.orgs ?? [];
  const [selectedOrgId, setSelectedOrgId] = useState<string>(
    activeOrganizationId ?? availableOrgs[0]?.id ?? "",
  );

  // ── Modal mode ────────────────────────────────────────────────────────────
  const [modalMode, setModalMode] = useState<"task" | "blocker">("task");

  // ── Task form ─────────────────────────────────────────────────────────────
  const [form, setForm] = useState({
    name: "",
    description: "",
    durationMinutes: 60,
    priority: "medium" as NonNullable<Task["priority"]>,
    status: "todo" as NonNullable<Task["status"]>,
    intensity: "normal" as NonNullable<Task["intensity"]>,
    deadline: initialValues?.endDate ?? "",
    startDate: initialValues?.startDate ?? "",
    isFixed: initialValues?.isFixed ?? false,
    dependencies: [] as string[],
  });
  const [error, setError] = useState<string>();
  const [saving, setSaving] = useState(false);

  // ── Recurring blocker state ───────────────────────────────────────────────
  const [blockers, setBlockers] = useState<RecurringBlocker[]>([]);
  const [blockerForm, setBlockerForm] = useState<RecurringBlocker>(() => {
    const base = emptyBlockerForm();
    // Pre-fill from calendar drag selection (datetime-local → split into date + time)
    if (initialValues?.startDate) {
      base.startTime = initialValues.startDate.slice(11, 16); // "HH:mm"
      base.validFrom = initialValues.startDate.slice(0, 10); // "YYYY-MM-DD"
    }
    if (initialValues?.endDate) {
      base.endTime = initialValues.endDate.slice(11, 16);
    }
    return base;
  });
  const [editingBlockerId, setEditingBlockerId] = useState<string | null>(null);
  const [blockerError, setBlockerError] = useState<string | undefined>();
  const [blockerStatus, setBlockerStatus] = useState<string | undefined>();
  const [isSavingBlocker, setIsSavingBlocker] = useState(false);

  const fetchBlockersList = useCallback(async () => {
    if (!workProfileId) return;
    try {
      const token = await getAccessTokenSilently();
      const res = await fetch(
        `${API_BASE}/api/recurring-blocker/${workProfileId}`,
        { headers: { Authorization: `Bearer ${token}` } },
      );
      if (res.ok) setBlockers((await res.json()) as RecurringBlocker[]);
    } catch {
      /* ignore */
    }
  }, [getAccessTokenSilently, workProfileId]);

  if (modalMode === "blocker") fetchBlockersList();

  const toggleBlockerDay = (day: string) => {
    const current = blockerForm.daysOfWeek.split(",").filter(Boolean);
    const next = current.includes(day)
      ? current.filter((d) => d !== day)
      : [...current, day];
    setBlockerForm({ ...blockerForm, daysOfWeek: next.join(",") });
  };

  const startEditBlocker = (b: RecurringBlocker) => {
    setEditingBlockerId(b.id ?? null);
    setBlockerForm({
      name: b.name,
      daysOfWeek: b.daysOfWeek,
      startTime: b.startTime,
      endTime: b.endTime,
      validFrom: b.validFrom ?? "",
      validUntil: b.validUntil ?? "",
    });
    setBlockerError(undefined);
    setBlockerStatus(undefined);
  };

  const cancelBlockerEdit = () => {
    setEditingBlockerId(null);
    setBlockerForm(emptyBlockerForm());
    setBlockerError(undefined);
    setBlockerStatus(undefined);
  };

  const saveBlocker = async () => {
    if (!workProfileId) return;
    setIsSavingBlocker(true);
    setBlockerError(undefined);
    setBlockerStatus(undefined);
    try {
      const body = {
        ...blockerForm,
        validFrom: blockerForm.validFrom || undefined,
        validUntil: blockerForm.validUntil || undefined,
      };
      const url = editingBlockerId
        ? `${API_BASE}/api/recurring-blocker/${workProfileId}/${editingBlockerId}`
        : `${API_BASE}/api/recurring-blocker/${workProfileId}`;
      const token = await getAccessTokenSilently();
      const res = await fetch(url, {
        method: editingBlockerId ? "PUT" : "POST",
        headers: {
          "Content-Type": "application/json",
          Authorization: `Bearer ${token}`,
        },
        body: JSON.stringify(body),
      });
      if (!res.ok)
        throw new Error((await res.text()) || "Could not save blocker.");
      setBlockerStatus(
        editingBlockerId ? "Blocker updated." : "Blocker created.",
      );
      cancelBlockerEdit();
      await fetchBlockersList();
    } catch (e) {
      setBlockerError(e instanceof Error ? e.message : "Unknown error.");
    } finally {
      setIsSavingBlocker(false);
    }
  };

  const deleteBlockerById = async (id: string) => {
    if (!workProfileId || !window.confirm("Blocker löschen?")) return;
    try {
      const token = await getAccessTokenSilently();
      await fetch(`${API_BASE}/api/recurring-blocker/${workProfileId}/${id}`, {
        method: "DELETE",
        headers: { Authorization: `Bearer ${token}` },
      });
      await fetchBlockersList();
    } catch (e) {
      setBlockerError(e instanceof Error ? e.message : "Could not delete.");
    }
  };

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
      setError(
        form.isFixed ? "End time is required." : "Deadline is required.",
      );
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
    if (form.durationMinutes > 10000) {
      setError("Duration is too long.");
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
      org: selectedOrgId || user.orgs?.[0]?.id || "",
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

  /** Clamp year to 4 digits – called onBlur, not during typing. */
  const clampYear = (val: string): string => {
    if (!val) return val;
    return val.replace(/^(\d{5,})/, (y) => y.slice(0, 4));
  };

  return createPortal(
    <div
      ref={overlayRef}
      className="fixed inset-0 z-50 flex items-end justify-center bg-black/60 backdrop-blur-sm sm:items-center"
      onClick={() => {}}
    >
      <div
        data-modal-backdrop="static"
        className="flex max-h-[90dvh] w-full max-w-2xl flex-col gap-4 overflow-y-auto rounded-t-3xl border border-slate-700 bg-slate-900 p-6 shadow-2xl sm:rounded-3xl"
      >
        {/* Header with mode toggle */}
        <div className="flex items-center justify-between">
          <div className="flex gap-1 rounded-full border border-slate-700 bg-slate-950/60 p-1">
            <button
              type="button"
              onClick={() => {
                setModalMode("task");
                setBlockerError(undefined);
                setBlockerStatus(undefined);
              }}
              className={`rounded-full px-4 py-1.5 text-xs font-semibold transition ${
                modalMode === "task"
                  ? "border border-emerald-300/50 bg-emerald-400/20 text-emerald-100"
                  : "text-slate-400 hover:text-slate-200"
              }`}
            >
              📅 Task
            </button>
            {workProfileId && (
              <button
                type="button"
                onClick={() => {
                  setModalMode("blocker");
                  void fetchBlockersList();
                }}
                className={`rounded-full px-4 py-1.5 text-xs font-semibold transition ${
                  modalMode === "blocker"
                    ? "border border-violet-300/50 bg-violet-400/20 text-violet-100"
                    : "text-slate-400 hover:text-slate-200"
                }`}
              >
                🔁 Recurring Blocker
              </button>
            )}
          </div>
          <button
            onClick={onClose}
            className="rounded-xl p-1.5 text-slate-400 transition hover:bg-slate-800 hover:text-slate-200"
            aria-label="Close dialog"
          >
            ✕
          </button>
        </div>

        {/* ─── BLOCKER MODE ─── */}
        {modalMode === "blocker" && (
          <div className="flex flex-col gap-5">
            <p className="text-xs text-slate-400">
              Wiederkehrende Blocker werden beim Auto-Schedule automatisch aus
              den freien Slots herausgerechnet.
            </p>

            {/* Form */}
            <div className="flex flex-col gap-4 rounded-2xl border border-violet-400/20 bg-violet-500/5 p-4">
              <div className="text-xs font-semibold tracking-wide text-violet-200">
                {editingBlockerId ? "Blocker bearbeiten" : "Neuer Blocker"}
              </div>
              <div className="flex flex-col gap-1">
                <label className="text-xs tracking-[0.14em] text-slate-500 uppercase">
                  Name
                </label>
                <input
                  value={blockerForm.name}
                  onChange={(e) =>
                    setBlockerForm({ ...blockerForm, name: e.target.value })
                  }
                  placeholder="z.B. Team-Standup, Mittagspause…"
                  className="rounded-xl border border-slate-800 bg-slate-800/60 px-3 py-2 text-sm text-slate-50 ring-violet-400/40 outline-none focus:border-violet-400/60 focus:ring"
                />
              </div>
              <div className="flex flex-col gap-1">
                <label className="text-xs tracking-[0.14em] text-slate-500 uppercase">
                  Wochentage
                </label>
                <div className="flex flex-wrap gap-1.5">
                  {WEEKDAYS.map((d) => {
                    const active = blockerForm.daysOfWeek
                      .split(",")
                      .includes(d);
                    return (
                      <button
                        key={d}
                        type="button"
                        onClick={() => toggleBlockerDay(d)}
                        className={`rounded-lg px-3 py-1.5 text-xs font-semibold transition ${
                          active
                            ? "border border-violet-400/60 bg-violet-500/25 text-violet-100"
                            : "border border-slate-700 bg-slate-800 text-slate-400 hover:border-slate-500 hover:text-slate-200"
                        }`}
                      >
                        {WEEKDAY_LABELS[d]}
                      </button>
                    );
                  })}
                </div>
              </div>
              <div className="grid grid-cols-2 gap-3">
                <div className="flex flex-col gap-1">
                  <label className="text-xs tracking-[0.14em] text-slate-500 uppercase">
                    Startzeit
                  </label>
                  <input
                    type="time"
                    value={blockerForm.startTime}
                    onChange={(e) =>
                      setBlockerForm({
                        ...blockerForm,
                        startTime: e.target.value,
                      })
                    }
                    className="rounded-xl border border-slate-800 bg-slate-800/60 px-3 py-2 text-sm text-slate-50 ring-violet-400/40 outline-none focus:border-violet-400/60 focus:ring"
                  />
                </div>
                <div className="flex flex-col gap-1">
                  <label className="text-xs tracking-[0.14em] text-slate-500 uppercase">
                    Endzeit
                  </label>
                  <input
                    type="time"
                    value={blockerForm.endTime}
                    onChange={(e) =>
                      setBlockerForm({
                        ...blockerForm,
                        endTime: e.target.value,
                      })
                    }
                    className="rounded-xl border border-slate-800 bg-slate-800/60 px-3 py-2 text-sm text-slate-50 ring-violet-400/40 outline-none focus:border-violet-400/60 focus:ring"
                  />
                </div>
                <div className="flex flex-col gap-1">
                  <label className="text-xs tracking-[0.14em] text-slate-500 uppercase">
                    Gültig ab (optional)
                  </label>
                  <input
                    type="date"
                    value={blockerForm.validFrom ?? ""}
                    min="2000-01-01"
                    max="2099-12-31"
                    onChange={(e) =>
                      setBlockerForm({
                        ...blockerForm,
                        validFrom: e.target.value,
                      })
                    }
                    onBlur={(e) =>
                      setBlockerForm({
                        ...blockerForm,
                        validFrom: clampYear(e.target.value),
                      })
                    }
                    className="rounded-xl border border-slate-800 bg-slate-800/60 px-3 py-2 text-sm text-slate-50 ring-violet-400/40 outline-none focus:border-violet-400/60 focus:ring"
                  />
                </div>
                <div className="flex flex-col gap-1">
                  <label className="text-xs tracking-[0.14em] text-slate-500 uppercase">
                    Gültig bis (optional)
                  </label>
                  <input
                    type="date"
                    value={blockerForm.validUntil ?? ""}
                    min="2000-01-01"
                    max="2099-12-31"
                    onChange={(e) =>
                      setBlockerForm({
                        ...blockerForm,
                        validUntil: e.target.value,
                      })
                    }
                    onBlur={(e) =>
                      setBlockerForm({
                        ...blockerForm,
                        validUntil: clampYear(e.target.value),
                      })
                    }
                    className="rounded-xl border border-slate-800 bg-slate-800/60 px-3 py-2 text-sm text-slate-50 ring-violet-400/40 outline-none focus:border-violet-400/60 focus:ring"
                  />
                </div>
              </div>
              {blockerError && (
                <div className="text-xs text-rose-300">{blockerError}</div>
              )}
              {blockerStatus && (
                <div className="text-xs text-emerald-300">{blockerStatus}</div>
              )}
              <div className="flex gap-2">
                <button
                  type="button"
                  onClick={() => void saveBlocker()}
                  disabled={
                    isSavingBlocker ||
                    !blockerForm.name ||
                    !blockerForm.daysOfWeek
                  }
                  className="flex-1 rounded-xl border border-violet-400/60 bg-violet-500/20 py-2 text-sm font-semibold text-violet-100 transition hover:bg-violet-500/30 disabled:opacity-50"
                >
                  {isSavingBlocker
                    ? "Speichern…"
                    : editingBlockerId
                      ? "Aktualisieren"
                      : "Blocker erstellen"}
                </button>
                {editingBlockerId && (
                  <button
                    type="button"
                    onClick={cancelBlockerEdit}
                    className="rounded-xl border border-slate-700 bg-slate-800 px-4 py-2 text-sm font-semibold text-slate-300 transition hover:border-slate-500"
                  >
                    Abbrechen
                  </button>
                )}
              </div>
            </div>

            {/* List */}
            {blockers.length === 0 ? (
              <div className="rounded-2xl border border-dashed border-slate-700 bg-slate-900/40 p-5 text-center text-sm text-slate-500">
                Noch keine wiederkehrenden Blocker.
              </div>
            ) : (
              <div className="flex flex-col gap-2">
                <div className="text-xs tracking-[0.14em] text-slate-500 uppercase">
                  Bestehende Blocker
                </div>
                {blockers.map((b) => (
                  <div
                    key={b.id}
                    className="flex items-center justify-between gap-3 rounded-2xl border border-slate-800 bg-slate-900/70 px-4 py-3"
                  >
                    <div className="min-w-0">
                      <div className="truncate text-sm font-semibold text-slate-100">
                        {b.name}
                      </div>
                      <div className="text-xs text-slate-400">
                        {b.daysOfWeek
                          .split(",")
                          .map((d) => WEEKDAY_LABELS[d] ?? d)
                          .join(", ")}
                        &nbsp;·&nbsp;{b.startTime}–{b.endTime}
                        {(b.validFrom || b.validUntil) && (
                          <span className="ml-2 text-slate-500">
                            ({b.validFrom ?? "…"} – {b.validUntil ?? "…"})
                          </span>
                        )}
                      </div>
                    </div>
                    <div className="flex shrink-0 gap-1.5">
                      <button
                        type="button"
                        onClick={() => startEditBlocker(b)}
                        className="rounded-lg border border-slate-700 bg-slate-800 px-2.5 py-1 text-xs text-slate-300 hover:border-slate-500 hover:text-slate-100"
                      >
                        Edit
                      </button>
                      <button
                        type="button"
                        onClick={() => void deleteBlockerById(b.id!)}
                        className="rounded-lg border border-rose-400/40 bg-rose-500/10 px-2.5 py-1 text-xs text-rose-300 hover:bg-rose-500/20"
                      >
                        Del
                      </button>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        )}

        {/* ─── TASK MODE ─── */}
        {modalMode === "task" && (
          <>
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
                    <div className="text-[11px] text-slate-500">
                      {tpl.minutes} min
                    </div>
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
                onChange={(e) =>
                  setForm((f) => ({ ...f, name: e.target.value }))
                }
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
                    min="2000-01-01T00:00"
                    max="2099-12-31T23:59"
                    onChange={(e) =>
                      setForm((f) => ({ ...f, startDate: e.target.value }))
                    }
                    onBlur={(e) =>
                      setForm((f) => ({
                        ...f,
                        startDate: clampYear(e.target.value),
                      }))
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
                    min="2000-01-01T00:00"
                    max="2099-12-31T23:59"
                    onChange={(e) =>
                      setForm((f) => ({ ...f, deadline: e.target.value }))
                    }
                    onBlur={(e) =>
                      setForm((f) => ({
                        ...f,
                        deadline: clampYear(e.target.value),
                      }))
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
                    min="2000-01-01T00:00"
                    max="2099-12-31T23:59"
                    onChange={(e) =>
                      setForm((f) => ({ ...f, deadline: e.target.value }))
                    }
                    onBlur={(e) =>
                      setForm((f) => ({
                        ...f,
                        deadline: clampYear(e.target.value),
                      }))
                    }
                    className={fieldClass}
                  />
                </div>
              </div>
            )}

            {/* Organization */}
            {availableOrgs.length > 0 && (
              <div className="flex flex-col gap-1">
                <label className="text-xs tracking-[0.14em] text-slate-500 uppercase">
                  Organization
                </label>
                <select
                  value={selectedOrgId}
                  onChange={(e) => setSelectedOrgId(e.target.value)}
                  className={fieldClass}
                >
                  {availableOrgs.map((org) => (
                    <option key={org.id} value={org.id}>
                      {org.name}
                      {org.id === activeOrganizationId ? " (active)" : ""}
                    </option>
                  ))}
                </select>
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
                    {lvl === "light"
                      ? "🌤 Light"
                      : lvl === "normal"
                        ? "⚡ Normal"
                        : "🔥 Intensive"}
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
          </>
        )}
      </div>
    </div>,
    document.body,
  );
};

export default CreateTaskModal;
