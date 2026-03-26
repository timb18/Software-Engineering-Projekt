import dayjs from "dayjs";
import { useEffect, useMemo, useState, type FC } from "react";
import { useNavigate } from "react-router";
import useUserStore from "../../stores/user-store";
import type { Task } from "../../util/types";

const startHour = 7;
const endHour = 19;
const intervalsPerDay = (endHour - startHour) * 4; // 15-minute slots

const getWeekStart = () => dayjs().startOf("week").add(1, "day");

const getStartSlot = (date: Date) => {
  const minutes = dayjs(date).diff(dayjs(date).startOf("day").add(startHour, "hour"), "minute");
  return Math.max(0, Math.min(intervalsPerDay - 1, Math.floor(minutes / 15)));
};

const getEndSlot = (date: Date) => {
  const minutes = dayjs(date).diff(dayjs(date).startOf("day").add(startHour, "hour"), "minute");
  return Math.max(1, Math.min(intervalsPerDay, Math.ceil(minutes / 15)));
};

const Tasks: FC = () => {
  const { user, setUser } = useUserStore();
  const navigate = useNavigate();
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
  const [editingTask, setEditingTask] = useState<Task | null>(null);
  const [editForm, setEditForm] = useState({
    name: "",
    description: "",
    start: "",
    end: "",
    priority: "medium" as Task["priority"],
    status: "todo" as Task["status"],
    assigneeEmail: "",
    isFixed: false,
  });
  const [editError, setEditError] = useState<string | undefined>();
  const [status, setStatus] = useState<string | undefined>();
  const [error, setError] = useState<string | undefined>();
  const [view, setView] = useState<"day" | "week" | "month">("week");
  const [filterStatus, setFilterStatus] = useState<"all" | "todo" | "in-progress" | "done">("all");
  const [filterAssignee, setFilterAssignee] = useState<string | "all">("all");

  useEffect(() => {
    if (!user) {
      navigate("/login");
    }
  }, [navigate, user]);

  if (!user) {
    return <></>;
  }

  const filteredTasks = (user.tasks ?? []).filter((t) => {
    const byStatus = filterStatus === "all" || (t.status ?? "todo") === filterStatus;
    const byAssignee = filterAssignee === "all" || t.assigneeEmail === filterAssignee;
    return byStatus && byAssignee;
  });

  const weekStart = getWeekStart();
  const weekEnd = weekStart.add(7, "day");
  const weekDays = Array.from({ length: 7 }).map((_, i) => weekStart.add(i, "day"));
  const today = dayjs();

  const tasksThisWeek = filteredTasks.filter((task) => {
    const start = dayjs(task.startDate);
    const end = dayjs(task.endDate);
    return start.isBefore(weekEnd) && end.isAfter(weekStart);
  });

  const tasksToday = filteredTasks.filter((task) =>
    dayjs(task.startDate).isSame(today, "day"),
  );

  const monthStart = today.startOf("month");
  const monthEnd = monthStart.endOf("month");
  const monthDays = Array.from({ length: monthEnd.date() }).map((_, i) => monthStart.add(i, "day"));
  const tasksThisMonth = filteredTasks.filter((task) => {
    const start = dayjs(task.startDate);
    const end = dayjs(task.endDate);
    return start.isBefore(monthEnd) && end.isAfter(monthStart);
  });

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

    const dependencies = dependencyOptions.filter((t) =>
      form.dependencies.includes(t.name),
    );

    const selectedAssignee = filterAssignee === "all" ? user.email : filterAssignee;

    const newTask: Task = {
      name: form.name.trim(),
      description: form.description.trim(),
      startDate,
      endDate,
      deadline: endDate,
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
      deadline: "",
      dependencies: [],
      isFixed: false,
    });
    setStatus("Task created");
  };

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
      assigneeEmail: task.assigneeEmail ?? user.email,
      isFixed: !!task.isFixed,
    });
  };

  const saveEdit = () => {
    setEditError(undefined);
    if (!editingTask) return;

    if (!editForm.name.trim()) {
      setEditError("Title is required.");
      return;
    }

    const start = dayjs(editForm.start);
    const end = dayjs(editForm.end);
    if (!start.isValid() || !end.isValid()) {
      setEditError("Start and end must be valid.");
      return;
    }
    if (!end.isAfter(start)) {
      setEditError("End must be after start.");
      return;
    }

    const updatedTasks = (user.tasks ?? []).map((t) =>
      t === editingTask
        ? {
            ...t,
            name: editForm.name.trim(),
            description: editForm.description.trim(),
            startDate: start.toDate(),
            endDate: end.toDate(),
            deadline: end.toDate(),
            priority: editForm.priority,
            status: editForm.status,
            assigneeEmail: editForm.assigneeEmail,
            isFixed: editForm.isFixed,
          }
        : t,
    );

    setUser({
      ...user,
      tasks: updatedTasks,
    });
    setEditingTask(null);
  };

  return (
    <div className="grid h-full w-full grid-rows-[3.5rem_1fr] gap-6 bg-gradient-to-br from-slate-950 via-slate-900 to-slate-950 p-6 text-slate-50">
      <div className="flex items-center justify-between">
        <div className="flex flex-col gap-1">
          <span className="text-xs uppercase tracking-[0.28em] text-emerald-300">Planner</span>
          <h1 className="text-4xl font-semibold leading-tight">Week of {weekStart.format("MMM DD")}</h1>
          <span className="text-sm text-slate-400">
            {weekStart.format("DD MMM")} - {weekEnd.subtract(1, "day").format("DD MMM YYYY")}
          </span>
        </div>
          <div className="flex items-center gap-2 text-sm">
          <button
            onClick={() => setView("day")}
            className={`rounded-full px-4 py-2 font-semibold transition ${
              view === "day"
                ? "border border-emerald-400/60 bg-emerald-400/15 text-emerald-100"
                : "border border-slate-700 bg-slate-900/60 text-slate-300 hover:border-emerald-300/40 hover:text-emerald-100"
            }`}
          >
            Day
          </button>
          <button
            onClick={() => setView("week")}
            className={`rounded-full px-4 py-2 font-semibold transition ${
              view === "week"
                ? "border border-emerald-400/60 bg-emerald-400/15 text-emerald-100"
                : "border border-slate-700 bg-slate-900/60 text-slate-300 hover:border-emerald-300/40 hover:text-emerald-100"
            }`}
          >
            Week
          </button>
          <button
            onClick={() => setView("month")}
            className={`rounded-full px-4 py-2 font-semibold transition ${
              view === "month"
                ? "border border-emerald-400/60 bg-emerald-400/15 text-emerald-100"
                : "border border-slate-700 bg-slate-900/60 text-slate-300 hover:border-emerald-300/40 hover:text-emerald-100"
            }`}
          >
            Month
          </button>
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

      <div className="grid grid-cols-[1.35fr_0.65fr] gap-5 max-lg:grid-cols-1">
        <div className="relative overflow-hidden rounded-3xl border border-slate-800 bg-gradient-to-b from-slate-900/80 to-slate-950/80 shadow-2xl backdrop-blur">
          <div className="flex items-center justify-between px-6 pt-4 pb-3 text-sm text-slate-300">
            <div className="flex items-center gap-2">
              <span className="h-2 w-2 rounded-full bg-emerald-400"></span>
              <span>
                {view === "day" && `${tasksToday.length} today`}
                {view === "week" && `${tasksThisWeek.length} this week`}
                {view === "month" && `${tasksThisMonth.length} this month`}
              </span>
            </div>
            <span className="text-xs text-slate-500">15 min slots, {startHour}:00-{endHour}:00</span>
          </div>

          <div className="relative h-[62vh] overflow-hidden px-2 pb-6">
            {view === "week" && (
              <div className="relative h-full overflow-auto rounded-2xl border border-slate-800/70 bg-slate-900/60">
                <div
                  className="grid min-h-full grid-cols-[5rem_repeat(7,minmax(0,1fr))] text-slate-500"
                  style={{ gridTemplateRows: `2.5rem repeat(${intervalsPerDay}, minmax(0,1fr))` }}
                >
                  {weekDays.map((day, i) => (
                    <div
                      key={day.format("ddd")}
                      style={{ gridColumn: i + 2 }}
                      className="flex flex-col items-center justify-center border-b border-slate-800/80 bg-slate-900/70 text-center text-sm font-semibold text-slate-100"
                    >
                      <div>{day.format("ddd")}</div>
                      <div className="text-xs font-normal text-slate-400">{day.format("DD MMM")}</div>
                    </div>
                  ))}

                  {Array.from({ length: intervalsPerDay }).map((_, i) => {
                    const time = dayjs().startOf("day").add((startHour * 60 + i * 15), "minute");
                    const showLabel = time.minute() === 0;
                    return (
                      <div
                        key={`time-${i}`}
                        style={{ gridRow: i + 2 }}
                        className="flex items-start justify-end border-t border-slate-900/80 pr-3 text-[11px] leading-4 text-slate-500"
                      >
                        {showLabel ? time.format("HH:mm") : ""}
                      </div>
                    );
                  })}

                  {Array.from({ length: 7 }).map((_, day) =>
                    Array.from({ length: intervalsPerDay }).map((_, slot) => (
                      <div
                        key={`${day}-${slot}`}
                        style={{ gridColumn: day + 2, gridRow: slot + 2 }}
                        className="border-t border-l border-slate-900/60"
                      ></div>
                    )),
                  )}
                </div>

                <div
                  className="pointer-events-none absolute inset-0 grid grid-cols-[5rem_repeat(7,minmax(0,1fr))] px-2 pb-6"
                  style={{ gridTemplateRows: `2.5rem repeat(${intervalsPerDay}, minmax(0,1fr))` }}
                >
                  {tasksThisWeek.map((task) => {
                    const start = dayjs(task.startDate);
                    const end = dayjs(task.endDate);
                    const dayIndex = start.startOf("day").diff(weekStart.startOf("day"), "day");

                    if (dayIndex < 0 || dayIndex > 6) {
                      return null;
                    }

                    const startSlot = getStartSlot(task.startDate);
                    const endSlot = getEndSlot(task.endDate);

                    return (
                      <div
                        key={`${task.name}-${task.startDate.toString()}`}
                        style={{
                          gridColumn: dayIndex + 2,
                          gridRow: `${startSlot + 2} / ${endSlot + 2}`,
                        }}
                        className="pointer-events-auto m-0.5 flex cursor-pointer flex-col gap-1 rounded-2xl border border-emerald-200/30 bg-emerald-400/25 px-3 py-2 text-emerald-50 shadow-lg backdrop-blur transition hover:border-emerald-200/70 hover:bg-emerald-400/35"
                        onClick={() => openEdit(task)}
                      >
                        <div className="flex items-center justify-between gap-2">
                          <div className="text-base font-semibold leading-tight text-emerald-50 truncate" title={task.name}>
                            {task.name}
                          </div>
                          {task.isFixed && (
                            <span className="rounded-full bg-emerald-500/15 px-2 py-[2px] text-[10px] font-semibold uppercase tracking-wide text-emerald-50">
                              Fixed
                            </span>
                          )}
                        </div>
                      </div>
                    );
                  })}
                </div>
              </div>
            )}

            {view === "day" && (
              <div className="relative h-full overflow-auto rounded-2xl border border-slate-800/70 bg-slate-900/60">
                <div
                  className="grid min-h-full grid-cols-[5rem_1fr] text-slate-500"
                  style={{ gridTemplateRows: `2.5rem repeat(${intervalsPerDay}, minmax(0,1fr))` }}
                >
                  <div className="flex flex-col items-center justify-center border-b border-slate-800/80 bg-slate-900/70 text-center text-sm font-semibold text-slate-100" style={{ gridColumn: 2 }}>
                    <div>{today.format("ddd")}</div>
                    <div className="text-xs font-normal text-slate-400">{today.format("DD MMM")}</div>
                  </div>

                  {Array.from({ length: intervalsPerDay }).map((_, i) => {
                    const time = dayjs().startOf("day").add((startHour * 60 + i * 15), "minute");
                    const showLabel = time.minute() === 0;
                    return (
                      <div
                        key={`day-time-${i}`}
                        style={{ gridRow: i + 2 }}
                        className="flex items-start justify-end border-t border-slate-900/80 pr-3 text-[11px] leading-4 text-slate-500"
                      >
                        {showLabel ? time.format("HH:mm") : ""}
                      </div>
                    );
                  })}

                  {Array.from({ length: intervalsPerDay }).map((_, slot) => (
                    <div
                      key={`day-${slot}`}
                      style={{ gridColumn: 2, gridRow: slot + 2 }}
                      className="border-t border-l border-slate-900/60"
                    ></div>
                  ))}
                </div>

                <div
                  className="pointer-events-none absolute inset-0 grid grid-cols-[5rem_1fr] px-2 pb-6"
                  style={{ gridTemplateRows: `2.5rem repeat(${intervalsPerDay}, minmax(0,1fr))` }}
                >
                  {tasksToday.map((task) => {
                    const start = dayjs(task.startDate);
                    const end = dayjs(task.endDate);
                    const startSlot = getStartSlot(task.startDate);
                    const endSlot = getEndSlot(task.endDate);

                    return (
                      <div
                        key={`${task.name}-${task.startDate.toString()}`}
                        style={{
                          gridColumn: 2,
                          gridRow: `${startSlot + 2} / ${endSlot + 2}`,
                        }}
                        className="pointer-events-auto m-0.5 flex cursor-pointer flex-col gap-1 rounded-2xl border border-emerald-200/30 bg-emerald-400/25 px-3 py-2 text-emerald-50 shadow-lg backdrop-blur transition hover:border-emerald-200/70 hover:bg-emerald-400/35"
                        onClick={() => openEdit(task)}
                      >
                        <div className="flex items-center justify-between gap-2">
                          <div className="text-base font-semibold leading-tight text-emerald-50 truncate" title={task.name}>
                            {task.name}
                          </div>
                          {task.isFixed && (
                            <span className="rounded-full bg-emerald-500/15 px-2 py-[2px] text-[10px] font-semibold uppercase tracking-wide text-emerald-50">
                              Fixed
                            </span>
                          )}
                        </div>
                      </div>
                    );
                  })}
                </div>
              </div>
            )}

            {view === "month" && (
              <div className="h-full overflow-auto rounded-2xl border border-slate-800/70 bg-slate-900/60 p-4">
                <div className="mb-3 flex items-center justify-between text-sm text-slate-300">
                  <div className="text-lg font-semibold text-slate-50">
                    {today.format("MMMM YYYY")}
                  </div>
                  <div className="text-xs text-slate-400">Tap a day to review tasks</div>
                </div>
                <div className="grid grid-cols-7 gap-2 text-sm">
                  {["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"].map((d) => (
                    <div key={d} className="text-center text-xs uppercase tracking-[0.12em] text-slate-500">
                      {d}
                    </div>
                  ))}
                  {Array.from({ length: monthDays[0].day() === 0 ? 6 : monthDays[0].day() - 1 }).map((_, i) => (
                    <div key={`lead-${i}`} className="h-20 rounded-xl border border-slate-900/40 bg-slate-900/40" />
                  ))}
                  {monthDays.map((day) => {
                    const dayTasks = tasksThisMonth.filter((t) => dayjs(t.startDate).isSame(day, "day"));
                    return (
                      <div
                        key={day.toString()}
                        className="flex h-24 flex-col gap-1 rounded-xl border border-slate-800 bg-slate-900/70 p-2 text-slate-100 shadow-sm"
                      >
                        <div className="flex items-center justify-between text-xs text-slate-300">
                          <span className="font-semibold">{day.date()}</span>
                          {day.isSame(today, "day") && <span className="text-emerald-300">Today</span>}
                        </div>
                        <div className="flex flex-col gap-1 overflow-y-auto text-[11px] text-slate-200">
                          {dayTasks.length === 0 && <span className="text-slate-500">No tasks</span>}
                          {dayTasks.map((t) => (
                            <span
                              key={`${t.name}-${t.startDate.toString()}`}
                              className="truncate rounded-lg bg-emerald-400/15 px-2 py-1 text-emerald-100 transition hover:bg-emerald-400/25"
                              onClick={() => openEdit(t)}
                            >
                              {t.name}
                            </span>
                          ))}
                        </div>
                      </div>
                    );
                  })}
                </div>
              </div>
            )}
          </div>
        </div>

        <div className="flex h-full min-h-[62vh] flex-col gap-4 rounded-3xl border border-slate-800 bg-gradient-to-b from-slate-900/80 to-slate-950/80 p-6 shadow-xl backdrop-blur">
          <div className="flex items-center justify-between">
            <div className="text-lg font-semibold text-slate-50">Upcoming</div>
            <button className="rounded-full border border-emerald-300/60 bg-emerald-400/10 px-3 py-1 text-xs font-semibold text-emerald-100">
              New task
            </button>
          </div>

          <div className="flex min-h-0 flex-1 flex-col gap-3 overflow-y-auto pr-1 text-sm text-slate-200">
            {tasksThisWeek.length === 0 && (
              <div className="rounded-2xl border border-dashed border-slate-700 bg-slate-900/60 p-4 text-slate-400">
                No tasks planned this week.
              </div>
            )}

            {tasksThisWeek.map((task) => (
              <div
                key={`${task.name}-list-${task.startDate.toString()}`}
                className="rounded-2xl border border-slate-800 bg-slate-900/80 p-4 shadow-sm transition hover:border-emerald-300/50 hover:bg-slate-900/70 cursor-pointer"
                onClick={() => openEdit(task)}
              >
                <div className="text-[11px] uppercase tracking-[0.12em] text-emerald-200">
                  {dayjs(task.startDate).format("ddd, DD MMM")}
                </div>
                <div className="flex items-center justify-between gap-2 text-base font-semibold text-slate-50">
                  <span className="truncate">{task.name}</span>
                  <div className="flex items-center gap-2">
                    {task.isFixed && (
                      <span className="rounded-full bg-emerald-500/20 px-2 py-1 text-[11px] uppercase tracking-wide text-emerald-50">
                        Fixed
                      </span>
                    )}
                    <span className="rounded-full bg-slate-800 px-2 py-1 text-[11px] uppercase tracking-wide text-slate-300">
                      {(task.status ?? "todo").replace("-", " ")}
                    </span>
                  </div>
                </div>
                <div className="text-xs text-slate-300">
                  {dayjs(task.startDate).format("HH:mm")} - {dayjs(task.endDate).format("HH:mm")}
                </div>
                {task.description && <div className="mt-1 text-sm text-slate-400">{task.description}</div>}
              </div>
            ))}
          </div>

          <div className="mt-2 rounded-2xl border border-slate-800 bg-slate-900/70 p-4 text-slate-100">
            <div className="text-sm font-semibold">Add task</div>
            <div className="mt-4 flex flex-col gap-3 text-sm">
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
                  className="min-h-[90px] rounded-xl border border-slate-800 bg-slate-900/80 px-3 py-2 text-slate-50 outline-none ring-emerald-400/40 focus:border-emerald-400/60 focus:ring"
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
                <label className="text-xs uppercase tracking-[0.14em] text-slate-500">Assignee</label>
                <select
                  value={editForm.assigneeEmail}
                  onChange={(e) => setEditForm({ ...editForm, assigneeEmail: e.target.value })}
                  className="rounded-xl border border-slate-800 bg-slate-900 px-3 py-2 text-slate-50 outline-none ring-emerald-400/40 focus:border-emerald-400/60 focus:ring"
                >
                  {assigneeOptions.map((email) => (
                    <option key={email} value={email}>
                      {email}
                    </option>
                  ))}
                </select>
              </div>

              <div className="flex flex-col gap-2 col-span-2">
                <label className="text-xs uppercase tracking-[0.14em] text-slate-500">Description</label>
                <textarea
                  value={editForm.description}
                  onChange={(e) => setEditForm({ ...editForm, description: e.target.value })}
                  className="min-h-[90px] rounded-xl border border-slate-800 bg-slate-900 px-3 py-2 text-slate-50 outline-none ring-emerald-400/40 focus:border-emerald-400/60 focus:ring"
                />
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

            <div className="mt-4 flex justify-end gap-3">
              <button
                onClick={() => setEditingTask(null)}
                className="rounded-full border border-slate-800 bg-slate-900 px-4 py-2 text-sm text-slate-200 hover:border-slate-600"
              >
                Cancel
              </button>
              <button
                onClick={saveEdit}
                className="rounded-full border border-emerald-300/60 bg-emerald-400/20 px-5 py-2 text-sm font-semibold text-emerald-50 shadow-sm transition hover:bg-emerald-400/30"
              >
                Save changes
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default Tasks;
