import { useEffect, useRef, useState, useMemo, type FC } from "react";
import { createPortal } from "react-dom";
import dayjs from "dayjs";
import type { Task } from "../util/types";
import useUserStore from "../stores/user-store";
import { updateTask, deleteTask, fetchTasks } from "../util/task-api";

interface EditTaskModalProps {
  task: Task;
  onClose: () => void;
}

const EditTaskModal: FC<EditTaskModalProps> = ({ task, onClose }) => {
  const { user, setUser, workProfileId } = useUserStore();
  const [form, setForm] = useState({
    name: "",
  description: "",
  durationMinutes: 0,
  priority: "medium" as Task["priority"],
  status: "todo" as Task["status"],
  intensity: "normal" as Task["intensity"],
  deadline: "",
  dependencies: [] as string[],
  isFixed: false,
  startDate: "",
  endDate: "",
  });
  useEffect(() => {
  setForm({
    name: task.name,
    description: task.description,
    durationMinutes: task.timeEstimateMinutes ?? 0,
    priority: (task.priority ?? "medium") as Task["priority"],
    status: (task.status ?? "todo") as Task["status"],
    intensity: (task.intensity ?? "normal") as Task["intensity"],
    deadline: task.deadline
      ? dayjs(task.deadline).format("YYYY-MM-DDTHH:mm")
      : "",
    dependencies: task.dependencies.map((d) => d.id!),
    isFixed: task.isFixed ?? false,
    startDate: dayjs(task.startDate).format("YYYY-MM-DDTHH:mm"),
    endDate: dayjs(task.endDate).format("YYYY-MM-DDTHH:mm"),
  });
}, [task]);
  const [error, setError] = useState<string | undefined>();

  const overlayRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const handleKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") onClose();
    };
    document.addEventListener("keydown", handleKey);
    return () => document.removeEventListener("keydown", handleKey);
  }, [onClose]);

  const dependencyOptions = useMemo(() => user.tasks ?? [], [user.tasks]).filter((t) => t.id !== task.id);


  const submit = async () => {
    setError(undefined);
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
    if (form.durationMinutes > 10000) {
      setError("Duration is too long.");
      return;
    }
    const start = dayjs(form.startDate);
    const end = dayjs(form.endDate);
    if (!start.isValid() || !end.isValid() || end.isBefore(start)) {
      setError("Invalid start/end dates.");
      return;
    }
    const newTask: Task = {
      ...task,
      name: form.name.trim(),
      description: form.description.trim(),
      startDate: start.toDate(),
      endDate: end.toDate(),
      isFixed: form.isFixed,
      priority: form.priority,
      status: form.status,
      deadline: dayjs(form.deadline).toDate(),
      dependencies: dependencyOptions.filter((t) => form.dependencies.includes(t.id!)),
      timeEstimateMinutes: form.durationMinutes,
      intensity: form.intensity,
    };
    try {
      await updateTask(workProfileId!, task.id!, newTask);
      await fetchTasks(workProfileId!).then((fresh) => {
        setUser({ ...user, tasks: fresh });
      });
      onClose();
    } catch (e) {
      setError("Failed to save task.");
    }
  };

  const handleDelete = async () => {
    setError(undefined);
    try {
      await deleteTask(workProfileId!, task.id!);
      const updatedTasks = user.tasks?.filter((t) => t.id !== task.id) ?? [];
      setUser({ ...user, tasks: updatedTasks });
      onClose();
    } catch (e) {
      setError("Failed to delete task.");
    }
  };

  return createPortal(
    <div
      ref={overlayRef}
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50"
    >
      <div
        className="bg-slate-900 rounded-3xl p-6 shadow-xl w-full max-w-lg max-h-[100vh] flex flex-col"
        onClick={(e) => e.stopPropagation()}
      >
        <div className= "p-1">
          <h2 className="text-xl font-semibold text-slate-50 mb-4">Edit Task</h2>
        {error && <div className="text-sm text-rose-300 mb-2">{error}</div>}
        </div>
        <div className="p-4 overflow-y-auto flex-1">
          <div className="grid grid-cols-1 gap-3">
          <label className="text-xs tracking-[0.14em] text-slate-500 uppercase">Title</label>
          <input
            value={form.name}
            onChange={(e) => setForm({ ...form, name: e.target.value })}
            className="rounded-xl border border-slate-800 bg-slate-900/80 px-3 py-2 text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
            placeholder="Task title"
          />
          <label className="text-xs tracking-[0.14em] text-slate-500 uppercase">Description</label>
          <textarea
            value={form.description}
            onChange={(e) => setForm({ ...form, description: e.target.value })}
            className="min-h-22.5 rounded-xl border border-slate-800 bg-slate-900/80 px-3 py-2 text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
            placeholder="What needs to be done"
          />
          <label className="text-xs tracking-[0.14em] text-slate-500 uppercase">Start Date</label>
          <input
            type="datetime-local"
            value={form.startDate}
            onChange={(e) => setForm({ ...form, startDate: e.target.value })}
            className="rounded-xl border border-slate-800 bg-slate-900/80 px-3 py-2 text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
          />
          <label className="text-xs tracking-[0.14em] text-slate-500 uppercase">End Date</label>
          <input
            type="datetime-local"
            value={form.endDate}
            onChange={(e) => setForm({ ...form, endDate: e.target.value })}
            className="rounded-xl border border-slate-800 bg-slate-900/80 px-3 py-2 text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
          />
          <label className="text-xs tracking-[0.14em] text-slate-500 uppercase">Priority</label>
          <select
            value={form.priority}
            onChange={(e) => setForm({ ...form, priority: e.target.value as Task["priority"] })}
            className="rounded-xl border border-slate-800 bg-slate-900/80 px-3 py-2 text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
          >
            <option value="low">Low</option>
            <option value="medium">Medium</option>
            <option value="high">High</option>
          </select>
          <label className="text-xs tracking-[0.14em] text-slate-500 uppercase">Intensity</label>
          <select
            value={form.intensity}
            onChange={(e) => setForm({ ...form, intensity: e.target.value as Task["intensity"] })}
            className="rounded-xl border border-slate-800 bg-slate-900/80 px-3 py-2 text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
          >
            <option value="light">Light</option>
            <option value="normal">Normal</option>
            <option value="intensive">Intensive</option>
          </select>
          <label className="text-xs tracking-[0.14em] text-slate-500 uppercase">Status</label>
          <select
            value={form.status}
            onChange={(e) => setForm({ ...form, status: e.target.value as Task["status"] })}
            className="rounded-xl border border-slate-800 bg-slate-900/80 px-3 py-2 text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
          >
            <option value="todo">To Do</option>
            <option value="in-progress">In Progress</option>
            <option value="done">Done</option>
          </select>
          <label className="text-xs tracking-[0.14em] text-slate-500 uppercase">Deadline</label>
          <input
            type="datetime-local"
            value={form.deadline}
            onChange={(e) => setForm({ ...form, deadline: e.target.value })}
            className="rounded-xl border border-slate-800 bg-slate-900/80 px-3 py-2 text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
          />
          <label className="text-xs tracking-[0.14em] text-slate-500 uppercase">Duration</label>
          <input
            type="number"
            value={form.durationMinutes}
            onChange={(e) => setForm({ ...form, durationMinutes: parseInt(e.target.value) })}
            className="rounded-xl border border-slate-800 bg-slate-900/80 px-3 py-2 text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
          />
          <label className="text-xs tracking-[0.14em] text-slate-500 uppercase">Dependencies</label>
          <div className="flex max-h-44 flex-col gap-2 overflow-y-auto rounded-xl border border-slate-800 bg-slate-900/60 p-3">
            {dependencyOptions.map((dep) => {
              const checked = form.dependencies.includes(dep.id!);
              return (
                <label key={dep.id} className="flex items-center gap-2 text-sm text-slate-200">
                  <input
                    type="checkbox"
                    checked={checked}
                    onChange={(e) => {
                      const next = e.target.checked
                        ? [...form.dependencies, dep.id!]
                        : form.dependencies.filter((n) => n !== dep.id!);
                      setForm({ ...form, dependencies: next });
                    }}
                  />
                  <span>{dep.name}</span>
                </label>
              );
            })}
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
        </div>
        </div>
        <div className="p-1">
            <div className="mt-4 flex justify-end gap-2">
          <button
            onClick={handleDelete}
            className="rounded-xl border border-red-300/60 bg-red-500/10 py-1 font-semibold text-red-100 hover:bg-red-500/20 mr-auto"
          >
            Delete
          </button>
          <button
            onClick={onClose}
            className="rounded-xl border border-rose-300/60 bg-rose-500/10 py-1 font-semibold text-rose-100 hover:bg-rose-500/20"
          >
            Cancel
          </button>
          <button
            onClick={submit}
            className="rounded-xl border border-emerald-300/60 bg-emerald-400/15 px-4 py-2 text-sm font-semibold text-emerald-100 shadow-sm transition hover:bg-emerald-400/25"
          >
            Save
          </button>
        </div>
        </div>
      </div>
    </div>,
    document.body,
  );
};

export default EditTaskModal;
