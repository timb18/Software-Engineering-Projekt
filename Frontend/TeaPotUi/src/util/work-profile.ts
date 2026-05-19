import type {
  Org,
  User,
  WorkBlock,
  WorkBreak,
  WorkDayProfile,
  WorkProfile,
  WorkWeekDay,
} from "./types";

/**
 * Company option for selecting a company/organization when creating work blocks.
 * 
 * - id: Unique identifier (normalized from name if not provided)
 * - name: Display name (shown in UI)
 */
export type CompanyOption = {
  id: string;
  name: string;
};

/**
 * Canonical day abbreviations in order (Monday through Sunday).
 * Used as the index system for work profile days.
 * 
 * Order: Mon(0), Tue(1), Wed(2), Thu(3), Fri(4), Sat(5), Sun(6)
 */
export const WEEK_DAYS: WorkWeekDay[] = [
  "Mon",
  "Tue",
  "Wed",
  "Thu",
  "Fri",
  "Sat",
  "Sun",
];

/**
 * Human-readable day labels for display in UI.
 * Keyed by day abbreviation (e.g., DAY_LABELS["Mon"] = "Monday").
 */
export const DAY_LABELS: Record<WorkWeekDay, string> = {
  Mon: "Monday",
  Tue: "Tuesday",
  Wed: "Wednesday",
  Thu: "Thursday",
  Fri: "Friday",
  Sat: "Saturday",
  Sun: "Sunday",
};

// Default working days for legacy profiles (pre-work-profile system)
const DEFAULT_LEGACY_DAYS: WorkWeekDay[] = ["Mon", "Tue", "Wed", "Thu", "Fri"];

// Fallback companies if user has no organizations (for demo/testing)
const FALLBACK_COMPANIES: CompanyOption[] = [
  { id: "company-1", name: "Company 1" },
  { id: "company-2", name: "Company 2" },
];

/**
 * Generates a unique ID for work blocks/breaks.
 * 
 * Uses native crypto.randomUUID() if available (modern browsers),
 * otherwise falls back to timestamp + random string (for tests/SSR).
 * 
 * @returns UUID string or fallback unique identifier
 */
const createId = () => {
  if (typeof crypto !== "undefined" && "randomUUID" in crypto) {
    return crypto.randomUUID();
  }

  // Fallback: timestamp-based ID for test/SSR environments
  return `work-${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;
};

/**
 * Normalizes a company ID for consistent storage and lookup.
 * 
 * If ID is empty/whitespace:
 * - Uses slugified company name (lowercase, spaces→hyphens)
 * 
 * If ID is provided:
 * - Trims whitespace and returns as-is
 * 
 * This ensures company references are consistent even if API
 * returns empty or inconsistent IDs.
 * 
 * @param id - Company ID from API or user input
 * @param name - Company name (fallback for generating ID)
 * @returns Normalized company ID
 */
const normalizeCompanyId = (id: string, name: string) => {
  const trimmedId = id.trim();
  if (trimmedId) {
    return trimmedId;
  }

  // Fallback: generate ID from name
  return name.trim().toLowerCase().replace(/\s+/g, "-");
};

/**
 * Deep clones a WorkBreak, preserving all properties.
 * Keeps the original ID (unlike duplicateWorkBreak which creates new ID).
 * 
 * Used internally for normalization/copying while maintaining identity.
 * 
 * @param workBreak - Break to clone
 * @returns Cloned break with same ID
 */
const cloneBreak = (workBreak: WorkBreak): WorkBreak => ({
  id: workBreak.id || createId(),
  startTime: workBreak.startTime,
  endTime: workBreak.endTime,
});

/**
 * Creates a duplicate of a WorkBreak with a NEW unique ID.
 * 
 * Used when copying breaks across days or duplicating work profiles.
 * The new break is independent from the original.
 * 
 * @param workBreak - Break to duplicate
 * @returns New break with fresh ID but same time range
 */
export const duplicateWorkBreak = (workBreak: WorkBreak): WorkBreak => ({
  id: createId(),
  startTime: workBreak.startTime,
  endTime: workBreak.endTime,
});

/**
 * Deep clones a WorkBlock, preserving all properties including company info.
 * Keeps the original ID (unlike duplicateWorkBlock which creates new ID).
 * 
 * Normalizes company ID during clone to ensure consistency.
 * 
 * @param block - Block to clone
 * @returns Cloned block with same ID
 */
const cloneBlock = (block: WorkBlock): WorkBlock => ({
  id: block.id || createId(),
  companyId: normalizeCompanyId(block.companyId, block.companyName),
  companyName: block.companyName,
  startTime: block.startTime,
  endTime: block.endTime,
});

/**
 * Creates a duplicate of a WorkBlock with a NEW unique ID.
 * 
 * Used when copying blocks across days or duplicating work profiles.
 * The new block is independent from the original and has its own ID.
 * 
 * Normalizes company ID for consistency.
 * 
 * @param block - Block to duplicate
 * @returns New block with fresh ID but same time/company info
 */
export const duplicateWorkBlock = (block: WorkBlock): WorkBlock => ({
  id: createId(),
  companyId: normalizeCompanyId(block.companyId, block.companyName),
  companyName: block.companyName,
  startTime: block.startTime,
  endTime: block.endTime,
});

/**
 * Duplicates an entire array of work blocks (creates new IDs for all).
 * 
 * @param blocks - Array of blocks to duplicate
 * @returns New array with duplicated blocks (each with fresh ID)
 */
export const duplicateWorkBlocks = (blocks: WorkBlock[]) => blocks.map(duplicateWorkBlock);

/**
 * Converts a time string (HH:MM format) to minutes since midnight.
 * 
 * Used throughout work profile for time-based calculations and comparisons.
 * 
 * Validation:
 * - Format must be exactly "HH:MM" (regex: ^\d{2}:\d{2}$)
 * - Hours must be 00-23
 * - Minutes must be 00-59
 * 
 * @param value - Time string (e.g., "14:30")
 * @returns Minutes since midnight (0-1440), or NaN if invalid
 * 
 * @example
 * timeToMinutes("09:30") // 570 (9*60 + 30)
 * timeToMinutes("23:59") // 1439
 * timeToMinutes("25:00") // NaN (invalid hour)
 */
export const timeToMinutes = (value: string) => {
  // Strict format validation: must be exactly HH:MM
  if (!/^\d{2}:\d{2}$/.test(value)) {
    return Number.NaN;
  }

  const [hours, minutes] = value.split(":").map(Number);
  
  // Range validation: hours must be 0-23, minutes must be 0-59
  if (hours < 0 || hours > 23 || minutes < 0 || minutes > 59) {
    return Number.NaN;
  }

  return hours * 60 + minutes;
};

/**
 * Converts minutes since midnight to a time string (HH:MM format).
 * 
 * Inverse operation of timeToMinutes().
 * 
 * Normalization:
 * - Wraps around 24-hour cycle (1440 minutes)
 * - Negative values wrap backward (e.g., -30 → 23:30)
 * - Floors floating-point input (e.g., 570.7 → 09:30)
 * 
 * @param value - Minutes since midnight (can be negative or > 1440)
 * @returns Time string in HH:MM format (00:00 - 23:59)
 * 
 * @example
 * minutesToTime(570) // "09:30"
 * minutesToTime(1439) // "23:59"
 * minutesToTime(1440) // "00:00" (wraps around)
 * minutesToTime(-30) // "23:30" (wraps backward)
 */
export const minutesToTime = (value: number) => {
  // Normalize to 0-1439 range using modulo arithmetic
  // Formula: ((value % 1440) + 1440) % 1440 handles negative numbers correctly
  const safeValue = ((Math.floor(value) % 1440) + 1440) % 1440;
  
  const hours = Math.floor(safeValue / 60)
    .toString()
    .padStart(2, "0");
  const minutes = (safeValue % 60).toString().padStart(2, "0");
  return `${hours}:${minutes}`;
};

/**
 * Factory function: Creates a new work break with default times.
 * 
 * Used when user wants to add a new break to their schedule.
 * 
 * @param startTime - Break start time (default: "12:30")
 * @param endTime - Break end time (default: "13:00")
 * @returns New WorkBreak with unique ID and specified times
 */
export const createWorkBreak = (
  startTime = "12:30",
  endTime = "13:00",
): WorkBreak => ({
  id: createId(),
  startTime,
  endTime,
});

/**
 * Factory function: Creates a new work block (shift) for a company.
 * 
 * Used when user creates a new shift/block in their work profile.
 * Automatically assigns the provided company or uses fallback.
 * 
 * @param company - CompanyOption to assign to this block (optional)
 * @param startTime - Shift start time (default: "09:00")
 * @param endTime - Shift end time (default: "17:00")
 * @returns New WorkBlock with unique ID, company, and specified times
 */
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

/**
 * Creates an empty work profile with all days initialized.
 * 
 * Each day has an empty blocks array and empty breaks array.
 * Used as the initial state for new users or to reset a profile.
 * 
 * @returns WorkProfile with 7 days (Mon-Sun), all empty
 */
export const createEmptyWorkProfile = (): WorkProfile => ({
  days: WEEK_DAYS.map((day) => ({ day, blocks: [], breaks: [] })),
});

/**
 * Normalizes a work profile for consistent internal representation.
 * 
 * This is a CRITICAL function that:
 * 1. Ensures all 7 days (Mon-Sun) are present and in order
 * 2. Deduplicates and clones all blocks/breaks (removes stale references)
 * 3. Sorts blocks and breaks within each day by start time
 * 4. Merges legacy nested breaks (old format) with day-level breaks (new format)
 * 5. Handles undefined/missing input gracefully
 * 
 * Called whenever work profile is loaded, saved, or before comparison.
 * Ensures consistent state across the application.
 * 
 * @param profile - WorkProfile to normalize (can be undefined)
 * @returns Normalized WorkProfile with all invariants maintained
 */
export const normalizeWorkProfile = (profile?: WorkProfile): WorkProfile => {
  const storedDays = new Map<WorkWeekDay, WorkDayProfile>();
  profile?.days.forEach((day) => {
    storedDays.set(day.day, day);
  });

  return {
    days: WEEK_DAYS.map((day) => {
      const stored = storedDays.get(day);
      // Clone and sort all blocks by start time
      const blocks = (stored?.blocks ?? [])
        .map(cloneBlock)
        .sort((left, right) => timeToMinutes(left.startTime) - timeToMinutes(right.startTime));

      // Merge explicit day-level breaks with any legacy nested breaks still present on blocks.
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

/**
 * Extracts unique company options from organizations and work profile.
 * 
 * Deduplicates companies by:
 * 1. Exact ID match (prevents using same company twice)
 * 2. Case-insensitive name match (prevents "Company 1" and "company 1")
 * 
 * Sources:
 * - User's organizations (primary source of companies)
 * - Existing blocks in work profile (legacy or manually-added companies)
 * 
 * Sorting: Alphabetically by company name for consistent UI display.
 * 
 * Fallback: If no companies found, returns FALLBACK_COMPANIES for demo/testing.
 * 
 * @param orgs - User's organizations (source of available companies)
 * @param profile - Current work profile (may have legacy company references)
 * @returns Deduplicated, sorted array of company options
 * 
 * @remarks
 * This function is expensive if called frequently (scans all orgs + all blocks).
 * Typically used only during component initialization or state changes.
 */
export const getCompanyOptions = (
  orgs: Org[] = [],
  profile?: WorkProfile,
): CompanyOption[] => {
  const options: CompanyOption[] = [];
  const seenIds = new Set<string>();
  const seenNames = new Set<string>();

  /**
   * Registers a company if not already seen.
   * Performs deduplication by ID and normalized name.
   */
  const register = (id: string, name: string) => {
    const normalizedName = name.trim();
    const normalizedId = normalizeCompanyId(id, name);
    const normalizedNameKey = normalizedName.toLowerCase();

    // Skip if already registered or if name/id is invalid
    if (
      !normalizedName ||
      !normalizedId ||
      seenIds.has(normalizedId) ||
      seenNames.has(normalizedNameKey)
    ) {
      return;
    }

    seenIds.add(normalizedId);
    seenNames.add(normalizedNameKey);
    options.push({ id: normalizedId, name: normalizedName });
  };

  // Register all companies from user's organizations
  orgs.forEach((org) => register(org.id, org.name));
  
  // Also register any companies referenced in existing blocks
  profile?.days.forEach((day) => {
    day.blocks.forEach((block) => register(block.companyId, block.companyName));
  });

  // Use fallback if no companies found (for demo/testing)
  if (options.length === 0) {
    return FALLBACK_COMPANIES;
  }

  // Sort alphabetically for consistent UI display
  return options.sort((left, right) => left.name.localeCompare(right.name));
};

/**
 * Creates a work profile from legacy user settings.
 * 
 * Handles three cases in priority order:
 * 
 * 1. **New work profile format**: If user.workProfile exists, normalize and return it
 *    (This is the modern format created by the work profile editor)
 * 
 * 2. **Explicit empty profile**: If hasPersistedWorkProfile === false, user explicitly
 *    started with an empty schedule. Return createEmptyWorkProfile().
 *    (This prevents accidental recreation of a deleted profile)
 * 
 * 3. **Legacy format**: Construct from old user fields (workStart, workEnd, workDays).
 *    Creates a profile with:
 *    - Same block on each configured day
 *    - First available company from user's orgs
 *    - Time range from workStart/workEnd
 *    - Default: Mon-Fri, 09:00-17:00 if no config found
 * 
 * This function ensures backward compatibility when users have old profile data.
 * 
 * @param user - User object with possible legacy work settings
 * @returns WorkProfile constructed from available data
 */
export const createWorkProfileFromLegacyUser = (
  user?: Pick<
    User,
    "orgs" | "workDays" | "workEnd" | "workProfile" | "workStart" | "hasPersistedWorkProfile"
  >,
): WorkProfile => {
  // Case 1: New work profile format exists → use it
  if (user?.workProfile) {
    return normalizeWorkProfile(user.workProfile);
  }

  // Case 2: User explicitly set empty profile → respect that
  if (user?.hasPersistedWorkProfile === false) {
    // A missing persisted profile means the user intentionally started from an empty schedule.
    return createEmptyWorkProfile();
  }

  // Case 3: Build from legacy fields
  const company = getCompanyOptions(user?.orgs ?? [])[0];
  const configuredDays = (user?.workDays ?? DEFAULT_LEGACY_DAYS).filter(
    (day): day is WorkWeekDay => WEEK_DAYS.includes(day as WorkWeekDay),
  );
  const activeDays = new Set<WorkWeekDay>(configuredDays);
  const startTime = user?.workStart ?? "09:00";
  const endTime = user?.workEnd ?? "17:00";

  return {
    days: WEEK_DAYS.map((day) => ({
      day,
      // Add shift block if day is configured, otherwise empty
      blocks: activeDays.has(day) ? [createWorkBlock(company, startTime, endTime)] : [],
      breaks: [],
    })),
  };
};

/**
 * Calculates productive minutes in a work block (duration minus breaks).
 * 
 * Validates times before calculation:
 * - Both start and end must be valid times (not NaN)
 * - End time must be strictly after start time (end > start)
 * 
 * @param block - WorkBlock with startTime/endTime
 * @returns Productive minutes (0 if invalid times)
 * 
 * @remarks
 * This is a pure utility for internal calculations.
 * For user-facing hours, use getProductiveHoursForBlock().
 */
const getProductiveMinutesForBlock = (block: WorkBlock) => {
  const start = timeToMinutes(block.startTime);
  const end = timeToMinutes(block.endTime);
  if (Number.isNaN(start) || Number.isNaN(end) || end <= start) {
    return 0;
  }

  return end - start;
};

/**
 * Rounds minutes to hours with 2 decimal precision.
 * 
 * Example: 90 minutes → 1.5 hours, 45 minutes → 0.75 hours
 * 
 * @param minutes - Duration in minutes
 * @returns Duration in hours (rounded to 2 decimals)
 */
const roundHours = (minutes: number) => Math.round((minutes / 60) * 100) / 100;

/**
 * Gets productive hours in a work block (user-facing format).
 * 
 * Converts minutes to hours and rounds to 2 decimal places.
 * Returns 0 for invalid blocks.
 * 
 * @param block - WorkBlock to measure
 * @returns Duration in hours (e.g., 7.5 for 7.5-hour workday)
 */
export const getProductiveHoursForBlock = (block: WorkBlock) =>
  roundHours(getProductiveMinutesForBlock(block));

/**
 * Generates a comprehensive summary of work profile metrics.
 * 
 * Calculates:
 * - Daily hours breakdown for each day (Mon-Sun)
 * - Earliest start time across all blocks
 * - Latest end time across all blocks
 * - Weekly total minutes of work
 * - Maximum daily workload (peak work day)
 * - Number of configured days (with at least one block)
 * - Total blocks and breaks count
 * 
 * Used for UI display (work profile summary, planning/scheduling views).
 * 
 * Return object:
 * - dailyHours: Record<WorkWeekDay, number> → hours per day
 * - earliestStart: string (HH:MM) → earliest work start time
 * - latestEnd: string (HH:MM) → latest work end time
 * - weeklyHours: number → total hours per week
 * - maxDailyHours: number → hours on busiest day
 * - activeDays: WorkWeekDay[] → days with at least one block
 * - totalBlocks: number → count of all work blocks
 * - totalBreaks: number → count of all breaks
 * 
 * Edge cases:
 * - Empty profile: Returns all 0s and empty arrays
 * - Invalid times: Silently skipped (don't affect calculation)
 * 
 * @param profile - WorkProfile to summarize
 * @returns WorkProfileSummary object with all calculated metrics
 */
export const getWorkProfileSummary = (profile: WorkProfile) => {
  const normalizedProfile = normalizeWorkProfile(profile);
  
  // Initialize daily hours tracking
  const dailyHours = Object.fromEntries(
    WEEK_DAYS.map((day) => [day, 0]),
  ) as Record<WorkWeekDay, number>;

  // Tracking variables for aggregation
  let earliestStart = Number.POSITIVE_INFINITY;
  let latestEnd = -1;
  let weeklyMinutes = 0;
  let maxDailyMinutes = 0;
  let totalBlocks = 0;
  let totalBreaks = 0;
  const activeDays: WorkWeekDay[] = [];

  // Scan all days to aggregate metrics
  normalizedProfile.days.forEach((day) => {
    let dailyMinutes = 0;

    // Sum up minutes from all blocks on this day
    day.blocks.forEach((block) => {
      totalBlocks += 1;

      const start = timeToMinutes(block.startTime);
      const end = timeToMinutes(block.endTime);
      // Update earliest start and latest end (skip if times are invalid)
      if (!Number.isNaN(start) && !Number.isNaN(end) && end > start) {
        earliestStart = Math.min(earliestStart, start);
        latestEnd = Math.max(latestEnd, end);
      }

      // Add productive minutes to daily total
      dailyMinutes += getProductiveMinutesForBlock(block);
    });

    totalBreaks += day.breaks.length;

    // If day has any blocks, mark it as active and record hours
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
