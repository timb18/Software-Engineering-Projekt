import type {
  Team,
  User,
  WorkBlock,
  WorkBreak,
  WorkDayProfile,
  WorkProfile,
  WorkWeekDay,
} from "./types";

export type CompanyOption = {
  id: string;
  name: string;
};

export const WEEK_DAYS: WorkWeekDay[] = [
  "Mon",
  "Tue",
  "Wed",
  "Thu",
  "Fri",
  "Sat",
  "Sun",
];

export const DAY_LABELS: Record<WorkWeekDay, string> = {
  Mon: "Monday",
  Tue: "Tuesday",
  Wed: "Wednesday",
  Thu: "Thursday",
  Fri: "Friday",
  Sat: "Saturday",
  Sun: "Sunday",
};

const DEFAULT_LEGACY_DAYS: WorkWeekDay[] = ["Mon", "Tue", "Wed", "Thu", "Fri"];
const FALLBACK_COMPANIES: CompanyOption[] = [
  { id: "company-1", name: "Company 1" },
  { id: "company-2", name: "Company 2" },
];

const createId = () => {
  if (typeof crypto !== "undefined" && "randomUUID" in crypto) {
    return crypto.randomUUID();
  }

  return `work-${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;
};

const normalizeCompanyId = (id: string, name: string) => {
  const trimmedId = id.trim();
  if (trimmedId) {
    return trimmedId;
  }

  return name.trim().toLowerCase().replace(/\s+/g, "-");
};

const cloneBreak = (workBreak: WorkBreak): WorkBreak => ({
  id: workBreak.id || createId(),
  startTime: workBreak.startTime,
  endTime: workBreak.endTime,
});

export const duplicateWorkBreak = (workBreak: WorkBreak): WorkBreak => ({
  id: createId(),
  startTime: workBreak.startTime,
  endTime: workBreak.endTime,
});

const cloneBlock = (block: WorkBlock): WorkBlock => ({
  id: block.id || createId(),
  companyId: normalizeCompanyId(block.companyId, block.companyName),
  companyName: block.companyName,
  startTime: block.startTime,
  endTime: block.endTime,
});

export const duplicateWorkBlock = (block: WorkBlock): WorkBlock => ({
  id: createId(),
  companyId: normalizeCompanyId(block.companyId, block.companyName),
  companyName: block.companyName,
  startTime: block.startTime,
  endTime: block.endTime,
});

export const duplicateWorkBlocks = (blocks: WorkBlock[]) => blocks.map(duplicateWorkBlock);

export const timeToMinutes = (value: string) => {
  if (!/^\d{2}:\d{2}$/.test(value)) {
    return Number.NaN;
  }

  const [hours, minutes] = value.split(":").map(Number);
  if (hours < 0 || hours > 23 || minutes < 0 || minutes > 59) {
    return Number.NaN;
  }

  return hours * 60 + minutes;
};

export const minutesToTime = (value: number) => {
  const safeValue = ((Math.floor(value) % 1440) + 1440) % 1440;
  const hours = Math.floor(safeValue / 60)
    .toString()
    .padStart(2, "0");
  const minutes = (safeValue % 60).toString().padStart(2, "0");
  return `${hours}:${minutes}`;
};

export const createWorkBreak = (
  startTime = "12:30",
  endTime = "13:00",
): WorkBreak => ({
  id: createId(),
  startTime,
  endTime,
});

export const createWorkBlock = (
  company?: CompanyOption,
  startTime = "09:00",
  endTime = "17:00",
): WorkBlock => ({
  id: createId(),
  companyId: normalizeCompanyId(company?.id ?? "", company?.name ?? ""),
  companyName: company?.name ?? "",
  startTime,
  endTime,
});

export const createEmptyWorkProfile = (): WorkProfile => ({
  days: WEEK_DAYS.map((day) => ({ day, blocks: [], breaks: [] })),
});

export const normalizeWorkProfile = (profile?: WorkProfile): WorkProfile => {
  const storedDays = new Map<WorkWeekDay, WorkDayProfile>();
  profile?.days.forEach((day) => {
    storedDays.set(day.day, day);
  });

  return {
    days: WEEK_DAYS.map((day) => {
      const stored = storedDays.get(day);
      const blocks = (stored?.blocks ?? [])
        .map(cloneBlock)
        .sort((left, right) => timeToMinutes(left.startTime) - timeToMinutes(right.startTime));

      // Collect day-level breaks plus any breaks still nested on legacy blocks
      const legacyBreaks = (stored?.blocks ?? []).flatMap((block) =>
        ((block as WorkBlock & { breaks?: WorkBreak[] }).breaks ?? []).map(cloneBreak),
      );
      const breaks = [...(stored?.breaks ?? []).map(cloneBreak), ...legacyBreaks].sort(
        (left, right) => timeToMinutes(left.startTime) - timeToMinutes(right.startTime),
      );

      return { day, blocks, breaks };
    }),
  };
};

export const getCompanyOptions = (
  teams: Team[] = [],
  profile?: WorkProfile,
): CompanyOption[] => {
  const options: CompanyOption[] = [];
  const seen = new Set<string>();

  const register = (id: string, name: string) => {
    const normalizedName = name.trim();
    const normalizedId = normalizeCompanyId(id, name);

    if (!normalizedName || !normalizedId || seen.has(normalizedId)) {
      return;
    }

    seen.add(normalizedId);
    options.push({ id: normalizedId, name: normalizedName });
  };

  teams.forEach((team) => register(team.id, team.name));
  profile?.days.forEach((day) => {
    day.blocks.forEach((block) => register(block.companyId, block.companyName));
  });

  if (options.length === 0) {
    return FALLBACK_COMPANIES;
  }

  return options.slice(0, 2);
};

export const createWorkProfileFromLegacyUser = (
  user?: Pick<User, "teams" | "workDays" | "workEnd" | "workProfile" | "workStart">,
): WorkProfile => {
  if (user?.workProfile) {
    return normalizeWorkProfile(user.workProfile);
  }

  const company = getCompanyOptions(user?.teams ?? [])[0];
  const configuredDays = (user?.workDays ?? DEFAULT_LEGACY_DAYS).filter(
    (day): day is WorkWeekDay => WEEK_DAYS.includes(day as WorkWeekDay),
  );
  const activeDays = new Set<WorkWeekDay>(configuredDays);
  const startTime = user?.workStart ?? "09:00";
  const endTime = user?.workEnd ?? "17:00";

  return {
    days: WEEK_DAYS.map((day) => ({
      day,
      blocks: activeDays.has(day) ? [createWorkBlock(company, startTime, endTime)] : [],
      breaks: [],
    })),
  };
};

const getProductiveMinutesForBlock = (block: WorkBlock) => {
  const start = timeToMinutes(block.startTime);
  const end = timeToMinutes(block.endTime);
  if (Number.isNaN(start) || Number.isNaN(end) || end <= start) {
    return 0;
  }

  return end - start;
};

const roundHours = (minutes: number) => Math.round((minutes / 60) * 100) / 100;

export const getProductiveHoursForBlock = (block: WorkBlock) =>
  roundHours(getProductiveMinutesForBlock(block));

export const getWorkProfileSummary = (profile: WorkProfile) => {
  const normalizedProfile = normalizeWorkProfile(profile);
  const dailyHours = Object.fromEntries(
    WEEK_DAYS.map((day) => [day, 0]),
  ) as Record<WorkWeekDay, number>;

  let earliestStart = Number.POSITIVE_INFINITY;
  let latestEnd = -1;
  let weeklyMinutes = 0;
  let maxDailyMinutes = 0;
  let totalBlocks = 0;
  let totalBreaks = 0;
  const activeDays: WorkWeekDay[] = [];

  normalizedProfile.days.forEach((day) => {
    let dailyMinutes = 0;

    day.blocks.forEach((block) => {
      totalBlocks += 1;

      const start = timeToMinutes(block.startTime);
      const end = timeToMinutes(block.endTime);
      if (!Number.isNaN(start) && !Number.isNaN(end) && end > start) {
        earliestStart = Math.min(earliestStart, start);
        latestEnd = Math.max(latestEnd, end);
      }

      dailyMinutes += getProductiveMinutesForBlock(block);
    });

    totalBreaks += day.breaks.length;

    if (day.blocks.length > 0) {
      activeDays.push(day.day);
    }

    dailyHours[day.day] = roundHours(dailyMinutes);
    weeklyMinutes += dailyMinutes;
    maxDailyMinutes = Math.max(maxDailyMinutes, dailyMinutes);
  });

  return {
    activeDays,
    activeDayCount: activeDays.length,
    earliestStart:
      earliestStart === Number.POSITIVE_INFINITY ? undefined : minutesToTime(earliestStart),
    latestEnd: latestEnd < 0 ? undefined : minutesToTime(latestEnd),
    weeklyHours: roundHours(weeklyMinutes),
    maxDailyHours: roundHours(maxDailyMinutes),
    totalBlocks,
    totalBreaks,
    dailyHours,
  };
};

export const getLegacyWorkSettings = (profile: WorkProfile) => {
  const summary = getWorkProfileSummary(profile);

  return {
    workCapacityHours: summary.maxDailyHours,
    workDays: summary.activeDays,
    workStart: summary.earliestStart,
    workEnd: summary.latestEnd,
    breakRules:
      summary.totalBreaks > 0
        ? `${summary.totalBreaks} manual break${summary.totalBreaks === 1 ? "" : "s"} configured`
        : "No manual breaks configured",
  };
};

export const createSuggestedBreak = (rangeStartMinutes = 12 * 60, rangeEndMinutes = 13 * 60) => {
  if (rangeEndMinutes - rangeStartMinutes < 15) {
    return createWorkBreak("12:00", "12:15");
  }

  const midpoint = rangeStartMinutes + Math.floor((rangeEndMinutes - rangeStartMinutes) / 2);
  const breakStart = Math.max(rangeStartMinutes, Math.min(rangeEndMinutes - 15, midpoint - 15));
  const breakEnd = Math.min(rangeEndMinutes, breakStart + 30);

  return createWorkBreak(minutesToTime(breakStart), minutesToTime(Math.max(breakStart + 15, breakEnd)));
};

export const validateWorkProfile = (profile: WorkProfile) => {
  const normalizedProfile = normalizeWorkProfile(profile);

  for (const day of normalizedProfile.days) {
    let previousBlockEnd = -1;

    for (let blockIndex = 0; blockIndex < day.blocks.length; blockIndex += 1) {
      const block = day.blocks[blockIndex];
      const start = timeToMinutes(block.startTime);
      const end = timeToMinutes(block.endTime);

      if (!block.companyName.trim()) {
        return `${DAY_LABELS[day.day]} block ${blockIndex + 1} needs a company.`;
      }
      if (Number.isNaN(start) || Number.isNaN(end)) {
        return `${DAY_LABELS[day.day]} block ${blockIndex + 1} has an invalid time.`;
      }
      if (end <= start) {
        return `${DAY_LABELS[day.day]} block ${blockIndex + 1} must end after it starts.`;
      }
      if (previousBlockEnd > start) {
        return `${DAY_LABELS[day.day]} contains overlapping work blocks.`;
      }

      previousBlockEnd = end;
    }

    let previousBreakEnd = -1;
    for (let breakIndex = 0; breakIndex < day.breaks.length; breakIndex += 1) {
      const workBreak = day.breaks[breakIndex];
      const breakStart = timeToMinutes(workBreak.startTime);
      const breakEnd = timeToMinutes(workBreak.endTime);

      if (Number.isNaN(breakStart) || Number.isNaN(breakEnd)) {
        return `${DAY_LABELS[day.day]} break ${breakIndex + 1} has an invalid time.`;
      }
      if (breakEnd <= breakStart) {
        return `${DAY_LABELS[day.day]} break ${breakIndex + 1} ends before it starts.`;
      }
      if (previousBreakEnd > breakStart) {
        return `${DAY_LABELS[day.day]} has overlapping breaks.`;
      }

      previousBreakEnd = breakEnd;
    }
  }

  return undefined;
};