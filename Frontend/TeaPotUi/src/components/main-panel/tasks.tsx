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
import { CreateTaskModal } from "./task-list";
import useUserStore from "../../stores/user-store";
import { fetchBlocks, fetchTasks, type TaskBlock } from "../../util/task-api";
import type { Task, WorkBreak, WorkWeekDay } from "../../util/types";
import { saveWorkProfile } from "../../util/work-profile-api";
import {
  getBreakColor,
  getBlockerColor,
  getOrgColor,
  isDarkColor,
  readableTextColor,
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
    intensity: "normal" as Task["intensity"],
    organizationId: "",
    isFixed: false,
    dependencies: [] as string[],
  });
  const [editError, setEditError] = useState<string | undefined>();
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
  const [recurringBlockers, setRecurringBlockers] = useState<{
    id?: string; name: string; daysOfWeek: string;
    startTime: string; endTime: string;
    validFrom?: string; validUntil?: string;
  }[]>([]);
  const [editingBreak, setEditingBreak] = useState<{
    breakId: string;
    weekDay: WorkWeekDay;
    start: string;
    end: string;
  } | null>(null);
  const [editBreakForm, setEditBreakForm] = useState({ start: "", end: "" });
  const [editBreakError, setEditBreakError] = useState<string | undefined>();
  const [editingBlocker, setEditingBlocker] = useState<{
    id: string;
    name: string;
    days: string;
    startTime: string;
    endTime: string;
  } | null>(null);
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
    if (!workProfileId) return;
    fetch(`${API_BASE}/api/recurring-blocker/${workProfileId}`)
      .then((r) => r.ok ? r.json() : [])
      .then(setRecurringBlockers)
      .catch(() => setRecurringBlockers([]));
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

  // Index filtered tasks so blocks can be joined back to their task for color/title/status.
  const filteredTaskById = new Map(
    filteredTasks.filter((t) => t.id).map((t) => [t.id!, t]),
  );

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
          const isDarkBreak = isDarkColor(breakC);
          void colorVersion;
          breakEvents.push({
            id: `break-${workBreak.id}-${date.format("YYYY-MM-DD")}`,
            title: "Break",
            start: date.hour(sh).minute(sm).second(0).toDate(),
            end: date.hour(eh).minute(em).second(0).toDate(),
            backgroundColor: rgbToCss(breakC, 0.15),
            borderColor: rgbToCss(breakC, 0.45),
            textColor: readableTextColor(breakC),
            classNames: ["break-event", isDarkBreak ? "is-dark-event-color" : "is-light-event-color"],
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

  // Generate recurring blocker events for ±8 week window
  const blockerEvents: EventInput[] = [];
  {
    const windowStart = dayjs().subtract(14, "day").startOf("day");
    const windowEnd = dayjs().add(42, "day").startOf("day");
    for (const b of recurringBlockers) {
      const days = b.daysOfWeek.split(",").filter(Boolean);
      const [sh, sm] = b.startTime.split(":").map(Number);
      const [eh, em] = b.endTime.split(":").map(Number);
      for (const dayName of days) {
        const targetDow = DAY_TO_JS[dayName as WorkWeekDay];
        if (targetDow === undefined) continue;
        let date = windowStart.clone();
        while (date.day() !== targetDow) date = date.add(1, "day");
        while (date.isBefore(windowEnd)) {
          const dateStr = date.format("YYYY-MM-DD");
          if (b.validFrom && dateStr < b.validFrom) { date = date.add(7, "day"); continue; }
          if (b.validUntil && dateStr > b.validUntil) { date = date.add(7, "day"); continue; }
          blockerEvents.push({
            id: `blocker-${b.id ?? b.name}-${dateStr}`,
            title: b.name,
            start: date.hour(sh!).minute(sm!).second(0).toDate(),
            end: date.hour(eh!).minute(em!).second(0).toDate(),
            backgroundColor: rgbToCss(getBlockerColor(), 0.15),
            borderColor: rgbToCss(getBlockerColor(), 0.5),
            textColor: readableTextColor(getBlockerColor()),
            classNames: ["blocker-event"],
            editable: false,
            extendedProps: { type: "blocker", blockerId: b.id, blockerName: b.name, blockerDays: b.daysOfWeek, blockerStart: b.startTime, blockerEnd: b.endTime },
          });
          date = date.add(7, "day");
        }
      }
    }
  }

  // Render one calendar event per scheduled task_block. Tasks without blocks
  // (e.g. newly created, not yet scheduled) only appear in the Upcoming list.
  // Fallback: if no blocks were fetched at all, render tasks the old way so the
  // calendar still shows something (e.g. for is_fixed tasks set manually).
  const taskIdsWithBlocks = new Set(blocks.map((b) => b.taskId));
  const blockEvents: EventInput[] = blocks.flatMap((b) => {
    const t = filteredTaskById.get(b.taskId);
    if (!t) return [];
    const c: RgbColor = t.org ? getOrgColor(t.org) : getOrgColor("");
    const isDarkTask = isDarkColor(c);
    void colorVersion; // reactive dependency
    return [{
      id: `${b.taskId}-${b.startDate.toISOString()}`,
      title: t.name,
      start: b.startDate,
      end: b.endDate,
      backgroundColor: rgbToCss(c, 0.22),
      borderColor: b.isFixed ? rgbToCss(c, 0.65) : rgbToCss(c, 0.45),
      textColor: readableTextColor(c),
      classNames: [
        "task-event",
        isDarkTask ? "is-dark-event-color" : "is-light-event-color",
        b.isFixed ? "task-fixed" : "",
        (t.status ?? "todo") === "done" ? "task-done" : "",
      ].filter(Boolean),
      editable: true,
      extendedProps: { task: t },
    }];
  });

  const taskFallbackEvents: EventInput[] = filteredTasks
    .filter((t) => t.startDate && t.endDate && !taskIdsWithBlocks.has(t.id ?? ""))
    .filter((t) => t.isFixed) // only show fixed/manual tasks without blocks
    .map((t) => {
      const c: RgbColor = t.org ? getOrgColor(t.org) : getOrgColor("");
      const isDarkTask = isDarkColor(c);
      void colorVersion;
      return {
        id: t.id ?? `task-${t.name}`,
        title: t.name,
        start: t.startDate,
        end: t.endDate,
        backgroundColor: rgbToCss(c, 0.22),
        borderColor: t.isFixed ? rgbToCss(c, 0.65) : rgbToCss(c, 0.45),
        textColor: readableTextColor(c),
        classNames: [
          "task-event",
          isDarkTask ? "is-dark-event-color" : "is-light-event-color",
          t.isFixed ? "task-fixed" : "",
          (t.status ?? "todo") === "done" ? "task-done" : "",
        ].filter(Boolean),
        editable: true,
        extendedProps: { task: t },
      };
    });

  // Auto-detect scheduled break gaps between consecutive task blocks (same day,
  // small gap <= 30 min) and render them as a plain block event showing only
  // the time range. The break end is shrunk by 1 second so it never *touches*
  // the next task block — that avoids FullCalendar treating the break and the
  // following task as overlapping and splitting their column into halves.
  const sortedBlocksForBreaks = [...blocks].sort(
    (a, b) => a.startDate.getTime() - b.startDate.getTime(),
  );
  const scheduledBreakEvents: EventInput[] = [];
  for (let i = 1; i < sortedBlocksForBreaks.length; i++) {
    const prev = sortedBlocksForBreaks[i - 1]!;
    const curr = sortedBlocksForBreaks[i]!;
    const gapMs = curr.startDate.getTime() - prev.endDate.getTime();
    const gapMin = Math.round(gapMs / 60000);
    if (gapMin < 5 || gapMin > 30) continue; // skip no-break and cross-slot gaps
    if (prev.endDate.toDateString() !== curr.startDate.toDateString()) continue;
    const breakC = getBreakColor();
    const isDarkBreak = isDarkColor(breakC);
    void colorVersion;
    scheduledBreakEvents.push({
      id: `auto-break-${prev.taskId}-${prev.endDate.toISOString()}`,
      title: "",
      start: prev.endDate,
      end: curr.startDate,
      // Background events render as a coloured stripe inside the time grid
      // without taking up a lane — that prevents FullCalendar from squeezing
      // the surrounding task blocks into half-width columns. Background
      // events still fire eventClick, so the pause can be clicked away.
      display: "background",
      backgroundColor: rgbToCss(breakC, 0.35),
      classNames: [
        "scheduled-break-event",
        isDarkBreak ? "is-dark-event-color" : "is-light-event-color",
      ],
      editable: false,
      overlap: false,
      displayEventTime: false,
      extendedProps: {
        type: "scheduled-break",
        durationMinutes: gapMin,
        nextTaskId: curr.taskId,
        nextStartIso: curr.startDate.toISOString(),
        gapMs: curr.startDate.getTime() - prev.endDate.getTime(),
      },
    });
  }

  const calendarEvents: EventInput[] = [
    ...blockEvents,
    ...scheduledBreakEvents,
    ...taskFallbackEvents,
  ];

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
    if (!task || !task.id) {
      arg.revert();
      return;
    }
    // When the user drags a task block, treat that as an explicit placement:
    // mark the task as fixed (so Auto-Schedule won't move it again) and use the
    // dragged event's new start/end as the task's window. Any auto-generated
    // blocks for this task are dropped locally so the fallback rendering uses
    // the task's own start/end instead of stale block positions.
    const newStart = arg.event.start!;
    const newEnd = arg.event.end ?? dayjs(newStart)
      .add(dayjs(task.endDate).diff(dayjs(task.startDate), "minute"), "minute")
      .toDate();
    const updatedTask: Task = {
      ...task,
      startDate: newStart,
      endDate: newEnd,
      isFixed: true,
    };
    saveTask(updatedTask)
      .then(() => setBlocks((prev) => prev.filter((b) => b.taskId !== task.id)))
      .catch(() => arg.revert());
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
    if (!task || !task.id) {
      arg.revert();
      return;
    }
    const newStart = arg.event.start!;
    const newEnd = arg.event.end!;
    const updatedTask: Task = {
      ...task,
      startDate: newStart,
      endDate: newEnd,
      isFixed: true,
    };
    saveTask(updatedTask)
      .then(() => setBlocks((prev) => prev.filter((b) => b.taskId !== task.id)))
      .catch(() => arg.revert());
  };

  const deleteBlocker = async () => {
    if (!editingBlocker || !workProfileId) return;
    const res = await fetch(
      `${API_BASE}/api/recurring-blocker/${workProfileId}/${editingBlocker.id}`,
      { method: "DELETE" },
    );
    if (res.ok) {
      setRecurringBlockers((prev) => prev.filter((b) => b.id !== editingBlocker.id));
      setEditingBlocker(null);
    }
  };

  const handleEventClick = (arg: EventClickArg) => {
    if (arg.event.extendedProps.type === "scheduled-break") {
      // Clicking an auto-generated break collapses the gap: shift the next
      // task block forward so it starts right where the previous one ended.
      const nextTaskId = arg.event.extendedProps.nextTaskId as string;
      const nextStartIso = arg.event.extendedProps.nextStartIso as string;
      const gapMs = arg.event.extendedProps.gapMs as number;
      setBlocks((prev) =>
        prev.map((b) =>
          b.taskId === nextTaskId &&
            b.startDate.toISOString() === nextStartIso
            ? {
              ...b,
              startDate: new Date(b.startDate.getTime() - gapMs),
              endDate: new Date(b.endDate.getTime() - gapMs),
            }
            : b,
        ),
      );
      return;
    }
    if (arg.event.extendedProps.type === "blocker") {
      const id = arg.event.extendedProps.blockerId as string | undefined;
      if (!id) return;
      setEditingBlocker({
        id,
        name: arg.event.extendedProps.blockerName as string,
        days: arg.event.extendedProps.blockerDays as string,
        startTime: arg.event.extendedProps.blockerStart as string,
        endTime: arg.event.extendedProps.blockerEnd as string,
      });
      return;
    }
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

  const changeOrganizationContext = (organizationId: string | "all") => {
    if (organizationId === "all") {
      setFilterOrgId("all");
      return;
    }

    setFilterOrgId(organizationId);
    void setActiveOrganization(organizationId).catch(() => {});
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
      intensity: (task.intensity ?? "normal") as Task["intensity"],
      organizationId: task.org,
      isFixed: !!task.isFixed,
      dependencies: (task.dependencies ?? [])
        .map((d) => d.id)
        .filter((id): id is string => !!id),
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
      // Preserve the user-defined deadline. The "End" field in the modal represents
      // the planned/fixed window (== earlyFinish after Auto-Schedule), not the deadline.
      // Overwriting deadline here would shrink it every time the user edits a task
      // after planning, so re-planning could never move the task earlier again.
      deadline: editingTask.deadline,
      priority: editForm.priority,
      status: editForm.status,
      intensity: editForm.intensity,
      org: editForm.organizationId,
      isFixed: editForm.isFixed,
      dependencies: (user.tasks ?? [])
        .filter(
          (t) =>
            t.id
            && editForm.dependencies.includes(t.id)
            && (t.org ?? "") === (editForm.organizationId ?? ""),
        ),
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
              events={[...calendarEvents, ...breakEvents, ...blockerEvents]}
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
            <div className="mt-3 flex justify-end">
              <div className="flex items-center gap-2 rounded-lg border border-slate-700/60 bg-slate-900/80 px-3 py-1.5 text-xs">
                <span className="text-[10px] font-semibold tracking-[0.14em] text-slate-500 uppercase">
                  Visible range
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
                  className="w-16 bg-transparent text-xs font-medium text-slate-100 outline-none"
                />
                <span className="text-slate-600">to</span>
                <input
                  type="time"
                  step={900}
                  value={plannerViewForm.endTime}
                  onChange={(e) => {
                    const endTime = e.target.value;
                    setPlannerViewForm((f) => ({ ...f, endTime }));
                    savePlannerView(plannerViewForm.startTime, endTime);
                  }}
                  className="w-16 bg-transparent text-xs font-medium text-slate-100 outline-none"
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
              setCalendarDialogOpen(true);
            }}
            className="mt-2 w-full rounded-2xl border border-dashed border-emerald-300/30 bg-emerald-400/5 py-3 text-xs font-semibold text-emerald-200/70 transition hover:border-emerald-300/60 hover:text-emerald-100"
          >
            + New task — or drag on calendar to set time
          </button>
        </div>
      </div>

      {calendarDialogOpen && (
        <CreateTaskModal
          onClose={() => {
            setCalendarDialogOpen(false);
          }}
          initialValues={{
            startDate: form.fixedStart || undefined,
            endDate: form.deadline || undefined,
            isFixed: form.isFixed,
          }}
          workProfileId={workProfileId ?? undefined}
        />
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

              <div className="flex flex-col gap-2">
                <label className="text-xs tracking-[0.14em] text-slate-500 uppercase">
                  Intensity
                </label>
                <select
                  value={editForm.intensity}
                  onChange={(e) =>
                    setEditForm({
                      ...editForm,
                      intensity: e.target.value as Task["intensity"],
                    })
                  }
                  className="rounded-xl border border-slate-800 bg-slate-900 px-3 py-2 text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
                >
                  <option value="light">Light</option>
                  <option value="normal">Normal</option>
                  <option value="intensive">Intensive</option>
                </select>
              </div>

              <div className="col-span-2 flex flex-col gap-2">
                <label className="text-xs tracking-[0.14em] text-slate-500 uppercase">
                  Dependencies
                </label>
                {/*
                  Only same-org tasks can be selected as dependencies. Cross-org
                  predecessors live in a different work profile and would be
                  silently filtered out by the backend DependencyAnalyzer, so
                  the planner would not respect them anyway.
                */}
                <div className="flex max-h-44 flex-col gap-2 overflow-y-auto rounded-xl border border-slate-800 bg-slate-900/60 p-3">
                  {(() => {
                    const depCandidates = (user.tasks ?? []).filter(
                      (t) =>
                        t.id
                        && t.id !== editingTask.id
                        && (t.org ?? "") === (editForm.organizationId ?? ""),
                    );
                    if (depCandidates.length === 0) {
                      return (
                        <span className="text-xs text-slate-500">
                          Keine weiteren Aufgaben in dieser Organisation verfügbar.
                        </span>
                      );
                    }
                    return depCandidates.map((dep) => {
                      const checked = editForm.dependencies.includes(dep.id!);
                      return (
                        <label
                          key={dep.id}
                          className="flex items-center gap-2 text-sm text-slate-200"
                        >
                          <input
                            type="checkbox"
                            checked={checked}
                            onChange={(e) => {
                              const next = e.target.checked
                                ? [...editForm.dependencies, dep.id!]
                                : editForm.dependencies.filter(
                                  (n) => n !== dep.id!,
                                );
                              setEditForm({
                                ...editForm,
                                dependencies: next,
                              });
                            }}
                          />
                          <span>{dep.name}</span>
                        </label>
                      );
                    });
                  })()}
                </div>
              </div>
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
                  className="rounded-full border border-emerald-300 bg-emerald-400 px-5 py-2 text-sm font-semibold text-slate-950 shadow-sm transition hover:bg-emerald-300"
                >
                  Save changes
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
      {editingBlocker && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm"
          onClick={() => setEditingBlocker(null)}
        >
          <div
            className="w-full max-w-sm rounded-3xl border border-violet-800/40 bg-slate-900 p-6 shadow-2xl"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="mb-4 flex items-center justify-between">
              <div>
                <div className="text-xs tracking-[0.18em] text-violet-300 uppercase">
                  Blocker
                </div>
                <div className="text-lg font-semibold text-slate-50">
                  {editingBlocker.name}
                </div>
                <div className="mt-0.5 text-sm text-slate-400">
                  {editingBlocker.days.split(",").join(", ")} &mdash; {editingBlocker.startTime}&ndash;{editingBlocker.endTime}
                </div>
              </div>
              <button
                onClick={() => setEditingBlocker(null)}
                className="rounded-full border border-slate-800 bg-slate-900 px-3 py-1 text-xs text-slate-300 hover:border-slate-500 hover:text-slate-100"
              >
                ✕
              </button>
            </div>
            <div className="flex justify-between gap-3 pt-2">
              <button
                onClick={deleteBlocker}
                className="rounded-full border border-rose-800/60 bg-rose-900/30 px-4 py-2 text-sm text-rose-300 hover:bg-rose-900/50"
              >
                Löschen
              </button>
              <button
                onClick={() => setEditingBlocker(null)}
                className="rounded-full border border-slate-800 bg-slate-900 px-4 py-2 text-sm text-slate-200 hover:border-slate-600"
              >
                Schließen
              </button>
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
