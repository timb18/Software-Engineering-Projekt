import { useEffect, useMemo, useState } from "react";
import type { User, WorkBlock, WorkBreak, WorkWeekDay } from "./types";
import {
  DAY_LABELS,
  createWorkBreak,
  createWorkBlock,
  createWorkProfileFromLegacyUser,
  getCompanyOptions,
  getLegacyWorkSettings,
  getWorkProfileSummary,
  normalizeWorkProfile,
  timeToMinutes,
  validateWorkProfile,
} from "./work-profile";

// ── Types ────────────────────────────────────────────────────────────────────

export type PendingSelectionType = "shift" | "break";

export type PendingSelection = {
  dayKey: WorkWeekDay;
  startTime: string;
  endTime: string;
  entryType: PendingSelectionType;
  companyId: string;
};

export type SelectedShift = {
  dayKey: WorkWeekDay;
  blockId: string;
};

export type SelectedBreak = {
  dayKey: WorkWeekDay;
  breakId: string;
};

export type UseWorkProfileCallbacks = {
  onSaveUser: (nextUser: User) => void;
  onStatusChange: (value: string | undefined) => void;
  onErrorChange: (value: string | undefined) => void;
  onDirtyChange?: (dirty: boolean) => void;
};

// ── Pure helpers (exported so callers can reuse them) ─────────────────────────

export const sortBreaks = (breaks: WorkBreak[]) =>
  [...breaks].sort(
    (left, right) => timeToMinutes(left.startTime) - timeToMinutes(right.startTime),
  );

export const sortBlocks = (blocks: WorkBlock[]) =>
  [...blocks].sort(
    (left, right) => timeToMinutes(left.startTime) - timeToMinutes(right.startTime),
  );

export const blocksOverlap = (
  blocks: WorkBlock[],
  candidateStartMinutes: number,
  candidateEndMinutes: number,
  ignoredBlockId?: string,
) =>
  blocks.some((block) => {
    if (block.id === ignoredBlockId) return false;

    const blockStartMinutes = timeToMinutes(block.startTime);
    const blockEndMinutes = timeToMinutes(block.endTime);

    if (
      Number.isNaN(blockStartMinutes) ||
      Number.isNaN(blockEndMinutes) ||
      blockEndMinutes <= blockStartMinutes
    ) {
      return false;
    }

    return candidateStartMinutes < blockEndMinutes && candidateEndMinutes > blockStartMinutes;
  });

export const breaksOverlap = (
  breaks: WorkBreak[],
  candidateStartMinutes: number,
  candidateEndMinutes: number,
  ignoredBreakId?: string,
) =>
  breaks.some((workBreak) => {
    if (workBreak.id === ignoredBreakId) return false;

    const breakStartMinutes = timeToMinutes(workBreak.startTime);
    const breakEndMinutes = timeToMinutes(workBreak.endTime);

    if (
      Number.isNaN(breakStartMinutes) ||
      Number.isNaN(breakEndMinutes) ||
      breakEndMinutes <= breakStartMinutes
    ) {
      return false;
    }

    return candidateStartMinutes < breakEndMinutes && candidateEndMinutes > breakStartMinutes;
  });

const getShiftValidationError = (dayKey: WorkWeekDay, blocks: WorkBlock[]) => {
  const sortedBlocks = sortBlocks(blocks);
  let previousBlockEnd = -1;

  for (const block of sortedBlocks) {
    const startMinutes = timeToMinutes(block.startTime);
    const endMinutes = timeToMinutes(block.endTime);

    if (Number.isNaN(startMinutes) || Number.isNaN(endMinutes) || endMinutes <= startMinutes) {
      return `${DAY_LABELS[dayKey]} shift must end after it starts.`;
    }

    if (previousBlockEnd > startMinutes) {
      return `${DAY_LABELS[dayKey]} already contains another overlapping shift.`;
    }

    previousBlockEnd = endMinutes;
  }

  return undefined;
};

// ── Hook ──────────────────────────────────────────────────────────────────────

export function useWorkProfile(user: User, callbacks: UseWorkProfileCallbacks) {
  const { onSaveUser, onStatusChange, onErrorChange, onDirtyChange } = callbacks;

  const [workForm, setWorkForm] = useState(() => createWorkProfileFromLegacyUser(user));
  const [pendingSelection, setPendingSelection] = useState<PendingSelection | undefined>();
  const [selectedShift, setSelectedShift] = useState<SelectedShift | undefined>();
  const [selectedBreak, setSelectedBreak] = useState<SelectedBreak | undefined>();
  const [copyDaySource, setCopyDaySource] = useState<WorkWeekDay | undefined>();
  const [copyDayPanelOpen, setCopyDayPanelOpen] = useState(false);
  const [copyDayTargets, setCopyDayTargets] = useState<WorkWeekDay[]>([]);
  const [copyEntryTargets, setCopyEntryTargets] = useState<WorkWeekDay[]>([]);

  const companyOptions = useMemo(
    () => getCompanyOptions(user.teams ?? [], workForm),
    [user.teams, workForm],
  );
  const workSummary = useMemo(() => getWorkProfileSummary(workForm), [workForm]);

  const savedWorkProfile = useMemo(() => createWorkProfileFromLegacyUser(user), [user]);
  const isDirty = useMemo(
    () =>
      JSON.stringify(normalizeWorkProfile(workForm)) !==
      JSON.stringify(normalizeWorkProfile(savedWorkProfile)),
    [workForm, savedWorkProfile],
  );

  useEffect(() => {
    onDirtyChange?.(isDirty);
  }, [isDirty, onDirtyChange]);

  useEffect(
    () => () => {
      onDirtyChange?.(false);
    },
    [onDirtyChange],
  );

  const showEncouragement = workSummary.totalBlocks === 0;

  const selectedShiftDetails = useMemo(() => {
    if (!selectedShift) return undefined;
    const day = workForm.days.find((entry) => entry.day === selectedShift.dayKey);
    const block = day?.blocks.find((entry) => entry.id === selectedShift.blockId);
    if (!day || !block) return undefined;
    return { day, block };
  }, [selectedShift, workForm.days]);

  const selectedBreakDetails = useMemo(() => {
    if (!selectedBreak) return undefined;
    const day = workForm.days.find((entry) => entry.day === selectedBreak.dayKey);
    const workBreak = day?.breaks.find((entry) => entry.id === selectedBreak.breakId);
    if (!day || !workBreak) return undefined;
    return { day, workBreak };
  }, [selectedBreak, workForm.days]);

  // ── Internal helpers ────────────────────────────────────────────────────────

  const clearMessages = () => {
    onStatusChange(undefined);
    onErrorChange(undefined);
  };

  const clearPendingSelectionState = () => {
    setPendingSelection(undefined);
  };

  const replaceDayBlocks = (dayKey: WorkWeekDay, nextBlocks: WorkBlock[]) => {
    setWorkForm((current) => ({
      days: current.days.map((day) =>
        day.day === dayKey ? { ...day, blocks: sortBlocks(nextBlocks) } : day,
      ),
    }));
  };

  const replaceDayBreaks = (dayKey: WorkWeekDay, nextBreaks: WorkBreak[]) => {
    setWorkForm((current) => ({
      days: current.days.map((day) =>
        day.day === dayKey ? { ...day, breaks: sortBreaks(nextBreaks) } : day,
      ),
    }));
  };

  const applyValidatedDayBlocks = (dayKey: WorkWeekDay, nextBlocks: WorkBlock[]) => {
    const validationError = getShiftValidationError(dayKey, nextBlocks);
    if (validationError) {
      clearMessages();
      onErrorChange(validationError);
      return false;
    }
    clearMessages();
    replaceDayBlocks(dayKey, nextBlocks);
    return true;
  };

  // ── Lookup helpers ──────────────────────────────────────────────────────────

  const findShiftLocation = (blockId: string) => {
    for (const day of workForm.days) {
      const block = day.blocks.find((entry) => entry.id === blockId);
      if (block) return { day, block };
    }
    return undefined;
  };

  const findBreakLocation = (breakId: string) => {
    for (const day of workForm.days) {
      const workBreak = day.breaks.find((entry) => entry.id === breakId);
      if (workBreak) return { day, workBreak };
    }
    return undefined;
  };

  // ── Selection ───────────────────────────────────────────────────────────────

  const selectShiftBlock = (dayKey: WorkWeekDay, blockId: string) => {
    clearMessages();
    clearPendingSelectionState();
    setSelectedBreak(undefined);
    setSelectedShift({ dayKey, blockId });
  };

  const selectBreakEntry = (dayKey: WorkWeekDay, breakId: string) => {
    clearMessages();
    clearPendingSelectionState();
    setSelectedShift(undefined);
    setSelectedBreak({ dayKey, breakId });
  };

  /**
   * Initiates a pending selection for a calendar range.
   * Returns an error string when the range is invalid or fully occupied,
   * or undefined when the pending selection was created successfully.
   */
  const selectRange = (
    dayKey: WorkWeekDay,
    startTime: string,
    endTime: string,
  ): string | undefined => {
    const startMinutes = timeToMinutes(startTime);
    const endMinutes = timeToMinutes(endTime);

    if (Number.isNaN(startMinutes) || Number.isNaN(endMinutes) || endMinutes <= startMinutes) {
      return "Invalid time range.";
    }

    const day = workForm.days.find((entry) => entry.day === dayKey);
    if (!day) return "Day not found.";

    const shiftOverlap = blocksOverlap(day.blocks, startMinutes, endMinutes);
    const breakOverlap = breaksOverlap(day.breaks, startMinutes, endMinutes);

    if (shiftOverlap && breakOverlap) {
      return `${DAY_LABELS[dayKey]} already has both a shift and a break in this range. Remove the existing entry first.`;
    }

    clearMessages();
    setSelectedShift(undefined);
    setSelectedBreak(undefined);
    setPendingSelection({
      dayKey,
      startTime,
      endTime,
      entryType: shiftOverlap ? "break" : "shift",
      companyId: companyOptions[0]?.id ?? "",
    });

    return undefined;
  };

  const closeEntryDialog = () => {
    clearMessages();
    clearPendingSelectionState();
    setSelectedShift(undefined);
    setSelectedBreak(undefined);
    setCopyEntryTargets([]);
  };

  // ── Mutations ───────────────────────────────────────────────────────────────

  const updateWorkBlock = (
    dayKey: WorkWeekDay,
    blockId: string,
    updater: (block: WorkBlock) => WorkBlock,
  ) => {
    const day = workForm.days.find((entry) => entry.day === dayKey);
    if (!day) return;

    const nextBlocks = sortBlocks(
      day.blocks.map((block) => (block.id === blockId ? updater(block) : block)),
    );

    applyValidatedDayBlocks(dayKey, nextBlocks);
  };

  const updateDayBreak = (
    dayKey: WorkWeekDay,
    breakId: string,
    updater: (workBreak: WorkBreak) => WorkBreak,
  ) => {
    clearMessages();
    setWorkForm((current) => ({
      days: current.days.map((day) => {
        if (day.day !== dayKey) return day;
        return {
          ...day,
          breaks: sortBreaks(
            day.breaks.map((workBreak) =>
              workBreak.id === breakId ? updater(workBreak) : workBreak,
            ),
          ),
        };
      }),
    }));
  };

  const removeBreakFromDay = (dayKey: WorkWeekDay, breakId: string) => {
    clearMessages();
    const day = workForm.days.find((entry) => entry.day === dayKey);
    if (!day) return;
    replaceDayBreaks(dayKey, day.breaks.filter((workBreak) => workBreak.id !== breakId));
    setSelectedBreak(undefined);
  };

  const updateBlockCompany = (dayKey: WorkWeekDay, blockId: string, companyId: string) => {
    const company = companyOptions.find((option) => option.id === companyId);
    if (!company) return;
    clearMessages();
    setWorkForm((current) => ({
      days: current.days.map((day) => {
        if (day.day !== dayKey) return day;
        return {
          ...day,
          blocks: day.blocks.map((block) =>
            block.id === blockId
              ? { ...block, companyId: company.id, companyName: company.name }
              : block,
          ),
        };
      }),
    }));
  };

  const removeWorkBlock = (dayKey: WorkWeekDay, blockId: string) => {
    clearMessages();
    clearPendingSelectionState();
    const day = workForm.days.find((entry) => entry.day === dayKey);
    if (!day) return;
    replaceDayBlocks(dayKey, day.blocks.filter((block) => block.id !== blockId));
    setSelectedShift(undefined);
  };

  const convertShiftToBreak = (dayKey: WorkWeekDay, blockId: string) => {
    const day = workForm.days.find((entry) => entry.day === dayKey);
    const block = day?.blocks.find((b) => b.id === blockId);
    if (!day || !block) return;
    clearMessages();
    replaceDayBlocks(dayKey, day.blocks.filter((b) => b.id !== blockId));
    const newBreak = createWorkBreak(block.startTime, block.endTime);
    replaceDayBreaks(dayKey, [...day.breaks, newBreak]);
    setSelectedShift(undefined);
    setSelectedBreak({ dayKey, breakId: newBreak.id });
    onStatusChange("Shift converted to break.");
  };

  const convertBreakToShift = (dayKey: WorkWeekDay, breakId: string) => {
    const day = workForm.days.find((entry) => entry.day === dayKey);
    const wb = day?.breaks.find((b) => b.id === breakId);
    if (!day || !wb) return;
    clearMessages();
    replaceDayBreaks(dayKey, day.breaks.filter((b) => b.id !== breakId));
    const company = companyOptions[0];
    if (!company) return;
    const newBlock = createWorkBlock(company, wb.startTime, wb.endTime);
    replaceDayBlocks(dayKey, [...day.blocks, newBlock]);
    setSelectedBreak(undefined);
    setSelectedShift({ dayKey, blockId: newBlock.id });
    onStatusChange("Break converted to shift.");
  };

  // ── Create from confirmed pending selection ─────────────────────────────────

  /**
   * Adds a shift to the given day. Works with plain time strings — no FullCalendar
   * date objects needed, making it safe to call from any view.
   */
  const addShift = (
    dayKey: WorkWeekDay,
    startTime: string,
    endTime: string,
    companyId = companyOptions[0]?.id,
  ) => {
    const day = workForm.days.find((entry) => entry.day === dayKey);
    if (!day) return;

    const startMinutes = timeToMinutes(startTime);
    const endMinutes = timeToMinutes(endTime);

    clearMessages();

    if (Number.isNaN(startMinutes) || Number.isNaN(endMinutes) || endMinutes <= startMinutes) {
      onErrorChange(`${DAY_LABELS[dayKey]} shift must end after it starts.`);
      return;
    }

    if (blocksOverlap(day.blocks, startMinutes, endMinutes)) {
      onErrorChange(`${DAY_LABELS[dayKey]} already contains another overlapping shift.`);
      return;
    }

    const company = companyOptions.find((option) => option.id === companyId) ?? companyOptions[0];
    const newBlock = createWorkBlock(company, startTime, endTime);
    replaceDayBlocks(dayKey, [...day.blocks, newBlock]);
    clearPendingSelectionState();
    onStatusChange(`${DAY_LABELS[dayKey]} shift created for ${startTime} - ${endTime}.`);
  };

  /**
   * Adds a break to the given day. Works with plain time strings — no FullCalendar
   * date objects needed, making it safe to call from any view.
   */
  const addBreak = (dayKey: WorkWeekDay, startTime: string, endTime: string) => {
    const day = workForm.days.find((entry) => entry.day === dayKey);
    if (!day) return;

    const startMinutes = timeToMinutes(startTime);
    const endMinutes = timeToMinutes(endTime);

    clearMessages();

    if (Number.isNaN(startMinutes) || Number.isNaN(endMinutes) || endMinutes <= startMinutes) {
      onErrorChange("Break must end after it starts.");
      return;
    }

    if (breaksOverlap(day.breaks, startMinutes, endMinutes)) {
      onErrorChange(`${DAY_LABELS[dayKey]} already contains an overlapping break.`);
      return;
    }

    const newBreak = createWorkBreak(startTime, endTime);
    replaceDayBreaks(dayKey, [...day.breaks, newBreak]);
    clearPendingSelectionState();
    onStatusChange(`Break added for ${DAY_LABELS[dayKey]}: ${startTime} - ${endTime}.`);
  };

  const commitPendingSelection = () => {
    if (!pendingSelection) return;
    if (pendingSelection.entryType === "break") {
      addBreak(pendingSelection.dayKey, pendingSelection.startTime, pendingSelection.endTime);
    } else {
      addShift(
        pendingSelection.dayKey,
        pendingSelection.startTime,
        pendingSelection.endTime,
        pendingSelection.companyId,
      );
    }
  };

  // ── Drag / resize (calendar-agnostic) ──────────────────────────────────────

  /**
   * Moves or resizes a shift block to a new time slot, optionally on a different day.
   * Returns true on success; false when blocked (caller should revert the visual change).
   */
  const moveBlock = (
    blockId: string,
    targetDayKey: WorkWeekDay,
    newStartTime: string,
    newEndTime: string,
  ): boolean => {
    const newStartMinutes = timeToMinutes(newStartTime);
    const newEndMinutes = timeToMinutes(newEndTime);

    if (newEndMinutes <= newStartMinutes) return false;

    const location = findShiftLocation(blockId);
    if (!location) return false;

    const sourceDayKey = location.day.day;
    const targetDay = workForm.days.find((d) => d.day === targetDayKey);
    if (!targetDay) return false;

    clearMessages();

    if (
      blocksOverlap(
        targetDay.blocks,
        newStartMinutes,
        newEndMinutes,
        sourceDayKey === targetDayKey ? blockId : undefined,
      )
    ) {
      onErrorChange("Shift overlaps with another shift.");
      return false;
    }

    const updatedBlock: WorkBlock = {
      ...location.block,
      startTime: newStartTime,
      endTime: newEndTime,
    };

    if (sourceDayKey === targetDayKey) {
      updateWorkBlock(targetDayKey, blockId, () => updatedBlock);
    } else {
      setWorkForm((current) => ({
        days: current.days.map((day) => {
          if (day.day === sourceDayKey) {
            return { ...day, blocks: day.blocks.filter((b) => b.id !== blockId) };
          }
          if (day.day === targetDayKey) {
            return { ...day, blocks: sortBlocks([...day.blocks, updatedBlock]) };
          }
          return day;
        }),
      }));
    }

    return true;
  };

  /**
   * Moves or resizes a break to a new time slot, optionally on a different day.
   * Returns true on success; false when blocked (caller should revert the visual change).
   */
  const moveBreak = (
    breakId: string,
    targetDayKey: WorkWeekDay,
    newStartTime: string,
    newEndTime: string,
  ): boolean => {
    const newStartMinutes = timeToMinutes(newStartTime);
    const newEndMinutes = timeToMinutes(newEndTime);

    if (newEndMinutes <= newStartMinutes) return false;

    const location = findBreakLocation(breakId);
    if (!location) return false;

    const sourceDayKey = location.day.day;
    const targetDay = workForm.days.find((d) => d.day === targetDayKey);
    if (!targetDay) return false;

    clearMessages();

    if (
      breaksOverlap(
        targetDay.breaks,
        newStartMinutes,
        newEndMinutes,
        sourceDayKey === targetDayKey ? breakId : undefined,
      )
    ) {
      onErrorChange("Break overlaps with another break.");
      return false;
    }

    const updatedBreak: WorkBreak = {
      ...location.workBreak,
      startTime: newStartTime,
      endTime: newEndTime,
    };

    if (sourceDayKey === targetDayKey) {
      updateDayBreak(targetDayKey, breakId, () => updatedBreak);
    } else {
      setWorkForm((current) => ({
        days: current.days.map((day) => {
          if (day.day === sourceDayKey) {
            return { ...day, breaks: day.breaks.filter((b) => b.id !== breakId) };
          }
          if (day.day === targetDayKey) {
            return { ...day, breaks: sortBreaks([...day.breaks, updatedBreak]) };
          }
          return day;
        }),
      }));
    }

    return true;
  };

  // ── Copy helpers ────────────────────────────────────────────────────────────

  const copyDayScheduleTo = (sourceDay: WorkWeekDay, targetDays: WorkWeekDay[]) => {
    clearMessages();
    const sourceData = workForm.days.find((d) => d.day === sourceDay);
    if (!sourceData) return;

    setWorkForm((current) => ({
      days: current.days.map((day) => {
        if (!targetDays.includes(day.day)) return day;

        const newBlocks = sourceData.blocks
          .filter((block) => {
            const s = timeToMinutes(block.startTime);
            const e = timeToMinutes(block.endTime);
            return !blocksOverlap(day.blocks, s, e);
          })
          .map((block) => {
            const company =
              companyOptions.find((c) => c.id === block.companyId) ?? companyOptions[0];
            return createWorkBlock(company, block.startTime, block.endTime);
          });

        const newBreaks = sourceData.breaks
          .filter((wb) => {
            const s = timeToMinutes(wb.startTime);
            const e = timeToMinutes(wb.endTime);
            return !breaksOverlap(day.breaks, s, e);
          })
          .map((wb) => createWorkBreak(wb.startTime, wb.endTime));

        return {
          ...day,
          blocks: sortBlocks([...day.blocks, ...newBlocks]),
          breaks: sortBreaks([...day.breaks, ...newBreaks]),
        };
      }),
    }));

    setCopyDaySource(undefined);
    setCopyDayPanelOpen(false);
    setCopyDayTargets([]);
    onStatusChange(
      `${DAY_LABELS[sourceDay]} copied to ${targetDays.map((d) => DAY_LABELS[d]).join(", ")}.`,
    );
  };

  const copySingleShiftTo = (block: WorkBlock, targetDays: WorkWeekDay[]) => {
    clearMessages();
    const startM = timeToMinutes(block.startTime);
    const endM = timeToMinutes(block.endTime);
    const company = companyOptions.find((c) => c.id === block.companyId) ?? companyOptions[0];

    setWorkForm((current) => ({
      days: current.days.map((day) => {
        if (!targetDays.includes(day.day)) return day;
        if (blocksOverlap(day.blocks, startM, endM)) return day;
        const newBlock = createWorkBlock(company, block.startTime, block.endTime);
        return { ...day, blocks: sortBlocks([...day.blocks, newBlock]) };
      }),
    }));

    setCopyEntryTargets([]);
    onStatusChange(`Shift copied to ${targetDays.map((d) => DAY_LABELS[d]).join(", ")}.`);
  };

  const copySingleBreakTo = (wb: WorkBreak, targetDays: WorkWeekDay[]) => {
    clearMessages();
    const startM = timeToMinutes(wb.startTime);
    const endM = timeToMinutes(wb.endTime);

    setWorkForm((current) => ({
      days: current.days.map((day) => {
        if (!targetDays.includes(day.day)) return day;
        if (breaksOverlap(day.breaks, startM, endM)) return day;
        const newBreak = createWorkBreak(wb.startTime, wb.endTime);
        return { ...day, breaks: sortBreaks([...day.breaks, newBreak]) };
      }),
    }));

    setCopyEntryTargets([]);
    onStatusChange(`Break copied to ${targetDays.map((d) => DAY_LABELS[d]).join(", ")}.`);
  };

  const toggleCopyDayTarget = (day: WorkWeekDay) =>
    setCopyDayTargets((current) =>
      current.includes(day) ? current.filter((d) => d !== day) : [...current, day],
    );

  const toggleCopyEntryTarget = (day: WorkWeekDay) =>
    setCopyEntryTargets((current) =>
      current.includes(day) ? current.filter((d) => d !== day) : [...current, day],
    );

  // ── Save ────────────────────────────────────────────────────────────────────

  /**
   * Validates and persists the current work profile.
   * The caller supplies the planner view window settings since those live in
   * the calendar component, not in this hook.
   */
  const saveWork = (
    plannerViewStart: string,
    plannerViewEnd: string,
    plannerViewValidationError?: string,
  ) => {
    clearMessages();

    if (pendingSelection) {
      onErrorChange(
        "Create or cancel the selected calendar entry before saving the work profile.",
      );
      return;
    }

    if (plannerViewValidationError) {
      onErrorChange(plannerViewValidationError);
      return;
    }

    const normalizedWorkProfile = normalizeWorkProfile(workForm);
    const validationError = validateWorkProfile(normalizedWorkProfile);
    if (validationError) {
      onErrorChange(validationError);
      return;
    }

    const nextUser = {
      ...user,
      plannerViewStart,
      plannerViewEnd,
      workProfile: normalizedWorkProfile,
      ...getLegacyWorkSettings(normalizedWorkProfile),
    };

    setWorkForm(normalizedWorkProfile);
    onSaveUser(nextUser);
    onStatusChange("Work profile saved.");
  };

  // ── Return ──────────────────────────────────────────────────────────────────

  return {
    // State
    workForm,
    companyOptions,
    workSummary,
    isDirty,
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
    // Selection
    selectRange,
    clearMessages,
    clearPendingSelectionState,
    closeEntryDialog,
    selectShiftBlock,
    selectBreakEntry,
    findShiftLocation,
    findBreakLocation,
    // Mutations
    updateWorkBlock,
    updateDayBreak,
    removeWorkBlock,
    removeBreakFromDay,
    updateBlockCompany,
    convertShiftToBreak,
    convertBreakToShift,
    // Create
    addShift,
    addBreak,
    commitPendingSelection,
    // Move / resize
    moveBlock,
    moveBreak,
    // Copy
    copyDayScheduleTo,
    copySingleShiftTo,
    copySingleBreakTo,
    toggleCopyDayTarget,
    toggleCopyEntryTarget,
    // Save
    saveWork,
  };
}
