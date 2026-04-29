import { afterEach, describe, expect, it, vi } from "vitest";
import { saveWorkProfile } from "./work-profile-api";
import type { WorkProfile } from "./types";

describe("work-profile-api", () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("sends planner view and max daily load fields when saving a work profile", async () => {
    const profile: WorkProfile = {
      plannerViewStart: "07:00",
      plannerViewEnd: "21:00",
      maxDailyLoad: "08:00:00",
      days: [
        {
          day: "Mon",
          blocks: [],
          breaks: [],
        },
      ],
    };

    const fetchMock = vi
      .spyOn(globalThis, "fetch")
      .mockResolvedValue(
        new Response(JSON.stringify(profile), {
          status: 200,
          headers: { "Content-Type": "application/json" },
        }),
      );

    await saveWorkProfile("user-1", profile);

    expect(fetchMock).toHaveBeenCalledOnce();
    const [, options] = fetchMock.mock.calls[0];
    expect(options?.method).toBe("PUT");
    expect(options?.body).toBe(JSON.stringify(profile));
  });
});
