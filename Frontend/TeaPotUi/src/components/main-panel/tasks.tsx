import type {
  DateSelectArg,
  EventClickArg,
  EventContentArg,
  EventDropArg,
  EventInput,
} from "@fullcalendar/core";
import dayGridPlugin from "@fullcalendar/daygrid";
import interactionPlugin, {
  type EventResizeDoneArg,
} from "@fullcalendar/interaction";
import FullCalendar from "@fullcalendar/react";
import timeGridPlugin from "@fullcalendar/timegrid";
import dayjs from "dayjs";
import { useEffect, useMemo, useRef, useState, type FC } from "react";
import useUserStore from "../../stores/user-store";
import { fetchBlocks, fetchTasks, type TaskBlock } from "../../util/task-api";
import type { Task, WorkBreak, WorkWeekDay } from "../../util/types";
import { saveWorkProfile } from "../../util/work-profile-api";
import {
  getBreakColor,
  getOrgColor,
  rgbToCss,
  type RgbColor,
} from "../../util/color-prefs";
import "./tasks-calendar.css";

const API_BASE = import.meta.env.VITE_API_BASE_URL ?? "";

const DAY_TO_JS: Record<WorkWeekDay, number> = {
  Mon: 1,
  Tue: 2,
  Wed: 3,
  Thu: 4,
  Fri: 5,
  Sat: 6,
  Sun: 0,
};
const JS_TO_DAY: Record<number, WorkWeekDay> = {
  0: "Sun",
  1: "Mon",
  2: "Tue",
  3: "Wed",
  4: "Thu",
  5: "Fri",
  6: "Sat",
};

const Tasks: FC = () => {
  const {
    user,
    setUser,
    activeOrganizationId,
    setActiveOrganization,
    addTask,
    saveTask,
    removeTask,
    workProfileId,
  } = useUserStore();
  const [form, setForm] = useState({
    name: "",
    description: "",
    durationMinutes: 60,
    priority: "medium" as Task["priority"],
    intensity: "normal" as Task["intensity"],
    status: "todo" as Task["status"],
    deadline: "",
    fixedStart: "",
    dependencies: [] as string[],
    isFixed: false,
  });
  const [calendarDialogOpen, setCalendarDialogOpen] = useState(false);
  const [editingTask, setEditingTask] = useState<Task | null>(null);
  const [editForm, setEditForm] = useState({
    name: "",
    description: "",
    start: "",
    end: "",
    priority: "medium" as Task["priority"],
    status: "todo" as Task["status"],
    organizationId: "",
    isFixed: false,
  });
  const [editError, setEditError] = useState<string | undefined>();
  const [status, setStatus] = useState<string | undefined>();
  const [error, setError] = useState<string | undefined>();
  const [view, setView] = useState<"day" | "week" | "month">("week");
  const [filterStatus, setFilterStatus] = useState<
    "all" | "todo" | "in-progress" | "done"
  >("all");
  const [filterOrgId, setFilterOrgId] = useState<string | "all">(
    activeOrganizationId ?? "all",
  );
  const [scheduling, setScheduling] = useState(false);
  const [scheduleMsg, setScheduleMsg] = useState<{
    ok: boolean;
    text: string;
  } | null>(null);
  const [blocks, setBlocks] = useState<TaskBlock[]>([]);
  const [editingBreak, setEditingBreak] = useState<{
    breakId: string;
    weekDay: WorkWeekDay;
    start: string;
    end: string;
  } | null>(null);
  const [editBreakForm, setEditBreakForm] = useState({ start: "", end: "" });
  const [editBreakError, setEditBreakError] = useState<string | undefined>();
  const [colorVersion, setColorVersion] = useState(0);
  const [plannerViewForm, setPlannerViewForm] = useState({
    startTime: user.plannerViewStart ?? "06:00",
    endTime: user.plannerViewEnd ?? "22:00",
  });

  const savePlannerView = (startTime: string, endTime: string) => {
    if (!user.workProfile) return;
    const updatedProfile = {
      ...user.workProfile,
      plannerViewStart: startTime,
      plannerViewEnd: endTime,
    };
    setUser({
      ...user,
      workProfile: updatedProfile,
      plannerViewStart: startTime,
      plannerViewEnd: endTime,
    });
    saveWorkProfile(user.id, updatedProfile).catch(() => setUser({ ...user }));
  };

  useEffect(() => {
    const handler = () => setColorVersion((v) => v + 1);
    window.addEventListener("teapot-colors-changed", handler);
    return () => window.removeEventListener("teapot-colors-changed", handler);
  }, []);

  useEffect(() => {
    if (!workProfileId) return;
    fetchBlocks(workProfileId)
      .then(setBlocks)
      .catch(() => setBlocks([]));
  }, [workProfileId]);

  useEffect(() => {
    setFilterOrgId(activeOrganizationId ?? "all");
  }, [activeOrganizationId]);

  if (!user) {
    return <></>;
  }

  const triggerSchedule = async () => {
    if (!workProfileId) {
      setScheduleMsg({ ok: false, text: "No work profile found." });
      return;
    }
    setScheduling(true);
    setScheduleMsg(null);
    try {
      const res = await fetch(
        `${API_BASE}/api/planning/${encodeURIComponent(workProfileId)}/schedule`,
        { method: "POST" },
      );
      const json = (await res.json()) as {
        success: boolean;
        errorMessage?: string;
        backtrackingCount?: number;
      };
      if (json.success) {
        setScheduleMsg({
          ok: true,
          text: `Plan created (${json.backtrackingCount ?? 0} backtracks).`,
        });
        // Reload tasks and blocks so the calendar reflects the new schedule
        const updated = await fetchTasks(workProfileId);
        setUser({ ...user, tasks: updated });
        const updatedBlocks = await fetchBlocks(workProfileId);
        setBlocks(updatedBlocks);
      } else {
        setScheduleMsg({
          ok: false,
          text: json.errorMessage ?? "Scheduling failed.",
        });
      }
    } catch {
      setScheduleMsg({ ok: false, text: "Network error while scheduling." });
    } finally {
      setScheduling(false);
    }
  };

  const orgOptions = useMemo(() => user.orgs ?? [], [user.orgs]);
  const selectedFilterOrg =
    filterOrgId === "all"
      ? undefined
      : orgOptions.find((org) => org.id === filterOrgId);

  const filteredTasks = (user.tasks ?? []).filter((t) => {
    const byStatus =
      filterStatus === "all" || (t.status ?? "todo") === filterStatus;
    const byOrg =
      filterOrgId === "all" ||
      t.org === filterOrgId ||
      (!!selectedFilterOrg?.workProfileId &&
        t.org === selectedFilterOrg.workProfileId);
    return byStatus && byOrg;
  });

  // blocks are fetched for auto-schedule status; direct task rendering uses user.tasks
  void blocks;

  const calendarRef = useRef<FullCalendar>(null);

  // Generate recurring break events from work profile for a ±8 week window
  const breakEvents: EventInput[] = [];
  if (user.workProfile) {
    const windowStart = dayjs().subtract(14, "day").startOf("day");
    const windowEnd = dayjs().add(42, "day").startOf("day");
    for (const dayProfile of user.workProfile.days) {
      const targetDow = DAY_TO_JS[dayProfile.day];
      for (const workBreak of dayProfile.breaks) {
        const [sh, sm] = workBreak.startTime.split(":").map(Number);
        const [eh, em] = workBreak.endTime.split(":").map(Number);
        let date = windowStart.clone();
        while (date.day() !== targetDow) date = date.add(1, "day");
        while (date.isBefore(windowEnd)) {
          const breakC = getBreakColor();
          void colorVersion;
          breakEvents.push({
            id: `break-${workBreak.id}-${date.format("YYYY-MM-DD")}`,
            title: "Break",
            start: date.hour(sh).minute(sm).second(0).toDate(),
            end: date.hour(eh).minute(em).second(0).toDate(),
            backgroundColor: rgbToCss(breakC, 0.15),
            borderColor: rgbToCss(breakC, 0.45),
            textColor: rgbToCss(breakC, 1),
            classNames: ["break-event"],
            editable: true,
            extendedProps: {
              type: "break",
              breakId: workBreak.id,
              weekDay: dayProfile.day,
            },
          });
          date = date.add(7, "day");
        }
      }
    }
  }

  const calendarEvents: EventInput[] = filteredTasks
    .filter((t) => t.startDate && t.endDate)
    .map((t) => {
      // eslint-disable-next-line react-hooks/exhaustive-deps
      const c: RgbColor = t.org ? getOrgColor(t.org) : getOrgColor("");
      void colorVersion; // reactive dependency
      return {
        id: t.id ?? `task-${t.name}`,
        title: t.name,
        start: t.startDate,
        end: t.endDate,
        backgroundColor: rgbToCss(c, 0.22),
        borderColor: t.isFixed ? rgbToCss(c, 0.65) : rgbToCss(c, 0.45),
        textColor: "#f0fdf4",
        classNames: [
          "task-event",
          t.isFixed ? "task-fixed" : "",
          (t.status ?? "todo") === "done" ? "task-done" : "",
        ].filter(Boolean),
        editable: true,
        extendedProps: { task: t },
      };
    });

  const updateBreakInProfile = (
    breakId: string,
    oldWeekDay: WorkWeekDay,
    newWeekDay: WorkWeekDay,
    newStartTime: string,
    newEndTime: string,
    revert: () => void,
  ) => {
    if (!user.workProfile) {
      revert();
      return;
    }
    const updatedDays = user.workProfile.days.map((day) => {
      if (day.day === oldWeekDay && day.day === newWeekDay) {
        return {
          ...day,
          breaks: day.breaks.map((b) =>
            b.id === breakId
              ? { ...b, startTime: newStartTime, endTime: newEndTime }
              : b,
          ),
        };
      }
      if (day.day === oldWeekDay) {
        return { ...day, breaks: day.breaks.filter((b) => b.id !== breakId) };
      }
      if (day.day === newWeekDay) {
        const movedBreak: WorkBreak = {
          id: breakId,
          startTime: newStartTime,
          endTime: newEndTime,
        };
        return { ...day, breaks: [...day.breaks, movedBreak] };
      }
      return day;
    });
    const updatedProfile = { ...user.workProfile, days: updatedDays };
    setUser({ ...user, workProfile: updatedProfile });
    saveWorkProfile(user.id, updatedProfile).catch(() => {
      setUser({ ...user });
      revert();
    });
  };

  const handleEventDrop = (arg: EventDropArg) => {
    if (arg.event.extendedProps.type === "break") {
      const breakId = arg.event.extendedProps.breakId as string;
      const oldWeekDay = arg.event.extendedProps.weekDay as WorkWeekDay;
      const newStart = arg.event.start!;
      const newEnd = arg.event.end!;
      const newWeekDay = JS_TO_DAY[dayjs(newStart).day()];
      updateBreakInProfile(
        breakId,
        oldWeekDay,
        newWeekDay,
        dayjs(newStart).format("HH:mm"),
        dayjs(newEnd).format("HH:mm"),
        () => arg.revert(),
      );
      return;
    }
    const task = arg.event.extendedProps.task as Task | undefined;
    if (!task) {
      arg.revert();
      return;
    }
    const newStart = arg.event.start!;
    const duration = dayjs(task.endDate).diff(dayjs(task.startDate), "minute");
    const newEnd = dayjs(newStart).add(duration, "minute").toDate();
    const updatedTask: Task = {
      ...task,
      startDate: newStart,
      endDate: newEnd,
      deadline: newEnd,
    };
    saveTask(updatedTask).catch(() => arg.revert());
  };

  const handleEventResize = (arg: EventResizeDoneArg) => {
    if (arg.event.extendedProps.type === "break") {
      const breakId = arg.event.extendedProps.breakId as string;
      const oldWeekDay = arg.event.extendedProps.weekDay as WorkWeekDay;
      const newStart = arg.event.start!;
      const newEnd = arg.event.end!;
      const newWeekDay = JS_TO_DAY[dayjs(newStart).day()];
      updateBreakInProfile(
        breakId,
        oldWeekDay,
        newWeekDay,
        dayjs(newStart).format("HH:mm"),
        dayjs(newEnd).format("HH:mm"),
        () => arg.revert(),
      );
      return;
    }
    const task = arg.event.extendedProps.task as Task | undefined;
    if (!task) {
      arg.revert();
      return;
    }
    const newStart = arg.event.start!;
    const newEnd = arg.event.end!;
    const updatedTask: Task = {
      ...task,
      startDate: newStart,
      endDate: newEnd,
      deadline: newEnd,
    };
    saveTask(updatedTask).catch(() => arg.revert());
  };

  const handleEventClick = (arg: EventClickArg) => {
    if (arg.event.extendedProps.type === "break") {
      const breakId = arg.event.extendedProps.breakId as string;
      const weekDay = arg.event.extendedProps.weekDay as WorkWeekDay;
      const start = arg.event.start!;
      const end = arg.event.end!;
      setEditBreakError(undefined);
      setEditingBreak({
        breakId,
        weekDay,
        start: dayjs(start).format("HH:mm"),
        end: dayjs(end).format("HH:mm"),
      });
      setEditBreakForm({
        start: dayjs(start).format("HH:mm"),
        end: dayjs(end).format("HH:mm"),
      });
      return;
    }
    const task = arg.event.extendedProps.task as Task | undefined;
    if (task) openEdit(task);
  };

  const deleteBreak = () => {
    if (!editingBreak || !user.workProfile) return;
    const updatedDays = user.workProfile.days.map((day) =>
      day.day === editingBreak.weekDay
        ? {
          ...day,
          breaks: day.breaks.filter((b) => b.id !== editingBreak.breakId),
        }
        : day,
    );
    const updatedProfile = { ...user.workProfile, days: updatedDays };
    setUser({ ...user, workProfile: updatedProfile });
    saveWorkProfile(user.id, updatedProfile).catch(() => setUser({ ...user }));
    setEditingBreak(null);
  };

  const saveBreakEdit = () => {
    if (!editingBreak || !user.workProfile) return;
    setEditBreakError(undefined);
    const [sh, sm] = editBreakForm.start.split(":").map(Number);
    const [eh, em] = editBreakForm.end.split(":").map(Number);
    if (isNaN(sh) || isNaN(sm) || isNaN(eh) || isNaN(em)) {
      setEditBreakError("Invalid time format.");
      return;
    }
    if (sh * 60 + sm >= eh * 60 + em) {
      setEditBreakError("End must be after start.");
      return;
    }
    updateBreakInProfile(
      editingBreak.breakId,
      editingBreak.weekDay,
      editingBreak.weekDay,
      editBreakForm.start,
      editBreakForm.end,
      () => setEditBreakError("Could not save break."),
    );
    setEditingBreak(null);
  };

  const handleCalendarSelect = (selectionInfo: DateSelectArg) => {
    const start = dayjs(selectionInfo.start);
    const end = dayjs(selectionInfo.end);
    setForm((current) => ({
      ...current,
      fixedStart: start.format("YYYY-MM-DDTHH:mm"),
      deadline: end.format("YYYY-MM-DDTHH:mm"),
      isFixed: true,
    }));
    setCalendarDialogOpen(true);
    calendarRef.current?.getApi().unselect();
  };

  const renderEventContent = (arg: EventContentArg) => {
    const start = arg.event.start!;
    const end = arg.event.end!;
    const timeStr = `${dayjs(start).format("HH:mm")}–${dayjs(end).format("HH:mm")}`;
    if (arg.event.extendedProps.type === "break") {
      return (
        <div className="break-event-content">
          <span className="break-event-label">☕ Pause</span>
          <span className="break-event-time">{timeStr}</span>
        </div>
      );
    }
    const task = arg.event.extendedProps.task as Task | undefined;
    return (
      <div className="task-event-content">
        <span className="task-event-time">{timeStr}</span>
        <span className="task-event-name">{arg.event.title}</span>
        {task?.isFixed && <span className="task-event-badge">Fixed</span>}
      </div>
    );
  };

  const fcView =
    view === "day"
      ? "timeGridDay"
      : view === "week"
        ? "timeGridWeek"
        : "dayGridMonth";
  const today = dayjs();
  const upcomingTasks = filteredTasks
    .filter((t) => t.endDate && dayjs(t.endDate).isAfter(today))
    .sort(
      (a, b) =>
        new Date(a.startDate).getTime() - new Date(b.startDate).getTime(),
    );

  const dependencyOptions = useMemo(() => user.tasks ?? [], [user.tasks]);

  const changeOrganizationContext = (organizationId: string | "all") => {
    if (organizationId === "all") {
      setFilterOrgId("all");
      return;
    }

    setFilterOrgId(organizationId);
    void setActiveOrganization(organizationId).catch((err: unknown) => {
      setError(String(err));
    });
  };
  const submitTask = () => {
    setError(undefined);
    setStatus(undefined);

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
    if (form.isFixed && !form.fixedStart) {
      setError("Start time is required for fixed tasks.");
      return;
    }
    if (!form.isFixed && form.durationMinutes <= 0) {
      setError("Duration must be greater than 0 minutes.");
      return;
    }
    if (form.durationMinutes > 10000){
      setError("Duration is too long.");
      return;
    }

    const deadline = dayjs(form.deadline);
    if (!deadline.isValid()) {
      setError(form.isFixed ? "End time is invalid." : "Deadline is invalid.");
      return;
    }

    let endDate: Date;
    let startDate: Date;
    if (form.isFixed) {
      const fixedStartDayjs = dayjs(form.fixedStart);
      if (!fixedStartDayjs.isValid()) {
        setError("Start time is invalid.");
        return;
      }
      if (!deadline.isAfter(fixedStartDayjs)) {
        setError("End time must be after start time.");
        return;
      }
      startDate = fixedStartDayjs.toDate();
      endDate = deadline.toDate();
    } else {
      endDate = deadline.toDate();
      startDate = deadline.subtract(form.durationMinutes, "minute").toDate();
    }

    const dependencies = dependencyOptions.filter((t) =>
      form.dependencies.includes(t.name),
    );

    const selectedOrg =
      orgOptions.find((org) => org.id === activeOrganizationId) ||
      (filterOrgId !== "all" &&
        orgOptions.find((org) => org.id === filterOrgId)) ||
      orgOptions[0];

    if (!selectedOrg) {
      setError("No organization available for this task.");
      return;
    }

    const newTask: Task = {
      name: form.name.trim(),
      description: form.description.trim(),
      startDate,
      endDate,
      deadline: endDate,
      isFixed: form.isFixed,
      priority: form.priority,
      intensity: form.intensity,
      status: form.status ?? "todo",
      org: selectedOrg.id,
      recurrence: "none",
      dependencies,
    };

    const conflicts = (user.tasks ?? []).filter((t) => {
      if (t.org !== selectedOrg.id) return false;
      const s = dayjs(t.startDate);
      const e = dayjs(t.endDate);
      return s.isBefore(endDate) && e.isAfter(startDate);
    });
    if (conflicts.length > 0) {
      setError(
        `Overlap with ${conflicts.length} task(s). Consider rescheduling.`,
      );
    }

    setStatus("Saving…");
    addTask(newTask)
      .then(async () => {
        setForm({
          name: "",
          description: "",
          durationMinutes: 60,
          priority: "medium",
          intensity: "normal",
          status: "todo",
          deadline: "",
          fixedStart: "",
          dependencies: [],
          isFixed: false,
        });
        setStatus("Task created");
        setCalendarDialogOpen(false);
        if (workProfileId) {
          const updatedBlocks = await fetchBlocks(workProfileId).catch(
            () => null,
          );
          if (updatedBlocks) setBlocks(updatedBlocks);
        }
      })
      .catch((err: unknown) => {
        setError(String(err));
        setStatus(undefined);
      });
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
      organizationId: task.org,
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

    const updatedTask: Task = {
      ...editingTask,
      name: editForm.name.trim(),
      description: editForm.description.trim(),
      startDate: start.toDate(),
      endDate: end.toDate(),
      deadline: end.toDate(),
      priority: editForm.priority,
      status: editForm.status,
      org: editForm.organizationId,
      isFixed: editForm.isFixed,
    };

    saveTask(updatedTask).catch((err: unknown) => setEditError(String(err)));
    setEditingTask(null);
  };

  return (
    <div className="grid h-full w-full grid-rows-[3.5rem_1fr] gap-6 bg-linear-to-br from-slate-950 via-slate-900 to-slate-950 p-6 text-slate-50">
      <div className="flex items-center justify-between">
        <div className="flex flex-col gap-1">
          <span className="text-xs tracking-[0.28em] text-emerald-300 uppercase">
            Planner
          </span>
          <h1 className="text-4xl leading-tight font-semibold">
            Task Calendar
          </h1>
          <span className="text-sm text-slate-400">
            {filteredTasks.length} task(s)
          </span>
        </div>
        <div className="flex items-center gap-4 text-sm">
          <div className="flex flex-col items-end gap-1">
            <button
              onClick={() => {
                void triggerSchedule();
              }}
              disabled={scheduling}
              className="rounded-full border border-emerald-500/60 bg-emerald-500/15 px-5 py-2 font-semibold text-emerald-100 transition hover:bg-emerald-500/25 disabled:cursor-not-allowed disabled:opacity-50"
            >
              {scheduling ? "Scheduling…" : "Auto-Schedule"}
            </button>
            {scheduleMsg && (
              <span
                className={`text-xs ${scheduleMsg.ok ? "text-emerald-400" : "text-red-400"}`}
              >
                {scheduleMsg.text}
              </span>
            )}
          </div>
          <button
            onClick={() => setView("day")}
            className={`rounded-full px-4 py-2 font-semibold transition ${view === "day"
                ? "border border-emerald-400/60 bg-emerald-400/15 text-emerald-100"
                : "border border-slate-700 bg-slate-900/60 text-slate-300 hover:border-emerald-300/40 hover:text-emerald-100"
              }`}
          >
            Day
          </button>
          <button
            onClick={() => setView("week")}
            className={`rounded-full px-4 py-2 font-semibold transition ${view === "week"
                ? "border border-emerald-400/60 bg-emerald-400/15 text-emerald-100"
                : "border border-slate-700 bg-slate-900/60 text-slate-300 hover:border-emerald-300/40 hover:text-emerald-100"
              }`}
          >
            Week
          </button>
          <button
            onClick={() => setView("month")}
            className={`rounded-full px-4 py-2 font-semibold transition ${view === "month"
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
            onChange={(e) =>
              setFilterStatus(e.target.value as typeof filterStatus)
            }
            className="rounded-full border border-slate-800 bg-slate-900/70 px-3 py-1 outline-none"
          >
            <option value="all">All statuses</option>
            <option value="todo">To Do</option>
            <option value="in-progress">In Progress</option>
            <option value="done">Done</option>
          </select>
          <select
            value={filterOrgId}
            onChange={(e) =>
              changeOrganizationContext(e.target.value as typeof filterOrgId)
            }
            className="rounded-full border border-slate-800 bg-slate-900/70 px-3 py-1 outline-none"
          >
            <option value="all">All assignees</option>
            {orgOptions.map((org) => (
              <option key={org.id} value={org.id}>
                {org.name}
              </option>
            ))}
          </select>
        </div>
      </div>

      <div className="grid h-full min-h-0 grid-cols-[1.35fr_0.65fr] gap-5 max-lg:grid-cols-1">
        <div className="relative flex min-h-0 flex-col overflow-hidden rounded-3xl border border-slate-800 bg-linear-to-b from-slate-900/80 to-slate-950/80 shadow-2xl backdrop-blur">
          <div className="task-calendar min-h-0 flex-1 p-4">
            <FullCalendar
              ref={calendarRef}
              plugins={[timeGridPlugin, dayGridPlugin, interactionPlugin]}
              initialView={fcView}
              key={fcView}
              events={[...calendarEvents, ...breakEvents]}
              editable
              eventStartEditable
              eventDurationEditable
              eventResizableFromStart
              selectable
              selectMirror
              selectMinDistance={10}
              slotDuration="00:30:00"
              snapDuration="00:15:00"
              slotLabelInterval="01:00:00"
              eventDrop={handleEventDrop}
              eventResize={handleEventResize}
              eventClick={handleEventClick}
              eventContent={renderEventContent}
              select={handleCalendarSelect}
              headerToolbar={{
                left: "prev,next today",
                center: "title",
                right: "",
              }}
              customButtons={{
                visibleRange: {
                  text: "",
                  click: () => { },
                },
              }}
              slotMinTime={`${plannerViewForm.startTime}:00`}
              slotMaxTime={`${plannerViewForm.endTime}:00`}
              scrollTime={`${plannerViewForm.startTime}:00`}
              allDaySlot={false}
              height="100%"
              locale="de"
              firstDay={1}
            />
            {/* Visible range controls overlaid on the calendar toolbar */}
            <div className="pointer-events-none absolute inset-x-4 top-4 flex items-center justify-end">
              <div className="pointer-events-auto flex items-center gap-1 rounded-lg border border-slate-700/60 bg-slate-900/90 px-3 py-1 text-xs backdrop-blur">
                <span className="text-[11px] tracking-[0.14em] text-slate-500 uppercase">
                  Visible
                </span>
                <input
                  type="time"
                  step={900}
                  value={plannerViewForm.startTime}
                  onChange={(e) => {
                    const startTime = e.target.value;
                    setPlannerViewForm((f) => ({ ...f, startTime }));
                    savePlannerView(startTime, plannerViewForm.endTime);
                  }}
                  className="w-20 bg-transparent text-xs text-slate-100 outline-none"
                />
                <span className="text-slate-600">–</span>
                <input
                  type="time"
                  step={900}
                  value={plannerViewForm.endTime}
                  onChange={(e) => {
                    const endTime = e.target.value;
                    setPlannerViewForm((f) => ({ ...f, endTime }));
                    savePlannerView(plannerViewForm.startTime, endTime);
                  }}
                  className="w-20 bg-transparent text-xs text-slate-100 outline-none"
                />
              </div>
            </div>
          </div>
        </div>

        <div className="flex h-full min-h-[62vh] flex-col gap-4 rounded-3xl border border-slate-800 bg-linear-to-b from-slate-900/80 to-slate-950/80 p-6 shadow-xl backdrop-blur">
          <div className="flex items-center justify-between">
            <div className="text-lg font-semibold text-slate-50">Upcoming</div>
            <button
              className="rounded-full border border-emerald-300/60 bg-emerald-400/10 px-3 py-1 text-xs font-semibold text-emerald-100"
              onClick={() => {
                setForm({
                  name: "",
                  description: "",
                  durationMinutes: 60,
                  priority: "medium",
                  intensity: "normal",
                  status: "todo",
                  deadline: "",
                  fixedStart: "",
                  dependencies: [],
                  isFixed: false,
                });
                setError(undefined);
                setStatus(undefined);
                setCalendarDialogOpen(true);
              }}
            >
              New task
            </button>
          </div>

          <div className="flex min-h-0 flex-1 flex-col gap-3 overflow-y-auto pr-1 text-sm text-slate-200">
            {upcomingTasks.length === 0 && (
              <div className="rounded-2xl border border-dashed border-slate-700 bg-slate-900/60 p-4 text-slate-400">
                No upcoming tasks.
              </div>
            )}

            {upcomingTasks.map((task) => (
              <div
                key={task.id ?? task.name}
                className="rounded-2xl border border-slate-800 bg-slate-900/80 p-4 shadow-sm transition hover:border-emerald-300/50 hover:bg-slate-900/70"
              >
                <div className="text-[11px] tracking-[0.12em] text-emerald-200 uppercase">
                  {dayjs(task.startDate).format("ddd, DD MMM")}
                </div>
                <div className="flex items-center justify-between gap-2 text-base font-semibold text-slate-50">
                  <span className="truncate">{task.name}</span>
                  <div className="flex items-center gap-2">
                    {task.isFixed && (
                      <span className="rounded-full bg-emerald-500/20 px-2 py-1 text-[11px] tracking-wide text-emerald-50 uppercase">
                        Fixed
                      </span>
                    )}
                    <span className="rounded-full bg-slate-800 px-2 py-1 text-[11px] tracking-wide text-slate-300 uppercase">
                      {(task.status ?? "todo").replace("-", " ")}
                    </span>
                    <button
                      onClick={() => openEdit(task)}
                      className="rounded-full border border-slate-700 bg-slate-800 px-2 py-1 text-[11px] text-slate-300 transition hover:border-emerald-300/60 hover:text-emerald-100"
                    >
                      Edit
                    </button>
                  </div>
                </div>
                <div className="text-xs text-slate-300">
                  {dayjs(task.startDate).format("HH:mm")} -{" "}
                  {dayjs(task.endDate).format("HH:mm")}
                </div>
              </div>
            ))}
          </div>

          <button
            onClick={() => {
              setForm({
                name: "",
                description: "",
                durationMinutes: 60,
                priority: "medium",
                intensity: "normal",
                status: "todo",
                deadline: "",
                fixedStart: "",
                dependencies: [],
                isFixed: false,
              });
              setError(undefined);
              setStatus(undefined);
              setCalendarDialogOpen(true);
            }}
            className="mt-2 w-full rounded-2xl border border-dashed border-emerald-300/30 bg-emerald-400/5 py-3 text-xs font-semibold text-emerald-200/70 transition hover:border-emerald-300/60 hover:text-emerald-100"
          >
            + New task — or drag on calendar to set time
          </button>
        </div>
      </div>

      {calendarDialogOpen && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/70 px-4 py-6 backdrop-blur-sm"
          onClick={() => {
            setError(undefined);
            setStatus(undefined);
          }}
        >
          <div data-modal-backdrop="static"
            className="max-h-[90vh] w-full max-w-2xl overflow-y-auto rounded-4xl border border-slate-800 bg-slate-900 p-5 shadow-2xl"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-start justify-between gap-4">
              <div>
                <div className="text-sm font-semibold text-slate-100">
                  New Task
                </div>
                <p className="mt-1 text-xs text-slate-400">
                  {form.fixedStart
                    ? `${dayjs(form.fixedStart).format("ddd DD MMM, HH:mm")} – ${dayjs(form.deadline).format("HH:mm")}`
                    : "Fill in the task details below."}
                </p>
              </div>
              <button
                type="button"
                onClick={() => {
                  setCalendarDialogOpen(false);
                  setError(undefined);
                  setStatus(undefined);
                }}
                className="rounded-full border border-slate-700 bg-slate-950/70 px-3 py-1.5 text-xs font-semibold text-slate-300 transition hover:border-slate-500 hover:text-slate-100"
              >
                ✕ Close
              </button>
            </div>

            <div className="mt-5 flex flex-col gap-4">
              {/* Quick templates */}
              <div>
                <div className="mb-2 text-[11px] tracking-[0.14em] text-slate-500 uppercase">
                  Quick templates
                </div>
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
              </div>

              <div className="flex flex-col gap-1">
                <label className="text-[11px] tracking-[0.14em] text-slate-500 uppercase">
                  Title
                </label>
                <input
                  value={form.name}
                  onChange={(e) => setForm({ ...form, name: e.target.value })}
                  className="rounded-xl border border-slate-800 bg-slate-950/70 px-3 py-2 text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
                  placeholder="Task title"
                  autoFocus
                />
              </div>

              <div className="flex flex-col gap-1">
                <label className="text-[11px] tracking-[0.14em] text-slate-500 uppercase">
                  Description
                </label>
                <textarea
                  value={form.description}
                  onChange={(e) =>
                    setForm({ ...form, description: e.target.value })
                  }
                  className="min-h-20 rounded-xl border border-slate-800 bg-slate-950/70 px-3 py-2 text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
                  placeholder="What needs to be done"
                />
              </div>

              <div className="grid gap-3 sm:grid-cols-2">
                {!form.isFixed && (
                  <div className="flex flex-col gap-1">
                    <label className="text-[11px] tracking-[0.14em] text-slate-500 uppercase">
                      Duration (min)
                    </label>
                    <input
                      type="number"
                      min={1}
                      value={form.durationMinutes}
                      onChange={(e) =>
                        setForm({
                          ...form,
                          durationMinutes: Number(e.target.value),
                        })
                      }
                      className="rounded-xl border border-slate-800 bg-slate-950/70 px-3 py-2 text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
                    />
                  </div>
                )}
                <div className="flex flex-col gap-1">
                  <label className="text-[11px] tracking-[0.14em] text-slate-500 uppercase">
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
                    className="rounded-xl border border-slate-800 bg-slate-950/70 px-3 py-2 text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
                  >
                    <option value="low">Low</option>
                    <option value="medium">Medium</option>
                    <option value="high">High</option>
                  </select>
                </div>
                <div className="flex flex-col gap-1">
                  <label className="text-[11px] tracking-[0.14em] text-slate-500 uppercase">
                    Intensity
                  </label>
                  <select
                    value={form.intensity}
                    onChange={(e) =>
                      setForm({
                        ...form,
                        intensity: e.target.value as Task["intensity"],
                      })
                    }
                    className="rounded-xl border border-slate-800 bg-slate-950/70 px-3 py-2 text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
                  >
                    <option value="light">Light</option>
                    <option value="normal">Normal</option>
                    <option value="intensive">Intensive</option>
                  </select>
                </div>
              </div>

              <div className="grid gap-3 sm:grid-cols-2">
                <div className="flex flex-col gap-1">
                  <label className="text-[11px] tracking-[0.14em] text-slate-500 uppercase">
                    Status
                  </label>
                  <select
                    value={form.status ?? "todo"}
                    onChange={(e) =>
                      setForm({
                        ...form,
                        status: e.target.value as Task["status"],
                      })
                    }
                    className="rounded-xl border border-slate-800 bg-slate-950/70 px-3 py-2 text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
                  >
                    <option value="todo">To Do</option>
                    <option value="in-progress">In Progress</option>
                    <option value="done">Done</option>
                  </select>
                </div>
                <div className="flex flex-col gap-1">
                  <label className="text-[11px] tracking-[0.14em] text-slate-500 uppercase">
                    Assignee
                  </label>
                  <select
                    value={filterOrgId}
                    onChange={(e) =>
                      changeOrganizationContext(
                        e.target.value as typeof filterOrgId,
                      )
                    }
                    className="rounded-xl border border-slate-800 bg-slate-950/70 px-3 py-2 text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
                  >
                    <option value="all">Anyone</option>
                    {orgOptions.map((org) => (
                      <option key={org.id} value={org.id}>
                        {org.name}
                      </option>
                    ))}
                  </select>
                </div>
              </div>

              <label className="flex items-center gap-3 rounded-xl border border-slate-800 bg-slate-950/60 px-3 py-2 text-xs text-slate-300">
                <input
                  type="checkbox"
                  checked={form.isFixed}
                  onChange={(e) =>
                    setForm({ ...form, isFixed: e.target.checked })
                  }
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

              <div className="grid gap-3 sm:grid-cols-2">
                <div className="flex flex-col gap-1">
                  <label className="text-[11px] tracking-[0.14em] text-slate-500 uppercase">
                    {form.isFixed ? "Start time" : "Deadline"}
                  </label>
                  <input
                    type="datetime-local"
                    value={form.isFixed ? form.fixedStart : form.deadline}
                    onChange={(e) =>
                      setForm(
                        form.isFixed
                          ? { ...form, fixedStart: e.target.value }
                          : { ...form, deadline: e.target.value },
                      )
                    }
                    className="rounded-xl border border-slate-800 bg-slate-950/70 px-3 py-2 text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
                  />
                </div>
                {form.isFixed && (
                  <div className="flex flex-col gap-1">
                    <label className="text-[11px] tracking-[0.14em] text-slate-500 uppercase">
                      End time
                    </label>
                    <input
                      type="datetime-local"
                      value={form.deadline}
                      onChange={(e) =>
                        setForm({ ...form, deadline: e.target.value })
                      }
                      className="rounded-xl border border-slate-800 bg-slate-950/70 px-3 py-2 text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
                    />
                  </div>
                )}
              </div>

              <div className="flex flex-col gap-2">
                <label className="text-[11px] tracking-[0.14em] text-slate-500 uppercase">
                  Dependencies
                </label>
                <div className="flex max-h-36 flex-col gap-2 overflow-y-auto rounded-xl border border-slate-800 bg-slate-950/60 p-3">
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
              {status && (
                <div className="text-sm text-emerald-200">{status}</div>
              )}

              <div className="flex flex-wrap gap-2">
                <button
                  onClick={submitTask}
                  className="rounded-full border border-emerald-300/60 bg-emerald-400/15 px-4 py-2 text-sm font-semibold text-emerald-100 shadow-sm transition hover:bg-emerald-400/25"
                >
                  Add task
                </button>
                <button
                  onClick={() => {
                    setCalendarDialogOpen(false);
                    setError(undefined);
                    setStatus(undefined);
                  }}
                  className="rounded-full border border-slate-700 bg-slate-950/70 px-4 py-2 text-sm font-semibold text-slate-300 transition hover:border-slate-500 hover:text-slate-100"
                >
                  Cancel
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {editingTask && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur">
          <div className="w-full max-w-2xl rounded-3xl border border-slate-800 bg-slate-900/95 p-6 shadow-2xl">
            <div className="mb-4 flex items-center justify-between">
              <div>
                <div className="text-xs tracking-[0.18em] text-emerald-300 uppercase">
                  Edit task
                </div>
                <div className="text-2xl font-semibold text-slate-50">
                  {editingTask.name}
                </div>
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
                <label className="text-xs tracking-[0.14em] text-slate-500 uppercase">
                  Title
                </label>
                <input
                  value={editForm.name}
                  onChange={(e) =>
                    setEditForm({ ...editForm, name: e.target.value })
                  }
                  className="rounded-xl border border-slate-800 bg-slate-900 px-3 py-2 text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
                />
              </div>
              <div className="flex flex-col gap-2">
                <label className="text-xs tracking-[0.14em] text-slate-500 uppercase">
                  Priority
                </label>
                <select
                  value={editForm.priority}
                  onChange={(e) =>
                    setEditForm({
                      ...editForm,
                      priority: e.target.value as Task["priority"],
                    })
                  }
                  className="rounded-xl border border-slate-800 bg-slate-900 px-3 py-2 text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
                >
                  <option value="low">Low</option>
                  <option value="medium">Medium</option>
                  <option value="high">High</option>
                </select>
              </div>

              <div className="flex flex-col gap-2">
                <label className="text-xs tracking-[0.14em] text-slate-500 uppercase">
                  Status
                </label>
                <select
                  value={editForm.status}
                  onChange={(e) =>
                    setEditForm({
                      ...editForm,
                      status: e.target.value as Task["status"],
                    })
                  }
                  className="rounded-xl border border-slate-800 bg-slate-900 px-3 py-2 text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
                >
                  <option value="todo">To Do</option>
                  <option value="in-progress">In Progress</option>
                  <option value="done">Done</option>
                </select>
              </div>
              <div className="flex flex-col gap-2">
                <label className="text-xs tracking-[0.14em] text-slate-500 uppercase">
                  Assignee
                </label>
                <select
                  value={editForm.organizationId}
                  onChange={(e) =>
                    setEditForm({ ...editForm, organizationId: e.target.value })
                  }
                  className="rounded-xl border border-slate-800 bg-slate-900 px-3 py-2 text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
                >
                  {orgOptions.map((org) => (
                    <option key={org.id} value={org.id}>
                      {org.name}
                    </option>
                  ))}
                </select>
              </div>

              <div className="col-span-2 flex flex-col gap-2">
                <label className="text-xs tracking-[0.14em] text-slate-500 uppercase">
                  Description
                </label>
                <textarea
                  value={editForm.description}
                  onChange={(e) =>
                    setEditForm({ ...editForm, description: e.target.value })
                  }
                  className="min-h-22.5 rounded-xl border border-slate-800 bg-slate-900 px-3 py-2 text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
                />
              </div>

              <div className="flex flex-col gap-2">
                <label className="text-xs tracking-[0.14em] text-slate-500 uppercase">
                  Start
                </label>
                <input
                  type="datetime-local"
                  value={editForm.start}
                  onChange={(e) =>
                    setEditForm({ ...editForm, start: e.target.value })
                  }
                  className="rounded-xl border border-slate-800 bg-slate-900 px-3 py-2 text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
                />
              </div>
              <div className="flex flex-col gap-2">
                <label className="text-xs tracking-[0.14em] text-slate-500 uppercase">
                  End
                </label>
                <input
                  type="datetime-local"
                  value={editForm.end}
                  onChange={(e) =>
                    setEditForm({ ...editForm, end: e.target.value })
                  }
                  className="rounded-xl border border-slate-800 bg-slate-900 px-3 py-2 text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
                />
              </div>

              <label className="col-span-2 flex items-center gap-3 rounded-xl border border-slate-800 bg-slate-900 px-3 py-2 text-xs text-slate-300">
                <input
                  type="checkbox"
                  checked={editForm.isFixed}
                  onChange={(e) =>
                    setEditForm({ ...editForm, isFixed: e.target.checked })
                  }
                />
                <div className="flex flex-col leading-tight">
                  <span className="text-sm font-semibold text-slate-100">
                    Fixed timeslot
                  </span>
                  <span className="text-[11px] text-slate-500">
                    Keeps meeting time locked.
                  </span>
                </div>
              </label>
            </div>

            {editError && (
              <div className="mt-3 text-sm text-rose-300">{editError}</div>
            )}

            <div className="mt-4 flex justify-between gap-3">
              <button
                onClick={() => {
                  if (editingTask?.id) {
                    removeTask(editingTask.id).catch((err: unknown) =>
                      setEditError(String(err)),
                    );
                  } else {
                    setUser({
                      ...user,
                      tasks: (user.tasks ?? []).filter(
                        (t) => t !== editingTask,
                      ),
                    });
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
        </div>
      )}
      {editingBreak && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm"
          onClick={() => setEditingBreak(null)}
        >
          <div
            className="w-full max-w-sm rounded-3xl border border-amber-800/40 bg-slate-900 p-6 shadow-2xl"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="mb-4 flex items-center justify-between">
              <div>
                <div className="text-xs tracking-[0.18em] text-amber-300 uppercase">
                  Pause bearbeiten
                </div>
                <div className="text-lg font-semibold text-slate-50">
                  {editingBreak.weekDay} &mdash; {editingBreak.start}&ndash;
                  {editingBreak.end}
                </div>
              </div>
              <button
                onClick={() => setEditingBreak(null)}
                className="rounded-full border border-slate-800 bg-slate-900 px-3 py-1 text-xs text-slate-300 hover:border-slate-500 hover:text-slate-100"
              >
                ✕
              </button>
            </div>

            <div className="flex flex-col gap-4 text-sm">
              <div className="grid grid-cols-2 gap-3">
                <div className="flex flex-col gap-1">
                  <label className="text-[11px] tracking-[0.14em] text-slate-500 uppercase">
                    Beginn
                  </label>
                  <input
                    type="time"
                    value={editBreakForm.start}
                    onChange={(e) =>
                      setEditBreakForm({
                        ...editBreakForm,
                        start: e.target.value,
                      })
                    }
                    className="rounded-xl border border-slate-800 bg-slate-950/70 px-3 py-2 text-slate-50 ring-amber-400/40 outline-none focus:border-amber-400/60 focus:ring"
                  />
                </div>
                <div className="flex flex-col gap-1">
                  <label className="text-[11px] tracking-[0.14em] text-slate-500 uppercase">
                    Ende
                  </label>
                  <input
                    type="time"
                    value={editBreakForm.end}
                    onChange={(e) =>
                      setEditBreakForm({
                        ...editBreakForm,
                        end: e.target.value,
                      })
                    }
                    className="rounded-xl border border-slate-800 bg-slate-950/70 px-3 py-2 text-slate-50 ring-amber-400/40 outline-none focus:border-amber-400/60 focus:ring"
                  />
                </div>
              </div>

              {editBreakError && (
                <div className="text-sm text-rose-300">{editBreakError}</div>
              )}

              <div className="flex justify-between gap-3 pt-1">
                <button
                  onClick={deleteBreak}
                  className="rounded-full border border-rose-800/60 bg-rose-900/30 px-4 py-2 text-sm text-rose-300 hover:bg-rose-900/50"
                >
                  Löschen
                </button>
                <div className="flex gap-2">
                  <button
                    onClick={() => setEditingBreak(null)}
                    className="rounded-full border border-slate-800 bg-slate-900 px-4 py-2 text-sm text-slate-200 hover:border-slate-600"
                  >
                    Abbrechen
                  </button>
                  <button
                    onClick={saveBreakEdit}
                    className="rounded-full border border-amber-400/60 bg-amber-500/20 px-5 py-2 text-sm font-semibold text-amber-50 transition hover:bg-amber-500/30"
                  >
                    Speichern
                  </button>
                </div>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default Tasks;
