import { describe, it, expect, vi } from "vitest";
import { renderHook, act } from "@testing-library/react";
import { useWorkProfile } from "./use-work-profile";
import { blocksOverlap, breaksOverlap, sortBlocks, sortBreaks } from "./use-work-profile";
import { createWorkBlock, createWorkBreak, createEmptyWorkProfile } from "./work-profile";
import type { User, WorkProfile } from "./types";

// ── Fixtures ──────────────────────────────────────────────────────────────────

const company = { id: "co-1", name: "Acme" };
const company2 = { id: "co-2", name: "Globex" };

const userWithEmptyProfile = (): User => ({
  username: "tester",
  email: "tester@example.com",
  orgs: [{ id: "co-1", name: "Acme", users: [] }],
  tasks: [],
  role: "user",
  workProfile: createEmptyWorkProfile(),
});

const userWithProfile = (profile: WorkProfile): User => ({
  ...userWithEmptyProfile(),
  workProfile: profile,
});

const makeCallbacks = () => ({
  onSaveUser: vi.fn(),
  onStatusChange: vi.fn(),
  onErrorChange: vi.fn(),
  onDirtyChange: vi.fn(),
});

// ── Pure helper: sortBlocks ───────────────────────────────────────────────────

describe("sortBlocks", () => {
  it("sorts blocks by startTime ascending", () => {
    const a = createWorkBlock(company, "14:00", "16:00");
    const b = createWorkBlock(company, "08:00", "10:00");
    const sorted = sortBlocks([a, b]);
    expect(sorted[0].startTime).toBe("08:00");
    expect(sorted[1].startTime).toBe("14:00");
  });

  it("returns a new array (does not mutate input)", () => {
    const blocks = [createWorkBlock(company, "10:00", "11:00")];
    const result = sortBlocks(blocks);
    expect(result).not.toBe(blocks);
  });
});

// ── Pure helper: sortBreaks ───────────────────────────────────────────────────

describe("sortBreaks", () => {
  it("sorts breaks by startTime ascending", () => {
    const a = createWorkBreak("15:00", "15:30");
    const b = createWorkBreak("12:00", "12:30");
    const sorted = sortBreaks([a, b]);
    expect(sorted[0].startTime).toBe("12:00");
    expect(sorted[1].startTime).toBe("15:00");
  });
});

// ── Pure helper: blocksOverlap ────────────────────────────────────────────────

describe("blocksOverlap", () => {
  const existing = createWorkBlock(company, "09:00", "12:00"); // 540–720

  it("returns false when candidate is completely before existing block", () => {
    expect(blocksOverlap([existing], 420, 480)).toBe(false); // 07:00-08:00
  });

  it("returns false when candidate is completely after existing block", () => {
    expect(blocksOverlap([existing], 780, 900)).toBe(false); // 13:00-15:00
  });

  it("returns true when candidate overlaps at the start of existing block", () => {
    expect(blocksOverlap([existing], 480, 570)).toBe(true); // 08:00-09:30
  });

  it("returns true when candidate is fully inside existing block", () => {
    expect(blocksOverlap([existing], 570, 660)).toBe(true); // 09:30-11:00
  });

  it("returns true when candidate spans over existing block", () => {
    expect(blocksOverlap([existing], 480, 780)).toBe(true); // 08:00-13:00
  });

  it("returns false when candidate touches end boundary (no overlap)", () => {
    expect(blocksOverlap([existing], 720, 780)).toBe(false); // 12:00-13:00 — touching, not overlapping
  });

  it("ignores block when its id matches ignoredBlockId", () => {
    expect(blocksOverlap([existing], 570, 660, existing.id)).toBe(false);
  });
});

// ── Pure helper: breaksOverlap ────────────────────────────────────────────────

describe("breaksOverlap", () => {
  const existing = createWorkBreak("12:00", "12:30"); // 720–750

  it("returns false when candidate does not overlap", () => {
    expect(breaksOverlap([existing], 750, 810)).toBe(false);
  });

  it("returns true when candidate overlaps", () => {
    expect(breaksOverlap([existing], 710, 730)).toBe(true);
  });

  it("ignores break when its id matches ignoredBreakId", () => {
    expect(breaksOverlap([existing], 710, 730, existing.id)).toBe(false);
  });
});

// ── useWorkProfile – initial state ────────────────────────────────────────────

describe("useWorkProfile – initial state", () => {
  it("starts with isDirty = false (saved === initial)", () => {
    const callbacks = makeCallbacks();
    const { result } = renderHook(() =>
      useWorkProfile(userWithEmptyProfile(), callbacks),
    );
    expect(result.current.isDirty).toBe(false);
  });

  it("showEncouragement is true when profile is empty", () => {
    const callbacks = makeCallbacks();
    const { result } = renderHook(() =>
      useWorkProfile(userWithEmptyProfile(), callbacks),
    );
    expect(result.current.showEncouragement).toBe(true);
  });

  it("showEncouragement is false when profile has at least one block", () => {
    const profile: WorkProfile = {
      days: [
        { day: "Mon", blocks: [createWorkBlock(company, "09:00", "17:00")], breaks: [] },
        ...["Tue", "Wed", "Thu", "Fri", "Sat", "Sun"].map((d) => ({
          day: d as never,
          blocks: [],
          breaks: [],
        })),
      ],
    };
    const callbacks = makeCallbacks();
    const { result } = renderHook(() =>
      useWorkProfile(userWithProfile(profile), callbacks),
    );
    expect(result.current.showEncouragement).toBe(false);
  });
});

// ── useWorkProfile – addShift ─────────────────────────────────────────────────

describe("useWorkProfile – addShift", () => {
  it("adds a shift and marks profile as dirty", () => {
    const callbacks = makeCallbacks();
    const { result } = renderHook(() =>
      useWorkProfile(userWithEmptyProfile(), callbacks),
    );

    act(() => {
      result.current.addShift("Mon", "09:00", "17:00");
    });

    const mon = result.current.workForm.days.find((d) => d.day === "Mon")!;
    expect(mon.blocks).toHaveLength(1);
    expect(mon.blocks[0].startTime).toBe("09:00");
    expect(result.current.isDirty).toBe(true);
  });

  it("calls onErrorChange when shift end is before start", () => {
    const callbacks = makeCallbacks();
    const { result } = renderHook(() =>
      useWorkProfile(userWithEmptyProfile(), callbacks),
    );

    act(() => {
      result.current.addShift("Mon", "17:00", "09:00");
    });

    expect(callbacks.onErrorChange).toHaveBeenCalled();
    const mon = result.current.workForm.days.find((d) => d.day === "Mon")!;
    expect(mon.blocks).toHaveLength(0);
  });

  it("calls onErrorChange when a new shift overlaps an existing one", () => {
    const profile: WorkProfile = {
      days: [
        { day: "Mon", blocks: [createWorkBlock(company, "09:00", "13:00")], breaks: [] },
        ...["Tue", "Wed", "Thu", "Fri", "Sat", "Sun"].map((d) => ({
          day: d as never,
          blocks: [],
          breaks: [],
        })),
      ],
    };
    const callbacks = makeCallbacks();
    const { result } = renderHook(() =>
      useWorkProfile(userWithProfile(profile), callbacks),
    );

    act(() => {
      result.current.addShift("Mon", "12:00", "17:00");
    });

    expect(callbacks.onErrorChange).toHaveBeenCalled();
    const mon = result.current.workForm.days.find((d) => d.day === "Mon")!;
    expect(mon.blocks).toHaveLength(1); // unchanged
  });
});

// ── useWorkProfile – addBreak ─────────────────────────────────────────────────

describe("useWorkProfile – addBreak", () => {
  it("adds a break to the correct day", () => {
    const callbacks = makeCallbacks();
    const { result } = renderHook(() =>
      useWorkProfile(userWithEmptyProfile(), callbacks),
    );

    act(() => {
      result.current.addBreak("Wed", "12:00", "12:30");
    });

    const wed = result.current.workForm.days.find((d) => d.day === "Wed")!;
    expect(wed.breaks).toHaveLength(1);
    expect(wed.breaks[0].startTime).toBe("12:00");
  });

  it("calls onErrorChange when overlapping break is added", () => {
    const profile: WorkProfile = {
      days: [
        { day: "Mon", blocks: [], breaks: [createWorkBreak("12:00", "12:30")] },
        ...["Tue", "Wed", "Thu", "Fri", "Sat", "Sun"].map((d) => ({
          day: d as never,
          blocks: [],
          breaks: [],
        })),
      ],
    };
    const callbacks = makeCallbacks();
    const { result } = renderHook(() =>
      useWorkProfile(userWithProfile(profile), callbacks),
    );

    act(() => {
      result.current.addBreak("Mon", "12:15", "12:45");
    });

    expect(callbacks.onErrorChange).toHaveBeenCalled();
  });
});

// ── useWorkProfile – moveBlock ────────────────────────────────────────────────

describe("useWorkProfile – moveBlock", () => {
  const buildProfileWithMonBlock = () => {
    const profile: WorkProfile = {
      days: [
        { day: "Mon", blocks: [createWorkBlock(company, "09:00", "17:00")], breaks: [] },
        ...["Tue", "Wed", "Thu", "Fri", "Sat", "Sun"].map((d) => ({
          day: d as never,
          blocks: [],
          breaks: [],
        })),
      ],
    };
    return profile;
  };

  it("moves a block to a new time on the same day", () => {
    const profile = buildProfileWithMonBlock();
    const callbacks = makeCallbacks();
    const { result } = renderHook(() =>
      useWorkProfile(userWithProfile(profile), callbacks),
    );
    const blockId = result.current.workForm.days.find((d) => d.day === "Mon")!.blocks[0].id;

    act(() => {
      result.current.moveBlock(blockId, "Mon", "10:00", "18:00");
    });

    const mon = result.current.workForm.days.find((d) => d.day === "Mon")!;
    expect(mon.blocks[0].startTime).toBe("10:00");
    expect(mon.blocks[0].endTime).toBe("18:00");
  });

  it("moves a block cross-day: removes from source, adds to target", () => {
    const profile = buildProfileWithMonBlock();
    const callbacks = makeCallbacks();
    const { result } = renderHook(() =>
      useWorkProfile(userWithProfile(profile), callbacks),
    );
    const blockId = result.current.workForm.days.find((d) => d.day === "Mon")!.blocks[0].id;

    act(() => {
      result.current.moveBlock(blockId, "Tue", "09:00", "17:00");
    });

    const mon = result.current.workForm.days.find((d) => d.day === "Mon")!;
    const tue = result.current.workForm.days.find((d) => d.day === "Tue")!;
    expect(mon.blocks).toHaveLength(0);
    expect(tue.blocks).toHaveLength(1);
    expect(tue.blocks[0].startTime).toBe("09:00");
  });

  it("returns false and calls onErrorChange when target day already has an overlapping block", () => {
    const profile: WorkProfile = {
      days: [
        { day: "Mon", blocks: [createWorkBlock(company, "09:00", "13:00")], breaks: [] },
        { day: "Tue", blocks: [createWorkBlock(company2, "10:00", "14:00")], breaks: [] },
        ...["Wed", "Thu", "Fri", "Sat", "Sun"].map((d) => ({
          day: d as never,
          blocks: [],
          breaks: [],
        })),
      ],
    };
    const callbacks = makeCallbacks();
    const { result } = renderHook(() =>
      useWorkProfile(userWithProfile(profile), callbacks),
    );
    const monBlockId = result.current.workForm.days.find((d) => d.day === "Mon")!.blocks[0].id;

    let success: boolean;
    act(() => {
      success = result.current.moveBlock(monBlockId, "Tue", "09:00", "13:00");
    });

    expect(success!).toBe(false);
    expect(callbacks.onErrorChange).toHaveBeenCalled();
    // Mon block must still be there
    const mon = result.current.workForm.days.find((d) => d.day === "Mon")!;
    expect(mon.blocks).toHaveLength(1);
  });
});

// ── useWorkProfile – moveBreak ────────────────────────────────────────────────

describe("useWorkProfile – moveBreak", () => {
  it("moves a break to a new time on the same day", () => {
    const profile: WorkProfile = {
      days: [
        { day: "Mon", blocks: [], breaks: [createWorkBreak("12:00", "12:30")] },
        ...["Tue", "Wed", "Thu", "Fri", "Sat", "Sun"].map((d) => ({
          day: d as never,
          blocks: [],
          breaks: [],
        })),
      ],
    };
    const callbacks = makeCallbacks();
    const { result } = renderHook(() =>
      useWorkProfile(userWithProfile(profile), callbacks),
    );
    const breakId = result.current.workForm.days.find((d) => d.day === "Mon")!.breaks[0].id;

    act(() => {
      result.current.moveBreak(breakId, "Mon", "15:00", "15:30");
    });

    const mon = result.current.workForm.days.find((d) => d.day === "Mon")!;
    expect(mon.breaks[0].startTime).toBe("15:00");
  });

  it("moves a break cross-day", () => {
    const profile: WorkProfile = {
      days: [
        { day: "Mon", blocks: [], breaks: [createWorkBreak("12:00", "12:30")] },
        ...["Tue", "Wed", "Thu", "Fri", "Sat", "Sun"].map((d) => ({
          day: d as never,
          blocks: [],
          breaks: [],
        })),
      ],
    };
    const callbacks = makeCallbacks();
    const { result } = renderHook(() =>
      useWorkProfile(userWithProfile(profile), callbacks),
    );
    const breakId = result.current.workForm.days.find((d) => d.day === "Mon")!.breaks[0].id;

    act(() => {
      result.current.moveBreak(breakId, "Wed", "13:00", "13:30");
    });

    const mon = result.current.workForm.days.find((d) => d.day === "Mon")!;
    const wed = result.current.workForm.days.find((d) => d.day === "Wed")!;
    expect(mon.breaks).toHaveLength(0);
    expect(wed.breaks[0].startTime).toBe("13:00");
  });
});

// ── useWorkProfile – removeWorkBlock ──────────────────────────────────────────

describe("useWorkProfile – removeWorkBlock", () => {
  it("removes the block and marks as dirty", () => {
    const profile: WorkProfile = {
      days: [
        { day: "Mon", blocks: [createWorkBlock(company, "09:00", "17:00")], breaks: [] },
        ...["Tue", "Wed", "Thu", "Fri", "Sat", "Sun"].map((d) => ({
          day: d as never,
          blocks: [],
          breaks: [],
        })),
      ],
    };
    const callbacks = makeCallbacks();
    const { result } = renderHook(() =>
      useWorkProfile(userWithProfile(profile), callbacks),
    );
    const blockId = result.current.workForm.days.find((d) => d.day === "Mon")!.blocks[0].id;

    act(() => {
      result.current.removeWorkBlock("Mon", blockId);
    });

    const mon = result.current.workForm.days.find((d) => d.day === "Mon")!;
    expect(mon.blocks).toHaveLength(0);
    expect(result.current.isDirty).toBe(true);
  });
});

// ── useWorkProfile – selectRange ──────────────────────────────────────────────

describe("useWorkProfile – selectRange", () => {
  it("returns undefined and sets pendingSelection for a valid empty range", () => {
    const callbacks = makeCallbacks();
    const { result } = renderHook(() =>
      useWorkProfile(userWithEmptyProfile(), callbacks),
    );

    act(() => {
      const error = result.current.selectRange("Mon", "09:00", "10:00");
      expect(error).toBeUndefined();
    });

    expect(result.current.pendingSelection).toBeDefined();
    expect(result.current.pendingSelection?.dayKey).toBe("Mon");
    expect(result.current.pendingSelection?.entryType).toBe("shift");
  });

  it("returns an error string for an invalid time range", () => {
    const callbacks = makeCallbacks();
    const { result } = renderHook(() =>
      useWorkProfile(userWithEmptyProfile(), callbacks),
    );

    let error: string | undefined;
    act(() => {
      error = result.current.selectRange("Mon", "10:00", "09:00");
    });

    expect(error).toBeDefined();
    expect(result.current.pendingSelection).toBeUndefined();
  });

  it("defaults to 'break' when a shift already occupies the range", () => {
    const profile: WorkProfile = {
      days: [
        { day: "Mon", blocks: [createWorkBlock(company, "09:00", "17:00")], breaks: [] },
        ...["Tue", "Wed", "Thu", "Fri", "Sat", "Sun"].map((d) => ({
          day: d as never,
          blocks: [],
          breaks: [],
        })),
      ],
    };
    const callbacks = makeCallbacks();
    const { result } = renderHook(() =>
      useWorkProfile(userWithProfile(profile), callbacks),
    );

    act(() => {
      result.current.selectRange("Mon", "10:00", "11:00");
    });

    expect(result.current.pendingSelection?.entryType).toBe("break");
  });
});

// ── useWorkProfile – saveWork ─────────────────────────────────────────────────

describe("useWorkProfile – saveWork", () => {
  it("calls onSaveUser and onStatusChange when profile is valid", () => {
    const profile: WorkProfile = {
      days: [
        { day: "Mon", blocks: [createWorkBlock(company, "09:00", "17:00")], breaks: [] },
        ...["Tue", "Wed", "Thu", "Fri", "Sat", "Sun"].map((d) => ({
          day: d as never,
          blocks: [],
          breaks: [],
        })),
      ],
    };
    const callbacks = makeCallbacks();
    const { result } = renderHook(() =>
      useWorkProfile(userWithProfile(profile), callbacks),
    );

    act(() => {
      result.current.saveWork("06:00", "22:00");
    });

    expect(callbacks.onSaveUser).toHaveBeenCalledOnce();
    expect(callbacks.onStatusChange).toHaveBeenCalledWith("Work profile saved.");
  });

  it("calls onErrorChange when there is a plannerView validation error", () => {
    const callbacks = makeCallbacks();
    const { result } = renderHook(() =>
      useWorkProfile(userWithEmptyProfile(), callbacks),
    );

    act(() => {
      result.current.saveWork("06:00", "22:00", "Visible end must be after start.");
    });

    expect(callbacks.onSaveUser).not.toHaveBeenCalled();
    expect(callbacks.onErrorChange).toHaveBeenCalledWith("Visible end must be after start.");
  });

  it("calls onErrorChange when pendingSelection exists (user hasn't confirmed it)", () => {
    const callbacks = makeCallbacks();
    const { result } = renderHook(() =>
      useWorkProfile(userWithEmptyProfile(), callbacks),
    );

    act(() => {
      result.current.selectRange("Mon", "09:00", "10:00");
    });

    act(() => {
      result.current.saveWork("06:00", "22:00");
    });

    expect(callbacks.onSaveUser).not.toHaveBeenCalled();
    expect(callbacks.onErrorChange).toHaveBeenCalled();
  });

  it("isDirty becomes false when parent re-renders with the saved user", () => {
    // isDirty compares workForm against savedWorkProfile = createWorkProfileFromLegacyUser(user).
    // The hook expects the parent to pass the updated user back after saveWork calls onSaveUser.
    const callbacks = makeCallbacks();
    let user = userWithEmptyProfile();
    callbacks.onSaveUser.mockImplementation((nextUser: User) => {
      user = nextUser;
    });

    const { result, rerender } = renderHook(
      (u: User) => useWorkProfile(u, callbacks),
      { initialProps: user },
    );

    act(() => {
      result.current.addShift("Mon", "09:00", "17:00");
    });

    expect(result.current.isDirty).toBe(true);

    act(() => {
      result.current.saveWork("06:00", "22:00");
    });

    // Re-render with the saved user that onSaveUser received
    rerender(user);

    expect(result.current.isDirty).toBe(false);
  });
});

// ── useWorkProfile – onDirtyChange callback ───────────────────────────────────

describe("useWorkProfile – onDirtyChange callback", () => {
  it("calls onDirtyChange(true) when a shift is added", () => {
    const callbacks = makeCallbacks();
    const { result } = renderHook(() =>
      useWorkProfile(userWithEmptyProfile(), callbacks),
    );

    act(() => {
      result.current.addShift("Mon", "09:00", "17:00");
    });

    expect(callbacks.onDirtyChange).toHaveBeenCalledWith(true);
  });

  it("calls onDirtyChange(false) on unmount", () => {
    const callbacks = makeCallbacks();
    const { unmount } = renderHook(() =>
      useWorkProfile(userWithEmptyProfile(), callbacks),
    );

    unmount();

    expect(callbacks.onDirtyChange).toHaveBeenCalledWith(false);
  });
});
