import { describe, it, expect } from "vitest";
import {
  timeToMinutes,
  minutesToTime,
  createWorkBreak,
  createWorkBlock,
  createEmptyWorkProfile,
  normalizeWorkProfile,
  getWorkProfileSummary,
  validateWorkProfile,
  createWorkProfileFromLegacyUser,
  getLegacyWorkSettings,
  getProductiveHoursForBlock,
} from "./work-profile";
import type { User, WorkProfile } from "./types";

// ── Helpers to build minimal test fixtures ─────────────────────────────────

const company = { id: "co-1", name: "Company 1" };
const company2 = { id: "co-2", name: "Company 2" };

const minimalUser = (overrides: Partial<User> = {}): User => ({
  username: "test",
  email: "test@example.com",
  orgs: [],
  tasks: [],
  role: "user",
  ...overrides,
});

// ── timeToMinutes ────────────────────────────────────────────────────────────

describe("timeToMinutes", () => {
  it("converts 00:00 to 0", () => {
    expect(timeToMinutes("00:00")).toBe(0);
  });

  it("converts 09:00 to 540", () => {
    expect(timeToMinutes("09:00")).toBe(540);
  });

  it("converts 17:30 to 1050", () => {
    expect(timeToMinutes("17:30")).toBe(1050);
  });

  it("converts 23:59 to 1439", () => {
    expect(timeToMinutes("23:59")).toBe(1439);
  });

  it("returns NaN for invalid format 'abc'", () => {
    expect(timeToMinutes("abc")).toBeNaN();
  });

  it("returns NaN for out-of-range hours", () => {
    expect(timeToMinutes("25:00")).toBeNaN();
  });

  it("returns NaN for out-of-range minutes", () => {
    expect(timeToMinutes("10:60")).toBeNaN();
  });

  it("returns NaN for empty string", () => {
    expect(timeToMinutes("")).toBeNaN();
  });
});

// ── minutesToTime ────────────────────────────────────────────────────────────

describe("minutesToTime", () => {
  it("converts 0 to 00:00", () => {
    expect(minutesToTime(0)).toBe("00:00");
  });

  it("converts 540 to 09:00", () => {
    expect(minutesToTime(540)).toBe("09:00");
  });

  it("converts 1050 to 17:30", () => {
    expect(minutesToTime(1050)).toBe("17:30");
  });

  it("converts 1439 to 23:59", () => {
    expect(minutesToTime(1439)).toBe("23:59");
  });
});

// ── createWorkBlock ──────────────────────────────────────────────────────────

describe("createWorkBlock", () => {
  it("creates a block with given company and times", () => {
    const block = createWorkBlock(company, "08:00", "16:00");
    expect(block.companyId).toBe("co-1");
    expect(block.companyName).toBe("Company 1");
    expect(block.startTime).toBe("08:00");
    expect(block.endTime).toBe("16:00");
  });

  it("assigns a unique id", () => {
    const a = createWorkBlock(company);
    const b = createWorkBlock(company);
    expect(a.id).not.toBe(b.id);
  });

  it("uses default 09:00-17:00 when no times given", () => {
    const block = createWorkBlock(company);
    expect(block.startTime).toBe("09:00");
    expect(block.endTime).toBe("17:00");
  });
});

// ── createWorkBreak ──────────────────────────────────────────────────────────

describe("createWorkBreak", () => {
  it("creates a break with given times", () => {
    const wb = createWorkBreak("12:00", "12:30");
    expect(wb.startTime).toBe("12:00");
    expect(wb.endTime).toBe("12:30");
  });

  it("assigns a unique id", () => {
    const a = createWorkBreak();
    const b = createWorkBreak();
    expect(a.id).not.toBe(b.id);
  });
});

// ── getProductiveHoursForBlock ───────────────────────────────────────────────

describe("getProductiveHoursForBlock", () => {
  it("returns 8.0 for a full 8-hour shift", () => {
    const block = createWorkBlock(company, "09:00", "17:00");
    expect(getProductiveHoursForBlock(block)).toBe(8.0);
  });

  it("returns 0.5 for a 30-minute block", () => {
    const block = createWorkBlock(company, "12:00", "12:30");
    expect(getProductiveHoursForBlock(block)).toBe(0.5);
  });

  it("returns 0 for a block where end equals start", () => {
    const block = createWorkBlock(company, "12:00", "12:00");
    expect(getProductiveHoursForBlock(block)).toBe(0);
  });
});

// ── normalizeWorkProfile ─────────────────────────────────────────────────────

describe("normalizeWorkProfile", () => {
  it("returns a profile with all 7 days even when input only has some days", () => {
    const profile: WorkProfile = {
      days: [{ day: "Mon", blocks: [], breaks: [] }],
    };
    const result = normalizeWorkProfile(profile);
    expect(result.days).toHaveLength(7);
    expect(result.days.map((d) => d.day)).toEqual([
      "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun",
    ]);
  });

  it("sorts blocks by start time ascending", () => {
    const profile: WorkProfile = {
      days: [
        {
          day: "Mon",
          blocks: [
            createWorkBlock(company, "14:00", "17:00"),
            createWorkBlock(company, "08:00", "12:00"),
          ],
          breaks: [],
        },
        ...["Tue", "Wed", "Thu", "Fri", "Sat", "Sun"].map((day) => ({
          day: day as never,
          blocks: [],
          breaks: [],
        })),
      ],
    };

    const result = normalizeWorkProfile(profile);
    const monBlocks = result.days.find((d) => d.day === "Mon")!.blocks;
    expect(monBlocks[0].startTime).toBe("08:00");
    expect(monBlocks[1].startTime).toBe("14:00");
  });

  it("handles undefined profile and returns empty 7-day profile", () => {
    const result = normalizeWorkProfile(undefined);
    expect(result.days).toHaveLength(7);
    result.days.forEach((d) => {
      expect(d.blocks).toHaveLength(0);
      expect(d.breaks).toHaveLength(0);
    });
  });
});

// ── createEmptyWorkProfile ───────────────────────────────────────────────────

describe("createEmptyWorkProfile", () => {
  it("returns 7 days all empty", () => {
    const profile = createEmptyWorkProfile();
    expect(profile.days).toHaveLength(7);
    profile.days.forEach((d) => {
      expect(d.blocks).toHaveLength(0);
      expect(d.breaks).toHaveLength(0);
    });
  });
});

// ── getWorkProfileSummary ────────────────────────────────────────────────────

describe("getWorkProfileSummary", () => {
  it("returns 0 active days and 0 hours for an empty profile", () => {
    const profile = createEmptyWorkProfile();
    const summary = getWorkProfileSummary(profile);
    expect(summary.activeDayCount).toBe(0);
    expect(summary.weeklyHours).toBe(0);
    expect(summary.totalBlocks).toBe(0);
  });

  it("counts a Mon 09:00-17:00 block as 8h, 1 active day", () => {
    const profile: WorkProfile = {
      days: [
        { day: "Mon", blocks: [createWorkBlock(company, "09:00", "17:00")], breaks: [] },
        ...["Tue", "Wed", "Thu", "Fri", "Sat", "Sun"].map((d) => ({
          day: d as never, blocks: [], breaks: [],
        })),
      ],
    };
    const summary = getWorkProfileSummary(profile);
    expect(summary.activeDayCount).toBe(1);
    expect(summary.weeklyHours).toBe(8);
    expect(summary.totalBlocks).toBe(1);
    expect(summary.earliestStart).toBe("09:00");
    expect(summary.latestEnd).toBe("17:00");
  });

  it("sums across multiple days", () => {
    const makeDay = (day: "Mon" | "Tue") => ({
      day,
      blocks: [createWorkBlock(company, "09:00", "13:00")],
      breaks: [],
    });
    const profile: WorkProfile = {
      days: [
        makeDay("Mon"),
        makeDay("Tue"),
        ...["Wed", "Thu", "Fri", "Sat", "Sun"].map((d) => ({
          day: d as never, blocks: [], breaks: [],
        })),
      ],
    };
    const summary = getWorkProfileSummary(profile);
    expect(summary.activeDayCount).toBe(2);
    expect(summary.weeklyHours).toBe(8);
  });

  it("counts breaks correctly", () => {
    const profile: WorkProfile = {
      days: [
        {
          day: "Mon",
          blocks: [createWorkBlock(company, "09:00", "17:00")],
          breaks: [createWorkBreak("12:00", "12:30"), createWorkBreak("15:00", "15:15")],
        },
        ...["Tue", "Wed", "Thu", "Fri", "Sat", "Sun"].map((d) => ({
          day: d as never, blocks: [], breaks: [],
        })),
      ],
    };
    const summary = getWorkProfileSummary(profile);
    expect(summary.totalBreaks).toBe(2);
  });
});

// ── validateWorkProfile ──────────────────────────────────────────────────────

describe("validateWorkProfile", () => {
  it("returns undefined for a valid profile", () => {
    const profile: WorkProfile = {
      days: [
        { day: "Mon", blocks: [createWorkBlock(company, "09:00", "17:00")], breaks: [] },
        ...["Tue", "Wed", "Thu", "Fri", "Sat", "Sun"].map((d) => ({
          day: d as never, blocks: [], breaks: [],
        })),
      ],
    };
    expect(validateWorkProfile(profile)).toBeUndefined();
  });

  it("returns an error when a block has end <= start", () => {
    const block = createWorkBlock(company, "10:00", "09:00");
    const profile: WorkProfile = {
      days: [
        { day: "Mon", blocks: [block], breaks: [] },
        ...["Tue", "Wed", "Thu", "Fri", "Sat", "Sun"].map((d) => ({
          day: d as never, blocks: [], breaks: [],
        })),
      ],
    };
    expect(validateWorkProfile(profile)).toMatch(/Monday/);
  });

  it("returns an error when two blocks overlap", () => {
    const profile: WorkProfile = {
      days: [
        {
          day: "Mon",
          blocks: [
            createWorkBlock(company, "09:00", "13:00"),
            createWorkBlock(company2, "12:00", "17:00"),
          ],
          breaks: [],
        },
        ...["Tue", "Wed", "Thu", "Fri", "Sat", "Sun"].map((d) => ({
          day: d as never, blocks: [], breaks: [],
        })),
      ],
    };
    expect(validateWorkProfile(profile)).toMatch(/overlap/i);
  });

  it("returns an error when a block has an empty company name", () => {
    const block = createWorkBlock({ id: "", name: "" }, "09:00", "17:00");
    const profile: WorkProfile = {
      days: [
        { day: "Mon", blocks: [block], breaks: [] },
        ...["Tue", "Wed", "Thu", "Fri", "Sat", "Sun"].map((d) => ({
          day: d as never, blocks: [], breaks: [],
        })),
      ],
    };
    expect(validateWorkProfile(profile)).toMatch(/company/i);
  });

  it("returns an error when two breaks overlap", () => {
    const profile: WorkProfile = {
      days: [
        {
          day: "Mon",
          blocks: [],
          breaks: [
            createWorkBreak("12:00", "13:00"),
            createWorkBreak("12:30", "13:30"),
          ],
        },
        ...["Tue", "Wed", "Thu", "Fri", "Sat", "Sun"].map((d) => ({
          day: d as never, blocks: [], breaks: [],
        })),
      ],
    };
    expect(validateWorkProfile(profile)).toMatch(/overlap/i);
  });
});

// ── createWorkProfileFromLegacyUser ──────────────────────────────────────────

describe("createWorkProfileFromLegacyUser", () => {
  it("uses user.workProfile when present", () => {
    const stored: WorkProfile = {
      days: [
        { day: "Mon", blocks: [createWorkBlock(company, "08:00", "16:00")], breaks: [] },
        ...["Tue", "Wed", "Thu", "Fri", "Sat", "Sun"].map((d) => ({
          day: d as never, blocks: [], breaks: [],
        })),
      ],
    };
    const user = minimalUser({ workProfile: stored });
    const result = createWorkProfileFromLegacyUser(user);
    const mon = result.days.find((d) => d.day === "Mon")!;
    expect(mon.blocks[0].startTime).toBe("08:00");
  });

  it("creates blocks from legacy workDays when no workProfile is set", () => {
    const user = minimalUser({
      workDays: ["Mon", "Tue", "Wed"],
      workStart: "09:00",
      workEnd: "17:00",
    });
    const result = createWorkProfileFromLegacyUser(user);
    expect(result.days.find((d) => d.day === "Mon")!.blocks).toHaveLength(1);
    expect(result.days.find((d) => d.day === "Thu")!.blocks).toHaveLength(0);
  });

  it("ignores invalid day strings in legacy workDays", () => {
    const user = minimalUser({ workDays: ["Mon", "INVALID", "Fri"] });
    const result = createWorkProfileFromLegacyUser(user);
    expect(result.days.find((d) => d.day === "Mon")!.blocks).toHaveLength(1);
    expect(result.days.find((d) => d.day === "Fri")!.blocks).toHaveLength(1);
  });
});

// ── getLegacyWorkSettings ────────────────────────────────────────────────────

describe("getLegacyWorkSettings", () => {
  it("returns correct maxDailyHours as workCapacityHours", () => {
    const profile: WorkProfile = {
      days: [
        { day: "Mon", blocks: [createWorkBlock(company, "09:00", "17:00")], breaks: [] },
        { day: "Tue", blocks: [createWorkBlock(company, "09:00", "13:00")], breaks: [] },
        ...["Wed", "Thu", "Fri", "Sat", "Sun"].map((d) => ({
          day: d as never, blocks: [], breaks: [],
        })),
      ],
    };
    const settings = getLegacyWorkSettings(profile);
    expect(settings.workCapacityHours).toBe(8); // Mon has 8h (max)
  });

  it("returns active days in workDays", () => {
    const profile: WorkProfile = {
      days: [
        { day: "Mon", blocks: [createWorkBlock(company, "09:00", "17:00")], breaks: [] },
        { day: "Wed", blocks: [createWorkBlock(company, "09:00", "17:00")], breaks: [] },
        ...["Tue", "Thu", "Fri", "Sat", "Sun"].map((d) => ({
          day: d as never, blocks: [], breaks: [],
        })),
      ],
    };
    const settings = getLegacyWorkSettings(profile);
    expect(settings.workDays).toEqual(["Mon", "Wed"]);
  });

  it("sets no-breaks message when profile has no breaks", () => {
    const profile: WorkProfile = {
      days: [
        { day: "Mon", blocks: [createWorkBlock(company, "09:00", "17:00")], breaks: [] },
        ...["Tue", "Wed", "Thu", "Fri", "Sat", "Sun"].map((d) => ({
          day: d as never, blocks: [], breaks: [],
        })),
      ],
    };
    const settings = getLegacyWorkSettings(profile);
    expect(settings.breakRules).toMatch(/No manual breaks/i);
  });

  it("sets break count message when breaks exist", () => {
    const profile: WorkProfile = {
      days: [
        {
          day: "Mon",
          blocks: [createWorkBlock(company, "09:00", "17:00")],
          breaks: [createWorkBreak("12:00", "12:30")],
        },
        ...["Tue", "Wed", "Thu", "Fri", "Sat", "Sun"].map((d) => ({
          day: d as never, blocks: [], breaks: [],
        })),
      ],
    };
    const settings = getLegacyWorkSettings(profile);
    expect(settings.breakRules).toMatch(/1 manual break/i);
  });
});
