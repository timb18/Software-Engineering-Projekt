import { useMemo, useRef, useState, type CSSProperties, type FC } from "react";
import type {
  DateSelectArg,
  DayHeaderContentArg,
  EventClickArg,
  EventContentArg,
  EventDropArg,
  EventInput,
} from "@fullcalendar/core";
import FullCalendar from "@fullcalendar/react";
import interactionPlugin, { type EventResizeDoneArg } from "@fullcalendar/interaction";
import timeGridPlugin from "@fullcalendar/timegrid";
import type { User, WorkBreak, WorkWeekDay } from "../../util/types";
import { DAY_LABELS, WEEK_DAYS, getProductiveHoursForBlock, timeToMinutes } from "../../util/work-profile";
import { useWorkProfile } from "../../util/use-work-profile";
import "./work-profile-configurator.css";

type WorkProfileConfiguratorProps = {
  user: User;
  onSaveUser: (nextUser: User) => void;
  onStatusChange: (value: string | undefined) => void;
  onErrorChange: (value: string | undefined) => void;
  onDirtyChange?: (dirty: boolean) => void;
};

type PlannerViewForm = {
  startTime: string;
  endTime: string;
};

type PlannerViewWindow = {
  slotMinTime: string;
  slotMaxTime: string;
  scrollTime: string;
  visibleMinutes: number;
  validationError?: string;
  isFullDay: boolean;
};

const DEFAULT_PLANNER_VIEW_START = "06:00";
const DEFAULT_PLANNER_VIEW_END = "22:00";
const DEFAULT_CALENDAR_START_MINUTES = 6 * 60;
const DEFAULT_CALENDAR_END_MINUTES = 22 * 60;
const DEFAULT_HALF_HOUR_SLOTS_HEIGHT_PX = 600;
const MIN_SLOT_HEIGHT_PX = 12;
const MAX_SLOT_HEIGHT_PX = 22;
const MS_PER_DAY = 24 * 60 * 60 * 1000;
const REFERENCE_WEEK_MONDAY_MS = new Date(2025, 0, 6, 0, 0, 0, 0).getTime();

const DAY_INDEX_BY_KEY: Record<WorkWeekDay, number> = {
  Mon: 0,
  Tue: 1,
  Wed: 2,
  Thu: 3,
  Fri: 4,
  Sat: 5,
  Sun: 6,
};

const REFERENCE_WEEK_START = new Date(REFERENCE_WEEK_MONDAY_MS);
const REFERENCE_WEEK_END = new Date(REFERENCE_WEEK_MONDAY_MS + WEEK_DAYS.length * MS_PER_DAY);

const toTimeString = (date: Date) =>
  `${date.getHours().toString().padStart(2, "0")}:${date.getMinutes().toString().padStart(2, "0")}`;

const toCalendarDurationString = (minutes: number) =>
  `${Math.floor(minutes / 60)
    .toString()
    .padStart(2, "0")}:${(minutes % 60).toString().padStart(2, "0")}:00`;

const getReferenceDateForDay = (dayKey: WorkWeekDay) =>
  new Date(REFERENCE_WEEK_MONDAY_MS + DAY_INDEX_BY_KEY[dayKey] * MS_PER_DAY);

const getDateForShiftTime = (dayKey: WorkWeekDay, time: string) => {
  const [hours, minutes] = time.split(":").map(Number);
  const date = getReferenceDateForDay(dayKey);

  date.setHours(hours, minutes, 0, 0);
  return date;
};

const getWorkWeekDayFromDate = (date: Date): WorkWeekDay | undefined => {
  const dayStartMs = new Date(date.getFullYear(), date.getMonth(), date.getDate()).getTime();
  const dayIndex = Math.round((dayStartMs - REFERENCE_WEEK_MONDAY_MS) / MS_PER_DAY);

  if (dayIndex < 0 || dayIndex >= WEEK_DAYS.length) {
    return undefined;
  }

  return WEEK_DAYS[dayIndex];
};

const getSingleDayFromRange = (start: Date, end: Date): WorkWeekDay | undefined => {
  if (end <= start) {
    return undefined;
  }

  const startDay = getWorkWeekDayFromDate(start);
  const inclusiveEnd = new Date(end.getTime() - 1);
  const endDay = getWorkWeekDayFromDate(inclusiveEnd);

  if (!startDay || startDay !== endDay) {
    return undefined;
  }

  return startDay;
};

const normalizePlannerTime = (value: string | undefined, fallback: string) => {
  if (!value) {
    return fallback;
  }

  return Number.isNaN(timeToMinutes(value)) ? fallback : value;
};

const getPlannerViewWindow = (
  startTime: string | undefined,
  endTime: string | undefined,
): PlannerViewWindow => {
  const normalizedStart = normalizePlannerTime(startTime, DEFAULT_PLANNER_VIEW_START);
  const normalizedEnd = normalizePlannerTime(endTime, DEFAULT_PLANNER_VIEW_END);
  const startMinutes = timeToMinutes(normalizedStart);
  const endMinutes = timeToMinutes(normalizedEnd);

  if (startMinutes === endMinutes) {
    return {
      slotMinTime: "00:00:00",
      slotMaxTime: "24:00:00",
      scrollTime: "00:00:00",
      visibleMinutes: 24 * 60,
      isFullDay: true,
    };
  }

  if (endMinutes < startMinutes) {
    return {
      slotMinTime: toCalendarDurationString(DEFAULT_CALENDAR_START_MINUTES),
      slotMaxTime: toCalendarDurationString(DEFAULT_CALENDAR_END_MINUTES),
      scrollTime: toCalendarDurationString(DEFAULT_CALENDAR_START_MINUTES),
      visibleMinutes: DEFAULT_CALENDAR_END_MINUTES - DEFAULT_CALENDAR_START_MINUTES,
      validationError:
        "Visible end must be after visible start. Use the same start and end time for a 24-hour view.",
      isFullDay: false,
    };
  }

  return {
    slotMinTime: toCalendarDurationString(startMinutes),
    slotMaxTime: toCalendarDurationString(Math.max(startMinutes + 30, endMinutes)),
    scrollTime: toCalendarDurationString(startMinutes),
    visibleMinutes: endMinutes - startMinutes,
    isFullDay: false,
  };
};

const WorkProfileConfigurator: FC<WorkProfileConfiguratorProps> = ({
  user,
  onSaveUser,
  onStatusChange,
  onErrorChange,
  onDirtyChange,
}) => {
  const calendarRef = useRef<FullCalendar | null>(null);
  const [plannerViewForm, setPlannerViewForm] = useState<PlannerViewForm>(() => ({
    startTime: user.plannerViewStart ?? DEFAULT_PLANNER_VIEW_START,
    endTime: user.plannerViewEnd ?? DEFAULT_PLANNER_VIEW_END,
  }));

  const {
    workForm,
    companyOptions,
    workSummary,
    showEncouragement,
    pendingSelection,
    setPendingSelection,
    selectedShift,
    selectedBreak,
    selectedShiftDetails,
    selectedBreakDetails,
    copyDaySource,
    setCopyDaySource,
    copyDayPanelOpen,
    setCopyDayPanelOpen,
    copyDayTargets,
    setCopyDayTargets,
    copyEntryTargets,
    setCopyEntryTargets,
    selectRange,
    clearMessages,
    closeEntryDialog: closeEntryDialogState,
    selectShiftBlock,
    selectBreakEntry,
    findShiftLocation,
    moveBlock,
    moveBreak,
    updateWorkBlock,
    updateDayBreak,
    removeWorkBlock,
    removeBreakFromDay,
    updateBlockCompany,
    convertShiftToBreak,
    convertBreakToShift,
    commitPendingSelection: commitPendingSelectionAction,
    copyDayScheduleTo,
    copySingleShiftTo,
    copySingleBreakTo,
    toggleCopyDayTarget,
    toggleCopyEntryTarget,
    saveWork: saveWorkAction,
  } = useWorkProfile(user, { onSaveUser, onStatusChange, onErrorChange, onDirtyChange });

  const plannerViewWindow = useMemo(
    () => getPlannerViewWindow(plannerViewForm.startTime, plannerViewForm.endTime),
    [plannerViewForm.endTime, plannerViewForm.startTime],
  );
  const calendarSlotHeight = useMemo(() => {
    const slotCount = Math.max(1, plannerViewWindow.visibleMinutes / 30);

    return Math.min(
      MAX_SLOT_HEIGHT_PX,
      Math.max(MIN_SLOT_HEIGHT_PX, Math.round(DEFAULT_HALF_HOUR_SLOTS_HEIGHT_PX / slotCount)),
    );
  }, [plannerViewWindow.visibleMinutes]);
  const calendarStyle = useMemo<CSSProperties>(
    () =>
      ({
        "--work-planner-slot-height": `${calendarSlotHeight}px`,
      }) as CSSProperties,
    [calendarSlotHeight],
  );

  const breakLookup = useMemo(() => {
    const nextLookup = new Map<string, { dayKey: WorkWeekDay; workBreak: WorkBreak }>();

    workForm.days.forEach((day) => {
      day.breaks.forEach((workBreak) => {
        nextLookup.set(workBreak.id, { dayKey: day.day, workBreak });
      });
    });

    return nextLookup;
  }, [workForm.days]);

  const calendarEvents = useMemo<EventInput[]>(
    () => [
      // Background column hints for each empty day (encouragement)
      ...(showEncouragement
        ? workForm.days
            .filter((day) => day.blocks.length === 0)
            .map((day) => ({
              id: `hint-bg-${day.day}`,
              display: "background" as const,
              start: getDateForShiftTime(day.day, "00:00"),
              end: getDateForShiftTime(day.day, "24:00"),
              classNames: ["work-day-empty-hint"],
            }))
        : []),
      ...workForm.days.flatMap((day) =>
        day.blocks.map((block) => ({
          id: block.id,
          title: "",
          start: getDateForShiftTime(day.day, block.startTime),
          end: getDateForShiftTime(day.day, block.endTime),
          classNames: ["work-shift-event"],
          extendedProps: {
            label: companyOptions.find((c) => c.id === block.companyId)?.name ?? "Shift",
            companyColorIdx: Math.max(0, companyOptions.findIndex((c) => c.id === block.companyId)),
          },
        })),
      ),
      ...workForm.days.flatMap((day) =>
        day.breaks.map((workBreak) => ({
          id: `break-${workBreak.id}`,
          title: "",
          start: getDateForShiftTime(day.day, workBreak.startTime),
          end: getDateForShiftTime(day.day, workBreak.endTime),
          classNames: ["work-break-event"],
          extendedProps: { label: "Break" },
        })),
      ),
    ],
    [workForm.days, companyOptions, showEncouragement],
  );

  // ── FullCalendar wrappers ─────────────────────────────────────────────────

  const closeEntryDialog = () => {
    closeEntryDialogState();
    calendarRef.current?.getApi().unselect();
  };

  const commitPendingSelection = () => {
    commitPendingSelectionAction();
    calendarRef.current?.getApi().unselect();
  };

  const saveWork = () => {
    saveWorkAction(plannerViewForm.startTime, plannerViewForm.endTime, plannerViewWindow.validationError);
  };

  const handleCalendarSelect = (selectionInfo: DateSelectArg) => {
    const dayKey = getSingleDayFromRange(selectionInfo.start, selectionInfo.end);
    if (!dayKey) {
      clearMessages();
      selectionInfo.view.calendar.unselect();
      onErrorChange("A shift must stay within a single day.");
      return;
    }

    const error = selectRange(
      dayKey,
      toTimeString(selectionInfo.start),
      toTimeString(selectionInfo.end),
    );
    if (error) {
      clearMessages();
      selectionInfo.view.calendar.unselect();
      onErrorChange(error);
      return;
    }

    onStatusChange("Range selected. Choose whether to create a shift or a break in the pop-up.");
  };

  const handleCalendarEventClick = (clickInfo: EventClickArg) => {
    const eventId = clickInfo.event.id;
    if (eventId.startsWith("break-")) {
      const breakId = eventId.slice(6);
      const location = breakLookup.get(breakId);
      if (location) selectBreakEntry(location.dayKey, breakId);
      return;
    }
    const location = findShiftLocation(eventId);
    if (location) selectShiftBlock(location.day.day, location.block.id);
  };

  const handleEventDrop = (dropInfo: EventDropArg) => {
    const eventId = dropInfo.event.id;
    const newStart = dropInfo.event.start;
    const newEnd = dropInfo.event.end;
    if (!newStart || !newEnd) { dropInfo.revert(); return; }
    const targetDayKey = getWorkWeekDayFromDate(newStart);
    if (!targetDayKey || targetDayKey !== getSingleDayFromRange(newStart, newEnd)) { dropInfo.revert(); return; }
    const newStartTime = toTimeString(newStart);
    const newEndTime = toTimeString(newEnd);
    const success = eventId.startsWith("break-")
      ? moveBreak(eventId.slice(6), targetDayKey, newStartTime, newEndTime)
      : moveBlock(eventId, targetDayKey, newStartTime, newEndTime);
    if (!success) dropInfo.revert();
  };

  const handleEventResize = (resizeInfo: EventResizeDoneArg) => {
    const eventId = resizeInfo.event.id;
    const newStart = resizeInfo.event.start;
    const newEnd = resizeInfo.event.end;
    if (!newStart || !newEnd) { resizeInfo.revert(); return; }
    const dayKey = getWorkWeekDayFromDate(newStart);
    if (!dayKey) { resizeInfo.revert(); return; }
    const newStartTime = toTimeString(newStart);
    const newEndTime = toTimeString(newEnd);
    const success = eventId.startsWith("break-")
      ? moveBreak(eventId.slice(6), dayKey, newStartTime, newEndTime)
      : moveBlock(eventId, dayKey, newStartTime, newEndTime);
    if (!success) resizeInfo.revert();
  };

  const renderEventContent = (contentInfo: EventContentArg) => {
    return (
      <div className="work-shift-content">
        <div className="work-shift-time">{contentInfo.timeText}</div>
        <div className="work-shift-label">{contentInfo.event.extendedProps.label}</div>
      </div>
    );
  };

  const renderDayHeaderContent = (headerInfo: DayHeaderContentArg) => {
    const dayKey = getWorkWeekDayFromDate(headerInfo.date);

    if (!dayKey) {
      return <></>;
    }

    const dayHasBlocks = (workForm.days.find((d) => d.day === dayKey)?.blocks.length ?? 0) > 0;
    const showHint = showEncouragement && !dayHasBlocks;

    return (
      <div className="flex flex-col items-center gap-0.5 pb-1">
        <span className="work-day-header">{DAY_LABELS[dayKey]}</span>
        {showHint && (
          <span className="animate-pulse text-[10px] font-semibold tracking-wide text-emerald-300/70">
            create here ↓
          </span>
        )}
      </div>
    );
  };

  return (
    <div className="flex flex-col gap-5">
      <div className="rounded-3xl border border-slate-800 bg-slate-900/80 p-5 text-sm text-slate-300 shadow-lg">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div>
            <div className="text-sm font-semibold text-slate-100">Shift Planner</div>
            <p className="mt-2 max-w-3xl text-slate-400">
              Drag a range in the calendar and confirm in the pop-up whether it becomes a shift or
              a break. Move or resize shifts directly in the weekly view.
            </p>
          </div>
          <button
            type="button"
            onClick={saveWork}
            className="rounded-xl border border-emerald-300/60 bg-emerald-400/15 px-4 py-2 text-sm font-semibold text-emerald-100 shadow-sm transition hover:bg-emerald-400/25"
          >
            Save work profile
          </button>
        </div>

        <div className="mt-4 grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
          <div className="rounded-2xl border border-slate-800 bg-slate-950/60 p-3">
            <div className="text-[11px] uppercase tracking-[0.18em] text-slate-500">Active Days</div>
            <div className="mt-2 text-2xl font-semibold text-slate-50">{workSummary.activeDayCount}</div>
          </div>
          <div className="rounded-2xl border border-slate-800 bg-slate-950/60 p-3">
            <div className="text-[11px] uppercase tracking-[0.18em] text-slate-500">Weekly Hours</div>
            <div className="mt-2 text-2xl font-semibold text-slate-50">{workSummary.weeklyHours}</div>
          </div>
          <div className="rounded-2xl border border-slate-800 bg-slate-950/60 p-3">
            <div className="text-[11px] uppercase tracking-[0.18em] text-slate-500">Total Shifts</div>
            <div className="mt-2 text-2xl font-semibold text-slate-50">{workSummary.totalBlocks}</div>
          </div>
        </div>
      </div>

      <section className="rounded-3xl border border-slate-800 bg-slate-900/80 p-4 shadow-lg">
        <div className="mb-4 flex flex-wrap items-start justify-between gap-3">
          <div className="min-w-0 flex-1">
            <div className="text-sm font-semibold text-slate-100">Week View</div>
            <p className="mt-1 text-xs text-slate-400">
              Drag over empty space for a shift or fully inside an existing shift for a break.
            </p>
          </div>
          <div className="flex items-center gap-px rounded-2xl border border-slate-800 bg-slate-950/60 p-1 text-xs">
            {/* Visible range */}
            <div className="flex items-center gap-2 rounded-xl px-3 py-2">
              <span className="text-[11px] uppercase tracking-[0.14em] text-slate-500">Visible</span>
              <input
                type="time"
                step={900}
                value={plannerViewForm.startTime}
                onChange={(event) => {
                  clearMessages();
                  setPlannerViewForm((current) => ({
                    ...current,
                    startTime: event.target.value,
                  }));
                }}
                className="w-20 bg-transparent text-xs text-slate-100 outline-none"
              />
              <span className="text-slate-600">–</span>
              <input
                type="time"
                step={900}
                value={plannerViewForm.endTime}
                onChange={(event) => {
                  clearMessages();
                  setPlannerViewForm((current) => ({
                    ...current,
                    endTime: event.target.value,
                  }));
                }}
                className="w-20 bg-transparent text-xs text-slate-100 outline-none"
              />
            </div>

            {/* Divider */}
            <div className="mx-1 h-6 w-px bg-slate-800" />

            {/* Copy day toggle */}
            <button
              type="button"
              onClick={() => {
                if (copyDayPanelOpen) {
                  setCopyDayPanelOpen(false);
                  setCopyDaySource(undefined);
                  setCopyDayTargets([]);
                } else {
                  setCopyDayPanelOpen(true);
                }
              }}
              className={`rounded-xl border px-3 py-2 text-xs font-semibold transition ${
                copyDayPanelOpen
                  ? "border-slate-600 bg-slate-800 text-slate-200"
                  : "border-emerald-300/60 bg-emerald-400/15 text-emerald-100 hover:bg-emerald-400/25"
              }`}
            >
              {copyDayPanelOpen ? "✕ Close" : "Copy day"}
            </button>
          </div>
        </div>

        <div className="overflow-x-auto">
          {copyDayPanelOpen ? (
            <div className="mb-3 rounded-2xl border border-emerald-300/20 bg-emerald-400/8 px-4 py-3">
              {/* Step 1: pick source */}
              <div className="mb-2 text-[11px] font-semibold uppercase tracking-[0.14em] text-emerald-300">
                Step 1 — Which day do you want to copy from?
              </div>
              <div className="flex flex-wrap gap-1">
                {WEEK_DAYS.map((day) => {
                  const hasEntries =
                    (workForm.days.find((d) => d.day === day)?.blocks.length ?? 0) > 0 ||
                    (workForm.days.find((d) => d.day === day)?.breaks.length ?? 0) > 0;
                  return (
                    <button
                      key={day}
                      type="button"
                      disabled={!hasEntries}
                      onClick={() => setCopyDaySource(day === copyDaySource ? undefined : day)}
                      className={`rounded-full px-3 py-1 text-xs font-semibold transition ${
                        copyDaySource === day
                          ? "border border-emerald-300/60 bg-emerald-400/30 text-emerald-100"
                          : hasEntries
                            ? "border border-slate-700 bg-slate-900/60 text-slate-300 hover:border-emerald-300/40 hover:text-emerald-200"
                            : "cursor-not-allowed border border-slate-800 bg-slate-950/40 text-slate-600"
                      }`}
                    >
                      {DAY_LABELS[day]}
                    </button>
                  );
                })}
              </div>

              {/* Step 2: pick targets — only shown once a source is selected */}
              {copyDaySource && (
                <>
                  <div className="mb-2 mt-4 text-[11px] font-semibold uppercase tracking-[0.14em] text-emerald-300">
                    Step 2 — Copy to which days?
                  </div>
                  <div className="flex flex-wrap gap-1">
                    {WEEK_DAYS.filter((d) => d !== copyDaySource).map((day) => (
                      <button
                        key={day}
                        type="button"
                        onClick={() => toggleCopyDayTarget(day)}
                        className={`rounded-full px-3 py-1 text-xs font-semibold transition ${
                          copyDayTargets.includes(day)
                            ? "border border-emerald-300/60 bg-emerald-400/25 text-emerald-100"
                            : "border border-slate-700 bg-slate-900/60 text-slate-400 hover:border-emerald-300/30 hover:text-emerald-200"
                        }`}
                      >
                        {DAY_LABELS[day]}
                      </button>
                    ))}
                    <button
                      type="button"
                      onClick={() =>
                        setCopyDayTargets(
                          copyDayTargets.length === WEEK_DAYS.length - 1
                            ? []
                            : WEEK_DAYS.filter((d) => d !== copyDaySource),
                        )
                      }
                      className="rounded-full border border-slate-700 bg-slate-900/60 px-3 py-1 text-xs font-semibold text-slate-400 transition hover:border-emerald-300/30 hover:text-emerald-200"
                    >
                      {copyDayTargets.length === WEEK_DAYS.length - 1 ? "Deselect all" : "All"}
                    </button>
                  </div>
                  <div className="mt-3 flex gap-2">
                    <button
                      type="button"
                      disabled={copyDayTargets.length === 0}
                      onClick={() => copyDayScheduleTo(copyDaySource, copyDayTargets)}
                      className="rounded-full border border-emerald-300/60 bg-emerald-400/15 px-4 py-1.5 text-xs font-semibold text-emerald-100 transition hover:bg-emerald-400/25 disabled:cursor-not-allowed disabled:opacity-40"
                    >
                      Apply copy
                    </button>
                    <button
                      type="button"
                      onClick={() => { setCopyDayPanelOpen(false); setCopyDaySource(undefined); setCopyDayTargets([]); }}
                      className="rounded-full border border-slate-700 bg-slate-900/60 px-4 py-1.5 text-xs font-semibold text-slate-400 transition hover:border-slate-500 hover:text-slate-200"
                    >
                      Cancel
                    </button>
                  </div>
                </>
              )}
            </div>
          ) : null}
          <div className="relative">
            <div className="work-planner-calendar min-w-[70rem]" style={calendarStyle}>
            <FullCalendar
              ref={calendarRef}
              plugins={[timeGridPlugin, interactionPlugin]}
              initialView="timeGridWeek"
              initialDate={REFERENCE_WEEK_START}
              visibleRange={{ start: REFERENCE_WEEK_START, end: REFERENCE_WEEK_END }}
              headerToolbar={false}
              firstDay={1}
              weekends
              allDaySlot={false}
              height="auto"
              expandRows={false}
              editable
              eventStartEditable
              eventDurationEditable
              eventResizableFromStart
              selectable
              selectMirror
              selectMinDistance={10}
              eventOverlap={false}
              slotEventOverlap={false}
              selectOverlap
              nowIndicator={false}
              scrollTimeReset={false}
              slotDuration="00:30:00"
              snapDuration="00:15:00"
              slotLabelInterval="01:00:00"
              slotMinTime={plannerViewWindow.slotMinTime}
              slotMaxTime={plannerViewWindow.slotMaxTime}
              scrollTime={plannerViewWindow.scrollTime}
              events={calendarEvents}
              displayEventEnd
              eventTimeFormat={{
                hour: "2-digit",
                minute: "2-digit",
                meridiem: false,
                hour12: false,
              }}
              dayHeaderContent={renderDayHeaderContent}
              selectAllow={(selectionInfo) =>
                Boolean(getSingleDayFromRange(selectionInfo.start, selectionInfo.end))
              }
              select={handleCalendarSelect}
              eventDrop={handleEventDrop}
              eventResize={handleEventResize}
              eventClick={handleCalendarEventClick}
              eventContent={renderEventContent}
              eventClassNames={(eventInfo) => {
                const id = eventInfo.event.id;
                if (id.startsWith("break-")) {
                  const breakId = id.slice(6);
                  return selectedBreak?.breakId === breakId
                    ? ["work-break-event", "is-selected-shift"]
                    : ["work-break-event"];
                }
                const colorIdx: number = eventInfo.event.extendedProps.companyColorIdx ?? 0;
                const companyClass = `work-shift-company-${colorIdx}`;
                return selectedShift?.blockId === id
                  ? ["work-shift-event", companyClass, "is-selected-shift"]
                  : ["work-shift-event", companyClass];
              }}
            />
          </div>

        </div>
        </div>
      </section>

      {(pendingSelection || selectedShiftDetails || selectedBreakDetails) && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/70 px-4 py-6 backdrop-blur-sm"
          onClick={closeEntryDialog}
        >
          <div
            className="max-h-[90vh] w-full max-w-3xl overflow-y-auto rounded-[2rem] border border-slate-800 bg-slate-900 p-5 shadow-2xl"
            onClick={(event) => event.stopPropagation()}
          >
            <div className="flex items-start justify-between gap-4">
              <div>
                <div className="text-sm font-semibold text-slate-100">
                  {pendingSelection?.entryType === "break" || selectedBreakDetails
                    ? "Pause (Break)"
                    : "Shift"}
                </div>
                <p className="mt-1 text-xs text-slate-400">
                  {pendingSelection
                    ? "Adjust the settings below and confirm."
                    : "Edit the entry directly. Changes apply immediately."}
                </p>
              </div>
              <div className="flex items-center gap-2">
                {(pendingSelection || selectedShiftDetails) && (
                  <span className="rounded-full border border-emerald-300/20 bg-emerald-400/8 px-3 py-1 text-xs font-semibold text-emerald-100">
                    {DAY_LABELS[
                      pendingSelection?.dayKey ??
                        selectedShiftDetails!.day.day
                    ]}
                  </span>
                )}
                {selectedBreakDetails && (
                  <span className="rounded-full border border-amber-300/20 bg-amber-400/8 px-3 py-1 text-xs font-semibold text-amber-100">
                    {DAY_LABELS[selectedBreakDetails.day.day]}
                  </span>
                )}
                <button
                  type="button"
                  onClick={closeEntryDialog}
                  className="rounded-full border border-slate-700 bg-slate-950/70 px-3 py-1.5 text-xs font-semibold text-slate-300 transition hover:border-slate-500 hover:text-slate-100"
                >
                  Close
                </button>
              </div>
            </div>

            {/* ── Create or edit a SHIFT ── */}
            {(pendingSelection?.entryType === "shift" || (!pendingSelection && selectedShiftDetails)) && (
              <div className="mt-5 flex flex-col gap-4">
                {/* Entry type toggle */}
                <div className="flex items-center gap-1 rounded-xl border border-slate-800 bg-slate-950/70 p-1">
                  <button
                    type="button"
                    disabled
                    className="flex-1 rounded-lg bg-emerald-500/20 px-3 py-1.5 text-xs font-semibold text-emerald-100"
                  >
                    Shift
                  </button>
                  <button
                    type="button"
                    onClick={() => {
                      if (pendingSelection) {
                        setPendingSelection((current) =>
                          current ? { ...current, entryType: "break" } : current,
                        );
                      } else if (selectedShiftDetails) {
                        convertShiftToBreak(
                          selectedShiftDetails.day.day,
                          selectedShiftDetails.block.id,
                        );
                      }
                    }}
                    className="flex-1 rounded-lg px-3 py-1.5 text-xs font-semibold text-slate-400 transition hover:bg-slate-800 hover:text-slate-200"
                  >
                    Break
                  </button>
                </div>

                <div className="grid gap-2">
                  <label className="text-[11px] uppercase tracking-[0.14em] text-slate-500">
                    Company
                  </label>
                  <select
                    value={
                      pendingSelection
                        ? pendingSelection.companyId
                        : selectedShiftDetails!.block.companyId
                    }
                    onChange={(event) => {
                      if (pendingSelection) {
                        setPendingSelection((current) =>
                          current ? { ...current, companyId: event.target.value } : current,
                        );
                      } else if (selectedShiftDetails) {
                        updateBlockCompany(
                          selectedShiftDetails.day.day,
                          selectedShiftDetails.block.id,
                          event.target.value,
                        );
                      }
                    }}
                    className="rounded-xl border border-slate-800 bg-slate-950/70 px-3 py-2 text-sm text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
                  >
                    {companyOptions.map((company) => (
                      <option key={company.id} value={company.id}>
                        {company.name}
                      </option>
                    ))}
                  </select>
                </div>

                <div className="grid gap-3 sm:grid-cols-2">
                  <div className="flex flex-col gap-1">
                    <label className="text-[11px] uppercase tracking-[0.14em] text-slate-500">
                      Start time
                    </label>
                    <input
                      type="time"
                      value={
                        pendingSelection
                          ? pendingSelection.startTime
                          : selectedShiftDetails!.block.startTime
                      }
                      onChange={(event) => {
                        if (pendingSelection) {
                          setPendingSelection((current) =>
                            current ? { ...current, startTime: event.target.value } : current,
                          );
                        } else if (selectedShiftDetails) {
                          updateWorkBlock(
                            selectedShiftDetails.day.day,
                            selectedShiftDetails.block.id,
                            (b) => ({ ...b, startTime: event.target.value }),
                          );
                        }
                      }}
                      className="rounded-xl border border-slate-800 bg-slate-950/70 px-3 py-2 text-sm text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
                    />
                  </div>
                  <div className="flex flex-col gap-1">
                    <label className="text-[11px] uppercase tracking-[0.14em] text-slate-500">
                      End time
                    </label>
                    <input
                      type="time"
                      value={
                        pendingSelection
                          ? pendingSelection.endTime
                          : selectedShiftDetails!.block.endTime
                      }
                      onChange={(event) => {
                        if (pendingSelection) {
                          setPendingSelection((current) =>
                            current ? { ...current, endTime: event.target.value } : current,
                          );
                        } else if (selectedShiftDetails) {
                          updateWorkBlock(
                            selectedShiftDetails.day.day,
                            selectedShiftDetails.block.id,
                            (b) => ({ ...b, endTime: event.target.value }),
                          );
                        }
                      }}
                      className="rounded-xl border border-slate-800 bg-slate-950/70 px-3 py-2 text-sm text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
                    />
                  </div>
                </div>

                {!pendingSelection && selectedShiftDetails && (
                  <div className="flex flex-wrap gap-2 text-xs text-slate-400">
                    <span className="rounded-full border border-slate-800 bg-slate-950/70 px-2 py-1">
                      {selectedShiftDetails.block.startTime} – {selectedShiftDetails.block.endTime}
                    </span>
                    <span className="rounded-full border border-slate-800 bg-slate-950/70 px-2 py-1">
                      {getProductiveHoursForBlock(selectedShiftDetails.block)} h
                    </span>
                  </div>
                )}

                <div className="flex flex-wrap gap-2">
                  {pendingSelection ? (
                    <>
                      <button
                        type="button"
                        onClick={commitPendingSelection}
                        className="rounded-full border border-emerald-300/60 bg-emerald-400/15 px-3 py-2 text-xs font-semibold text-emerald-100 transition hover:bg-emerald-400/25"
                      >
                        Create shift
                      </button>
                      <button
                        type="button"
                        onClick={closeEntryDialog}
                        className="rounded-full border border-slate-700 bg-slate-950/70 px-3 py-2 text-xs font-semibold text-slate-300 transition hover:border-slate-500 hover:text-slate-100"
                      >
                        Cancel
                      </button>
                    </>
                  ) : (
                    <>
                      {/* Copy shift to other days */}
                      <div className="w-full">
                        <div className="mb-1 text-[11px] uppercase tracking-[0.14em] text-slate-500">
                          Copy to other days
                        </div>
                        <div className="flex flex-wrap gap-1">
                          {WEEK_DAYS.filter((d) => d !== selectedShiftDetails!.day.day).map((day) => (
                            <button
                              key={day}
                              type="button"
                              onClick={() => toggleCopyEntryTarget(day)}
                              className={`rounded-full px-3 py-1 text-xs font-semibold transition ${
                                copyEntryTargets.includes(day)
                                  ? "border border-emerald-300/60 bg-emerald-400/25 text-emerald-100"
                                  : "border border-slate-700 bg-slate-900/60 text-slate-400 hover:border-emerald-300/30 hover:text-emerald-200"
                              }`}
                            >
                              {DAY_LABELS[day]}
                            </button>
                          ))}
                          <button
                            type="button"
                            onClick={() =>
                              setCopyEntryTargets(
                                WEEK_DAYS.filter((d) => d !== selectedShiftDetails!.day.day),
                              )
                            }
                            className="rounded-full border border-slate-700 bg-slate-900/60 px-3 py-1 text-xs font-semibold text-slate-400 transition hover:border-emerald-300/30 hover:text-emerald-200"
                          >
                            All
                          </button>
                          {copyEntryTargets.length > 0 && (
                            <button
                              type="button"
                              onClick={() =>
                                copySingleShiftTo(selectedShiftDetails!.block, copyEntryTargets)
                              }
                              className="rounded-full border border-emerald-300/60 bg-emerald-400/15 px-3 py-1 text-xs font-semibold text-emerald-100 transition hover:bg-emerald-400/25"
                            >
                              Copy shift
                            </button>
                          )}
                        </div>
                      </div>
                      <button
                        type="button"
                        onClick={() =>
                          removeWorkBlock(
                            selectedShiftDetails!.day.day,
                            selectedShiftDetails!.block.id,
                          )
                        }
                        className="rounded-full border border-slate-700 bg-slate-950/70 px-3 py-2 text-xs font-semibold text-slate-300 transition hover:border-rose-300/40 hover:text-rose-100"
                      >
                        Remove shift
                      </button>
                    </>
                  )}
                </div>
              </div>
            )}

            {/* ── Create or edit a BREAK ── */}
            {(pendingSelection?.entryType === "break" || (!pendingSelection && selectedBreakDetails)) && (
              <div className="mt-5 flex flex-col gap-4">
                {/* Entry type toggle */}
                <div className="flex items-center gap-1 rounded-xl border border-slate-800 bg-slate-950/70 p-1">
                  <button
                    type="button"
                    onClick={() => {
                      if (pendingSelection) {
                        setPendingSelection((current) =>
                          current ? { ...current, entryType: "shift" } : current,
                        );
                      } else if (selectedBreakDetails) {
                        convertBreakToShift(
                          selectedBreakDetails.day.day,
                          selectedBreakDetails.workBreak.id,
                        );
                      }
                    }}
                    className="flex-1 rounded-lg px-3 py-1.5 text-xs font-semibold text-slate-400 transition hover:bg-slate-800 hover:text-slate-200"
                  >
                    Shift
                  </button>
                  <button
                    type="button"
                    disabled
                    className="flex-1 rounded-lg bg-amber-500/20 px-3 py-1.5 text-xs font-semibold text-amber-100"
                  >
                    Break
                  </button>
                </div>

                <div className="grid gap-3 sm:grid-cols-2">
                  <div className="flex flex-col gap-1">
                    <label className="text-[11px] uppercase tracking-[0.14em] text-slate-500">
                      Start time
                    </label>
                    <input
                      type="time"
                      value={
                        pendingSelection
                          ? pendingSelection.startTime
                          : selectedBreakDetails!.workBreak.startTime
                      }
                      onChange={(event) => {
                        if (pendingSelection) {
                          setPendingSelection((current) =>
                            current ? { ...current, startTime: event.target.value } : current,
                          );
                        } else if (selectedBreakDetails) {
                          updateDayBreak(
                            selectedBreakDetails.day.day,
                            selectedBreakDetails.workBreak.id,
                            (wb) => ({ ...wb, startTime: event.target.value }),
                          );
                        }
                      }}
                      className="rounded-xl border border-slate-800 bg-slate-950/70 px-3 py-2 text-sm text-slate-50 ring-amber-400/40 outline-none focus:border-amber-400/60 focus:ring"
                    />
                  </div>
                  <div className="flex flex-col gap-1">
                    <label className="text-[11px] uppercase tracking-[0.14em] text-slate-500">
                      End time
                    </label>
                    <input
                      type="time"
                      value={
                        pendingSelection
                          ? pendingSelection.endTime
                          : selectedBreakDetails!.workBreak.endTime
                      }
                      onChange={(event) => {
                        if (pendingSelection) {
                          setPendingSelection((current) =>
                            current ? { ...current, endTime: event.target.value } : current,
                          );
                        } else if (selectedBreakDetails) {
                          updateDayBreak(
                            selectedBreakDetails.day.day,
                            selectedBreakDetails.workBreak.id,
                            (wb) => ({ ...wb, endTime: event.target.value }),
                          );
                        }
                      }}
                      className="rounded-xl border border-slate-800 bg-slate-950/70 px-3 py-2 text-sm text-slate-50 ring-amber-400/40 outline-none focus:border-amber-400/60 focus:ring"
                    />
                  </div>
                </div>

                {!pendingSelection && selectedBreakDetails && (
                  <div className="flex flex-wrap gap-2 text-xs text-slate-400">
                    <span className="rounded-full border border-slate-800 bg-slate-950/70 px-2 py-1">
                      {selectedBreakDetails.workBreak.startTime} – {selectedBreakDetails.workBreak.endTime}
                    </span>
                  </div>
                )}

                <div className="flex flex-wrap gap-2">
                  {pendingSelection ? (
                    <>
                      <button
                        type="button"
                        onClick={commitPendingSelection}
                        className="rounded-full border border-amber-300/60 bg-amber-400/15 px-3 py-2 text-xs font-semibold text-amber-100 transition hover:bg-amber-400/25"
                      >
                        Create break
                      </button>
                      <button
                        type="button"
                        onClick={closeEntryDialog}
                        className="rounded-full border border-slate-700 bg-slate-950/70 px-3 py-2 text-xs font-semibold text-slate-300 transition hover:border-slate-500 hover:text-slate-100"
                      >
                        Cancel
                      </button>
                    </>
                  ) : (
                    <>
                      {/* Copy break to other days */}
                      <div className="w-full">
                        <div className="mb-1 text-[11px] uppercase tracking-[0.14em] text-slate-500">
                          Copy to other days
                        </div>
                        <div className="flex flex-wrap gap-1">
                          {WEEK_DAYS.filter((d) => d !== selectedBreakDetails!.day.day).map((day) => (
                            <button
                              key={day}
                              type="button"
                              onClick={() => toggleCopyEntryTarget(day)}
                              className={`rounded-full px-3 py-1 text-xs font-semibold transition ${
                                copyEntryTargets.includes(day)
                                  ? "border border-amber-300/60 bg-amber-400/25 text-amber-100"
                                  : "border border-slate-700 bg-slate-900/60 text-slate-400 hover:border-amber-300/30 hover:text-amber-200"
                              }`}
                            >
                              {DAY_LABELS[day]}
                            </button>
                          ))}
                          <button
                            type="button"
                            onClick={() =>
                              setCopyEntryTargets(
                                WEEK_DAYS.filter((d) => d !== selectedBreakDetails!.day.day),
                              )
                            }
                            className="rounded-full border border-slate-700 bg-slate-900/60 px-3 py-1 text-xs font-semibold text-slate-400 transition hover:border-amber-300/30 hover:text-amber-200"
                          >
                            All
                          </button>
                          {copyEntryTargets.length > 0 && (
                            <button
                              type="button"
                              onClick={() =>
                                copySingleBreakTo(selectedBreakDetails!.workBreak, copyEntryTargets)
                              }
                              className="rounded-full border border-amber-300/60 bg-amber-400/15 px-3 py-1 text-xs font-semibold text-amber-100 transition hover:bg-amber-400/25"
                            >
                              Copy break
                            </button>
                          )}
                        </div>
                      </div>
                      <button
                        type="button"
                        onClick={() =>
                          removeBreakFromDay(
                            selectedBreakDetails!.day.day,
                            selectedBreakDetails!.workBreak.id,
                          )
                        }
                        className="rounded-full border border-slate-700 bg-slate-950/70 px-3 py-2 text-xs font-semibold text-slate-300 transition hover:border-rose-300/40 hover:text-rose-100"
                      >
                        Remove break
                      </button>
                    </>
                  )}
                </div>
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
};

export default WorkProfileConfigurator;