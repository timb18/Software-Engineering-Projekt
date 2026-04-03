import { useEffect, useMemo, useState, type FC } from "react";
import { useNavigate } from "react-router";
import useLoginStore from "../../stores/login-store";
import useUserStore from "../../stores/user-store";
import type { WorkBlock, WorkBreak, WorkProfile, WorkWeekDay } from "../../util/types";
import {
  DAY_LABELS,
  WEEK_DAYS,
  createSuggestedBreak,
  createWorkBlock,
  createWorkProfileFromLegacyUser,
  duplicateWorkBlock,
  duplicateWorkBlocks,
  getCompanyOptions,
  getLegacyWorkSettings,
  getProductiveHoursForBlock,
  getWorkProfileSummary,
  normalizeWorkProfile,
  timeToMinutes,
  validateWorkProfile,
} from "../../util/work-profile";

type Tab = "general" | "work" | "security" | "account";

type CompanyTheme = {
  badge: string;
  border: string;
  surface: string;
  segment: string;
  accentText: string;
};

const WEEKDAY_DAYS: WorkWeekDay[] = ["Mon", "Tue", "Wed", "Thu", "Fri"];
const DAY_SHORT_LABELS: Record<WorkWeekDay, string> = {
  Mon: "Mon",
  Tue: "Tue",
  Wed: "Wed",
  Thu: "Thu",
  Fri: "Fri",
  Sat: "Sat",
  Sun: "Sun",
};
const COMPANY_THEMES: CompanyTheme[] = [
  {
    badge: "border-emerald-300/50 bg-emerald-400/15",
    border: "border-emerald-300/20",
    surface: "bg-emerald-400/6",
    segment: "bg-emerald-300",
    accentText: "text-emerald-100",
  },
  {
    badge: "border-sky-300/50 bg-sky-400/15",
    border: "border-sky-300/20",
    surface: "bg-sky-400/6",
    segment: "bg-sky-300",
    accentText: "text-sky-100",
  },
];

const sortBreaks = (breaks: WorkBreak[]) =>
  [...breaks].sort(
    (left, right) => timeToMinutes(left.startTime) - timeToMinutes(right.startTime),
  );

const sortBlocks = (blocks: WorkBlock[]) =>
  [...blocks]
    .map((block) => ({ ...block, breaks: sortBreaks(block.breaks) }))
    .sort((left, right) => timeToMinutes(left.startTime) - timeToMinutes(right.startTime));

const getCompanyTheme = (companyId: string, companyIds: string[]) => {
  const companyIndex = companyIds.findIndex((id) => id === companyId);
  if (companyIndex < 0) {
    return COMPANY_THEMES[0];
  }

  return COMPANY_THEMES[companyIndex % COMPANY_THEMES.length];
};

const getBreakMinutes = (workBreak: WorkBreak) => {
  const start = timeToMinutes(workBreak.startTime);
  const end = timeToMinutes(workBreak.endTime);

  if (Number.isNaN(start) || Number.isNaN(end) || end <= start) {
    return 0;
  }

  return end - start;
};

const getTotalBreakMinutes = (block: WorkBlock) =>
  block.breaks.reduce((total, workBreak) => total + getBreakMinutes(workBreak), 0);

const User: FC = () => {
  const { logout } = useLoginStore();
  const { user, setUser } = useUserStore();
  const navigate = useNavigate();

  const [tab, setTab] = useState<Tab>("general");
  const [status, setStatus] = useState<string | undefined>();
  const [error, setError] = useState<string | undefined>();
  const [pwdForm, setPwdForm] = useState({
    current: "",
    next: "",
    confirm: "",
  });
  const [profileForm, setProfileForm] = useState({
    displayName: user?.displayName ?? user?.username ?? "",
    email: user?.email ?? "",
    timezone: user?.timezone ?? "Europe/Berlin",
    profileImage: user?.profileImage ?? "gradient-1",
  });
  const [workForm, setWorkForm] = useState<WorkProfile>(() =>
    createWorkProfileFromLegacyUser(user),
  );
  const [notifForm, setNotifForm] = useState({
    emailInvites: user?.notifications?.emailInvites ?? true,
    emailDeadlines: user?.notifications?.emailDeadlines ?? true,
  });
  const [expandedBreakBlockIds, setExpandedBreakBlockIds] = useState<string[]>([]);
  const [copiedBlock, setCopiedBlock] = useState<WorkBlock | undefined>();

  useEffect(() => {
    if (!user) {
      navigate("/login");
    }
  }, [navigate, user]);

  useEffect(() => {
    if (!user) {
      return;
    }

    setProfileForm({
      displayName: user.displayName ?? user.username,
      email: user.email,
      timezone: user.timezone ?? "Europe/Berlin",
      profileImage: user.profileImage ?? "gradient-1",
    });
    setWorkForm(createWorkProfileFromLegacyUser(user));
    setNotifForm({
      emailInvites: user.notifications?.emailInvites ?? true,
      emailDeadlines: user.notifications?.emailDeadlines ?? true,
    });
    setExpandedBreakBlockIds([]);
  }, [user]);

  const avatarStyle = useMemo(() => {
    if (profileForm.profileImage?.startsWith("http")) {
      return {
        backgroundImage: `url(${profileForm.profileImage})`,
        backgroundSize: "cover",
      };
    }

    const gradients: Record<string, string> = {
      "gradient-1": "linear-gradient(135deg, #34d399, #2563eb)",
      "gradient-2": "linear-gradient(135deg, #ec4899, #8b5cf6)",
      "gradient-3": "linear-gradient(135deg, #f59e0b, #ef4444)",
    };

    return {
      backgroundImage: gradients[profileForm.profileImage ?? "gradient-1"],
    };
  }, [profileForm.profileImage]);

  const companyOptions = useMemo(
    () => getCompanyOptions(user?.teams ?? [], workForm),
    [user?.teams, workForm],
  );
  const companyIds = useMemo(() => companyOptions.map((company) => company.id), [companyOptions]);
  const workSummary = useMemo(() => getWorkProfileSummary(workForm), [workForm]);

  if (!user) {
    return <></>;
  }

  const persist = (nextUser = user) => {
    setUser(nextUser);
  };

  const replaceDayBlocks = (dayKey: WorkWeekDay, nextBlocks: WorkBlock[]) => {
    setWorkForm((current) => ({
      days: current.days.map((day) =>
        day.day === dayKey ? { ...day, blocks: sortBlocks(nextBlocks) } : day,
      ),
    }));
  };

  const updateWorkBlock = (
    dayKey: WorkWeekDay,
    blockId: string,
    updater: (block: WorkBlock) => WorkBlock,
  ) => {
    const day = workForm.days.find((entry) => entry.day === dayKey);
    if (!day) {
      return;
    }

    replaceDayBlocks(
      dayKey,
      day.blocks.map((block) => (block.id === blockId ? updater(block) : block)),
    );
  };

  const updateWorkBreak = (
    dayKey: WorkWeekDay,
    blockId: string,
    breakId: string,
    updater: (workBreak: WorkBreak) => WorkBreak,
  ) => {
    updateWorkBlock(dayKey, blockId, (block) => ({
      ...block,
      breaks: block.breaks.map((workBreak) =>
        workBreak.id === breakId ? updater(workBreak) : workBreak,
      ),
    }));
  };

  const toggleBreaks = (blockId: string) => {
    setExpandedBreakBlockIds((current) =>
      current.includes(blockId)
        ? current.filter((entry) => entry !== blockId)
        : [...current, blockId],
    );
  };

  const openBreaks = (blockId: string) => {
    setExpandedBreakBlockIds((current) =>
      current.includes(blockId) ? current : [...current, blockId],
    );
  };

  const addWorkBlock = (dayKey: WorkWeekDay) => {
    const day = workForm.days.find((entry) => entry.day === dayKey);
    if (!day) {
      return;
    }

    replaceDayBlocks(dayKey, [...day.blocks, createWorkBlock(companyOptions[0])]);
    setStatus(`${DAY_LABELS[dayKey]} now has a new shift block.`);
  };

  const removeWorkBlock = (dayKey: WorkWeekDay, blockId: string) => {
    const day = workForm.days.find((entry) => entry.day === dayKey);
    if (!day) {
      return;
    }

    replaceDayBlocks(
      dayKey,
      day.blocks.filter((block) => block.id !== blockId),
    );
    setExpandedBreakBlockIds((current) => current.filter((entry) => entry !== blockId));
  };

  const updateBlockCompany = (dayKey: WorkWeekDay, blockId: string, companyId: string) => {
    const company = companyOptions.find((option) => option.id === companyId);
    if (!company) {
      return;
    }

    updateWorkBlock(dayKey, blockId, (block) => ({
      ...block,
      companyId: company.id,
      companyName: company.name,
    }));
  };

  const addBreakToBlock = (dayKey: WorkWeekDay, blockId: string) => {
    updateWorkBlock(dayKey, blockId, (block) => ({
      ...block,
      breaks: [...block.breaks, createSuggestedBreak(block)],
    }));
    openBreaks(blockId);
  };

  const removeBreakFromBlock = (dayKey: WorkWeekDay, blockId: string, breakId: string) => {
    updateWorkBlock(dayKey, blockId, (block) => ({
      ...block,
      breaks: block.breaks.filter((workBreak) => workBreak.id !== breakId),
    }));
  };

  const copyBlockToClipboard = (block: WorkBlock) => {
    setCopiedBlock(block);
    setStatus(`${block.companyName || "Shift"} copied. Choose a day card and paste it.`);
  };

  const pasteCopiedBlock = (dayKey: WorkWeekDay) => {
    if (!copiedBlock) {
      return;
    }

    const day = workForm.days.find((entry) => entry.day === dayKey);
    if (!day) {
      return;
    }

    replaceDayBlocks(dayKey, [...day.blocks, duplicateWorkBlock(copiedBlock)]);
    setStatus(`Copied block inserted into ${DAY_LABELS[dayKey]}.`);
  };

  const duplicateDay = (dayKey: WorkWeekDay) => {
    const sourceDayIndex = WEEK_DAYS.indexOf(dayKey);
    const targetDayKey = WEEK_DAYS[sourceDayIndex + 1];
    const sourceDay = workForm.days.find((entry) => entry.day === dayKey);

    setStatus(undefined);
    setError(undefined);

    if (!sourceDay || sourceDay.blocks.length === 0) {
      setError(`No shifts configured on ${DAY_LABELS[dayKey]} to duplicate.`);
      return;
    }
    if (!targetDayKey) {
      setError("Sunday cannot be duplicated forward.");
      return;
    }

    replaceDayBlocks(targetDayKey, duplicateWorkBlocks(sourceDay.blocks));
    setStatus(`${DAY_LABELS[dayKey]} copied to ${DAY_LABELS[targetDayKey]}.`);
  };

  const applyDayToWeekdays = (dayKey: WorkWeekDay) => {
    const sourceDay = workForm.days.find((entry) => entry.day === dayKey);

    setStatus(undefined);
    setError(undefined);

    if (!sourceDay || sourceDay.blocks.length === 0) {
      setError(`No shifts configured on ${DAY_LABELS[dayKey]} to apply.`);
      return;
    }

    setWorkForm((current) => ({
      days: current.days.map((day) => {
        if (!WEEKDAY_DAYS.includes(day.day) || day.day === dayKey) {
          return day;
        }

        return {
          ...day,
          blocks: duplicateWorkBlocks(sourceDay.blocks),
        };
      }),
    }));
    setStatus(`${DAY_LABELS[dayKey]} applied to all weekdays.`);
  };

  const saveProfile = () => {
    setStatus(undefined);
    setError(undefined);

    if (!profileForm.displayName.trim()) {
      setError("Name can't be empty");
      return;
    }

    const nextUser = {
      ...user,
      displayName: profileForm.displayName.trim(),
      email: profileForm.email.trim(),
      timezone: profileForm.timezone,
      profileImage: profileForm.profileImage,
    };

    persist(nextUser);
    setStatus("Profile updated.");
  };

  const saveWork = () => {
    setStatus(undefined);
    setError(undefined);

    const normalizedWorkProfile = normalizeWorkProfile(workForm);
    const validationError = validateWorkProfile(normalizedWorkProfile);
    if (validationError) {
      setError(validationError);
      return;
    }

    const nextUser = {
      ...user,
      workProfile: normalizedWorkProfile,
      ...getLegacyWorkSettings(normalizedWorkProfile),
    };

    setWorkForm(normalizedWorkProfile);
    persist(nextUser);
    setStatus("Work profile saved.");
  };

  const saveNotifications = () => {
    const nextUser = {
      ...user,
      notifications: {
        emailInvites: notifForm.emailInvites,
        emailDeadlines: notifForm.emailDeadlines,
      },
    };

    persist(nextUser);
    setStatus("Notifications updated.");
  };

  const logOut = () => {
    logout();
    navigate("/login");
  };

  const logoutAll = () => {
    setStatus("all sessions closed (Demo: localStorage JWT gelöscht)");
    localStorage.removeItem("token");
  };

  const updatePassword = () => {
    setError(undefined);
    setStatus(undefined);

    if (!pwdForm.current || !pwdForm.next || !pwdForm.confirm) {
      setError("Pleas fill in all the fields.");
      return;
    }
    if (pwdForm.next.length < 8) {
      setError("Password needs to contain at least 8 symbols.");
      return;
    }
    if (pwdForm.next !== pwdForm.confirm) {
      setError("New passwords don't match.");
      return;
    }

    setStatus("Password was succesfully changed.");
    setPwdForm({ current: "", next: "", confirm: "" });
  };

  const deleteAccount = () => {
    const confirmed = window.confirm("Account löschen? Dies loggt dich aus. (Demo)");
    if (!confirmed) {
      return;
    }

    logout();
    navigate("/login");
  };

  const timezones = [
    "Europe/Berlin",
    "UTC",
    "Europe/Vienna",
    "Europe/Zurich",
    "America/New_York",
  ];

  return (
    <div className="grid h-full w-full grid-rows-[3.5rem_1fr] gap-6 p-6 text-slate-50">
      <div className="flex items-center justify-between">
        <div className="flex flex-col gap-1">
          <span className="text-xs tracking-[0.28em] text-emerald-300 uppercase">Profile</span>
          <h1 className="text-4xl leading-tight font-semibold">My Profile</h1>
          <span className="text-sm text-slate-400">
            Manage account, shifts, security and notifications
          </span>
        </div>
        <div className="flex gap-2 text-sm">
          {(["general", "work", "security", "account"] as Tab[]).map((currentTab) => (
            <button
              key={currentTab}
              onClick={() => setTab(currentTab)}
              className={`rounded-full px-4 py-2 font-semibold transition ${
                tab === currentTab
                  ? "border border-emerald-300/60 bg-emerald-400/15 text-emerald-100"
                  : "border border-slate-800 bg-slate-900/60 text-slate-300 hover:border-emerald-300/40 hover:text-emerald-100"
              }`}
            >
              {currentTab === "general" && "General"}
              {currentTab === "work" && "Work profile"}
              {currentTab === "security" && "Security"}
              {currentTab === "account" && "Account"}
            </button>
          ))}
        </div>
      </div>

      <div className="rounded-3xl border border-slate-800 bg-slate-900/70 p-6 shadow-2xl">
        {tab === "general" && (
          <div className="grid grid-cols-1 gap-6 lg:grid-cols-[1.1fr_0.9fr]">
            <div className="flex flex-col gap-4">
              <div className="flex items-center gap-4 rounded-2xl border border-slate-800 bg-slate-900/80 p-4">
                <div className="relative">
                  <div
                    className="aspect-square w-24 rounded-full border border-slate-700"
                    style={avatarStyle}
                  ></div>
                </div>
                <div className="flex flex-col gap-2 text-sm">
                  <div className="text-xs tracking-[0.14em] text-slate-500 uppercase">
                    Profile Picture
                  </div>
                  <div className="flex gap-2">
                    {["gradient-1", "gradient-2", "gradient-3"].map((gradient) => (
                      <button
                        key={gradient}
                        onClick={() =>
                          setProfileForm({ ...profileForm, profileImage: gradient })
                        }
                        className={`h-10 w-10 rounded-full border ${
                          profileForm.profileImage === gradient
                            ? "border-emerald-300"
                            : "border-slate-700"
                        }`}
                        style={{
                          backgroundImage:
                            gradient === "gradient-1"
                              ? "linear-gradient(135deg, #34d399, #2563eb)"
                              : gradient === "gradient-2"
                                ? "linear-gradient(135deg, #ec4899, #8b5cf6)"
                                : "linear-gradient(135deg, #f59e0b, #ef4444)",
                        }}
                      />
                    ))}
                  </div>
                  <input
                    type="url"
                    placeholder="Bild-URL (optional)"
                    className="rounded-xl border border-slate-800 bg-slate-900/80 px-3 py-2 text-xs text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
                    value={
                      profileForm.profileImage.startsWith("http")
                        ? profileForm.profileImage
                        : ""
                    }
                    onChange={(event) =>
                      setProfileForm({
                        ...profileForm,
                        profileImage: event.target.value,
                      })
                    }
                  />
                </div>
              </div>

              <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
                <div className="flex flex-col gap-1">
                  <label className="text-xs tracking-[0.14em] text-slate-500 uppercase">
                    Display name
                  </label>
                  <input
                    value={profileForm.displayName}
                    onChange={(event) =>
                      setProfileForm({
                        ...profileForm,
                        displayName: event.target.value,
                      })
                    }
                    className="rounded-xl border border-slate-800 bg-slate-900/80 px-3 py-2 text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
                  />
                </div>
                <div className="flex flex-col gap-1">
                  <label className="text-xs tracking-[0.14em] text-slate-500 uppercase">
                    E-Mail
                  </label>
                  <input
                    type="email"
                    value={profileForm.email}
                    onChange={(event) =>
                      setProfileForm({ ...profileForm, email: event.target.value })
                    }
                    className="rounded-xl border border-slate-800 bg-slate-900/80 px-3 py-2 text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
                  />
                </div>
              </div>

              <div className="flex flex-col gap-1">
                <label className="text-xs tracking-[0.14em] text-slate-500 uppercase">
                  Timezone
                </label>
                <select
                  value={profileForm.timezone}
                  onChange={(event) =>
                    setProfileForm({ ...profileForm, timezone: event.target.value })
                  }
                  className="rounded-xl border border-slate-800 bg-slate-900/80 px-3 py-2 text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
                >
                  {timezones.map((timezone) => (
                    <option key={timezone} value={timezone}>
                      {timezone}
                    </option>
                  ))}
                </select>
              </div>

              <button
                onClick={saveProfile}
                className="w-fit rounded-xl border border-emerald-300/60 bg-emerald-400/15 px-4 py-2 text-sm font-semibold text-emerald-100 shadow-sm transition hover:bg-emerald-400/25"
              >
                Save changes
              </button>
            </div>

            <div className="flex flex-col gap-4 rounded-2xl border border-slate-800 bg-slate-900/80 p-4">
              <div className="text-sm font-semibold text-slate-100">Account & Security</div>
              <div className="text-sm text-slate-300">Role: {user.role}</div>
              <div className="text-sm text-slate-300">Username: {user.username}</div>
            </div>
          </div>
        )}

        {tab === "work" && (
          <div className="flex flex-col gap-5">
            <div className="rounded-3xl border border-slate-800 bg-slate-900/80 p-5 text-sm text-slate-300 shadow-lg">
              <div className="flex flex-wrap items-start justify-between gap-4">
                <div>
                  <div className="text-sm font-semibold text-slate-100">Shift Builder</div>
                    <p className="mt-2 max-w-3xl text-slate-400">Build the work week day by day.</p>
                </div>
                <div className="flex flex-wrap gap-2">
                  {companyOptions.map((company) => {
                    const theme = getCompanyTheme(company.id, companyIds);
                    return (
                      <span
                        key={company.id}
                        className={`rounded-full border px-3 py-1 text-xs font-semibold ${theme.badge} ${theme.accentText}`}
                      >
                        {company.name}
                      </span>
                    );
                  })}
                </div>
              </div>

              <div className="mt-4 grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
                <div className="rounded-2xl border border-slate-800 bg-slate-950/60 p-3">
                  <div className="text-[11px] uppercase tracking-[0.18em] text-slate-500">
                    Active Days
                  </div>
                  <div className="mt-2 text-2xl font-semibold text-slate-50">
                    {workSummary.activeDayCount}
                  </div>
                </div>
                <div className="rounded-2xl border border-slate-800 bg-slate-950/60 p-3">
                  <div className="text-[11px] uppercase tracking-[0.18em] text-slate-500">
                    Weekly Hours
                  </div>
                  <div className="mt-2 text-2xl font-semibold text-slate-50">
                    {workSummary.weeklyHours}
                  </div>
                </div>
                <div className="rounded-2xl border border-slate-800 bg-slate-950/60 p-3">
                  <div className="text-[11px] uppercase tracking-[0.18em] text-slate-500">
                    Total Shifts
                  </div>
                  <div className="mt-2 text-2xl font-semibold text-slate-50">
                    {workSummary.totalBlocks}
                  </div>
                </div>
              </div>

              <div className="mt-4 flex flex-wrap gap-3 text-xs text-slate-500">
                {copiedBlock && (
                  <span className="rounded-full border border-emerald-300/20 bg-emerald-400/8 px-3 py-1 text-emerald-100">
                    Clipboard: {copiedBlock.companyName} {copiedBlock.startTime}-{copiedBlock.endTime}
                  </span>
                )}
              </div>
            </div>

            <div className="grid gap-4 xl:grid-cols-2">
              {workForm.days.map((day) => {
                const hasBlocks = day.blocks.length > 0;
                const canDuplicateForward = WEEK_DAYS.indexOf(day.day) < WEEK_DAYS.length - 1;

                return (
                  <section
                    key={day.day}
                    className="rounded-3xl border border-slate-800 bg-slate-900/80 p-4 shadow-lg"
                  >
                    <div className="flex items-start justify-between gap-3">
                      <div>
                        <div className="flex items-center gap-2">
                          <span className="rounded-full border border-slate-700 bg-slate-950/70 px-2 py-1 text-[11px] font-semibold uppercase tracking-[0.16em] text-slate-300">
                            {DAY_SHORT_LABELS[day.day]}
                          </span>
                          <div className="text-lg font-semibold text-slate-100">
                            {DAY_LABELS[day.day]}
                          </div>
                        </div>
                        <div className="mt-2 flex flex-wrap gap-2 text-xs text-slate-400">
                          <span className="rounded-full border border-slate-800 bg-slate-950/60 px-2 py-1">
                            {hasBlocks ? `${day.blocks.length} shifts` : "Day off"}
                          </span>
                          <span className="rounded-full border border-slate-800 bg-slate-950/60 px-2 py-1">
                            {workSummary.dailyHours[day.day]} productive h
                          </span>
                        </div>
                      </div>

                      <div className="flex items-center gap-2">
                        <details className="relative">
                          <summary className="list-none cursor-pointer rounded-full border border-slate-700 bg-slate-950/70 px-3 py-2 text-xs font-semibold text-slate-200 transition hover:border-emerald-300/50 hover:text-emerald-100">
                            Actions
                          </summary>
                          <div className="absolute right-0 z-20 mt-2 w-48 rounded-2xl border border-slate-800 bg-slate-950/95 p-2 shadow-2xl">
                            <button
                              onClick={() => duplicateDay(day.day)}
                              disabled={!hasBlocks || !canDuplicateForward}
                              className="w-full rounded-xl px-3 py-2 text-left text-xs font-semibold text-slate-200 transition hover:bg-slate-900 disabled:cursor-not-allowed disabled:opacity-40"
                            >
                              Duplicate to next day
                            </button>
                            <button
                              onClick={() => applyDayToWeekdays(day.day)}
                              disabled={!hasBlocks}
                              className="mt-1 w-full rounded-xl px-3 py-2 text-left text-xs font-semibold text-slate-200 transition hover:bg-slate-900 disabled:cursor-not-allowed disabled:opacity-40"
                            >
                              Apply to weekdays
                            </button>
                            <button
                              onClick={() => pasteCopiedBlock(day.day)}
                              disabled={!copiedBlock}
                              className="mt-1 w-full rounded-xl px-3 py-2 text-left text-xs font-semibold text-slate-200 transition hover:bg-slate-900 disabled:cursor-not-allowed disabled:opacity-40"
                            >
                              Paste copied block
                            </button>
                          </div>
                        </details>

                        <button
                          onClick={() => addWorkBlock(day.day)}
                          className="rounded-full border border-emerald-300/60 bg-emerald-400/15 px-3 py-2 text-xs font-semibold text-emerald-100 transition hover:bg-emerald-400/25"
                        >
                          Add shift
                        </button>
                      </div>
                    </div>

                    {!hasBlocks && (
                      <div className="mt-4 rounded-2xl border border-dashed border-slate-700 bg-slate-950/60 p-4 text-sm text-slate-500">
                        Free day. Add a shift when you work on this weekday.
                      </div>
                    )}

                    {hasBlocks && (
                      <div className="mt-4 flex flex-col gap-3">
                        {day.blocks.map((block, blockIndex) => {
                          const theme = getCompanyTheme(block.companyId, companyIds);
                          const blockProductiveHours = getProductiveHoursForBlock(block);
                          const totalBreakMinutes = getTotalBreakMinutes(block);
                          const breaksExpanded = expandedBreakBlockIds.includes(block.id);

                          return (
                            <article
                              key={block.id}
                              className={`rounded-2xl border p-3 shadow-sm ${theme.border} ${theme.surface}`}
                            >
                              <div className="flex items-start gap-3">
                                <div className={`mt-1 h-12 w-1 rounded-full ${theme.segment}`}></div>

                                <div className="min-w-0 flex-1">
                                  <div className="flex flex-wrap items-center justify-between gap-2">
                                    <div className="flex items-center gap-2 text-xs">
                                      <span className={`rounded-full border px-2 py-1 font-semibold ${theme.badge} ${theme.accentText}`}>
                                        Shift {blockIndex + 1}
                                      </span>
                                      <span className="text-slate-400">
                                        {block.startTime} - {block.endTime}
                                      </span>
                                    </div>
                                    <div className="flex gap-2">
                                      <button
                                        onClick={() => copyBlockToClipboard(block)}
                                        className="rounded-full border border-slate-700 bg-slate-950/70 px-3 py-1 text-[11px] font-semibold text-slate-200 transition hover:border-emerald-300/50 hover:text-emerald-100"
                                      >
                                        Copy
                                      </button>
                                      <button
                                        onClick={() => removeWorkBlock(day.day, block.id)}
                                        className="rounded-full border border-slate-700 bg-slate-950/70 px-3 py-1 text-[11px] font-semibold text-slate-300 transition hover:border-rose-300/40 hover:text-rose-100"
                                      >
                                        Remove
                                      </button>
                                    </div>
                                  </div>

                                  <div className="mt-3 grid gap-2 md:grid-cols-[minmax(0,1fr)_7rem_7rem]">
                                    <select
                                      value={block.companyId}
                                      onChange={(event) =>
                                        updateBlockCompany(day.day, block.id, event.target.value)
                                      }
                                      className="rounded-xl border border-slate-800 bg-slate-950/70 px-3 py-2 text-sm text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
                                    >
                                      {companyOptions.map((company) => (
                                        <option key={company.id} value={company.id}>
                                          {company.name}
                                        </option>
                                      ))}
                                    </select>
                                    <input
                                      type="time"
                                      value={block.startTime}
                                      onChange={(event) =>
                                        updateWorkBlock(day.day, block.id, (currentBlock) => ({
                                          ...currentBlock,
                                          startTime: event.target.value,
                                        }))
                                      }
                                      className="rounded-xl border border-slate-800 bg-slate-950/70 px-3 py-2 text-sm text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
                                    />
                                    <input
                                      type="time"
                                      value={block.endTime}
                                      onChange={(event) =>
                                        updateWorkBlock(day.day, block.id, (currentBlock) => ({
                                          ...currentBlock,
                                          endTime: event.target.value,
                                        }))
                                      }
                                      className="rounded-xl border border-slate-800 bg-slate-950/70 px-3 py-2 text-sm text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
                                    />
                                  </div>

                                  <div className="mt-3 flex flex-wrap items-center justify-between gap-2 text-xs text-slate-400">
                                    <div className="flex flex-wrap gap-2">
                                      <span className="rounded-full border border-slate-800 bg-slate-950/70 px-2 py-1">
                                        {blockProductiveHours} productive h
                                      </span>
                                      <span className="rounded-full border border-slate-800 bg-slate-950/70 px-2 py-1">
                                        {block.breaks.length} breaks
                                      </span>
                                      {totalBreakMinutes > 0 && (
                                        <span className="rounded-full border border-slate-800 bg-slate-950/70 px-2 py-1">
                                          {totalBreakMinutes} min pause
                                        </span>
                                      )}
                                    </div>

                                    <div className="flex flex-wrap gap-2">
                                      <button
                                        onClick={() => addBreakToBlock(day.day, block.id)}
                                        className="rounded-full border border-slate-700 bg-slate-950/70 px-3 py-1 text-[11px] font-semibold text-slate-200 transition hover:border-emerald-300/50 hover:text-emerald-100"
                                      >
                                        Add break
                                      </button>
                                      <button
                                        onClick={() => toggleBreaks(block.id)}
                                        className="rounded-full border border-slate-700 bg-slate-950/70 px-3 py-1 text-[11px] font-semibold text-slate-200 transition hover:border-emerald-300/50 hover:text-emerald-100"
                                      >
                                        {breaksExpanded ? "Hide breaks" : `Breaks (${block.breaks.length})`}
                                      </button>
                                    </div>
                                  </div>

                                  {breaksExpanded && (
                                    <div className="mt-3 rounded-xl border border-slate-800 bg-slate-950/70 p-3">
                                      {block.breaks.length === 0 && (
                                        <div className="text-sm text-slate-500">
                                          No breaks configured for this shift.
                                        </div>
                                      )}

                                      {block.breaks.length > 0 && (
                                        <div className="flex flex-col gap-2">
                                          {block.breaks.map((workBreak, breakIndex) => (
                                            <div
                                              key={workBreak.id}
                                              className="grid gap-2 rounded-xl border border-slate-800 bg-slate-900/70 p-3 md:grid-cols-[1fr_1fr_auto]"
                                            >
                                              <div className="flex flex-col gap-1">
                                                <label className="text-[11px] uppercase tracking-[0.14em] text-slate-500">
                                                  Break {breakIndex + 1} start
                                                </label>
                                                <input
                                                  type="time"
                                                  value={workBreak.startTime}
                                                  onChange={(event) =>
                                                    updateWorkBreak(
                                                      day.day,
                                                      block.id,
                                                      workBreak.id,
                                                      (currentBreak) => ({
                                                        ...currentBreak,
                                                        startTime: event.target.value,
                                                      }),
                                                    )
                                                  }
                                                  className="rounded-xl border border-slate-800 bg-slate-950/70 px-3 py-2 text-sm text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
                                                />
                                              </div>
                                              <div className="flex flex-col gap-1">
                                                <label className="text-[11px] uppercase tracking-[0.14em] text-slate-500">
                                                  Break {breakIndex + 1} end
                                                </label>
                                                <input
                                                  type="time"
                                                  value={workBreak.endTime}
                                                  onChange={(event) =>
                                                    updateWorkBreak(
                                                      day.day,
                                                      block.id,
                                                      workBreak.id,
                                                      (currentBreak) => ({
                                                        ...currentBreak,
                                                        endTime: event.target.value,
                                                      }),
                                                    )
                                                  }
                                                  className="rounded-xl border border-slate-800 bg-slate-950/70 px-3 py-2 text-sm text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
                                                />
                                              </div>
                                              <div className="flex items-end">
                                                <button
                                                  onClick={() =>
                                                    removeBreakFromBlock(day.day, block.id, workBreak.id)
                                                  }
                                                  className="w-full rounded-xl border border-slate-700 bg-slate-950/70 px-3 py-2 text-xs font-semibold text-slate-300 transition hover:border-rose-300/40 hover:text-rose-100"
                                                >
                                                  Remove
                                                </button>
                                              </div>
                                            </div>
                                          ))}
                                        </div>
                                      )}
                                    </div>
                                  )}
                                </div>
                              </div>
                            </article>
                          );
                        })}
                      </div>
                    )}
                  </section>
                );
              })}
            </div>

            <div className="flex items-center justify-between gap-3">
              <button
                onClick={saveWork}
                className="w-fit rounded-xl border border-emerald-300/60 bg-emerald-400/15 px-4 py-2 text-sm font-semibold text-emerald-100 shadow-sm transition hover:bg-emerald-400/25"
              >
                Save work profile
              </button>
            </div>
          </div>
        )}

        {tab === "security" && (
          <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
            <div className="rounded-2xl border border-slate-800 bg-slate-900/80 p-5">
              <div className="text-sm tracking-[0.16em] text-slate-400 uppercase">
                Change Password
              </div>
              <div className="mt-4 flex flex-col gap-3 text-sm text-slate-200">
                <div className="flex flex-col gap-1">
                  <span className="text-xs tracking-[0.14em] text-slate-500 uppercase">
                    Current password
                  </span>
                  <input
                    type="password"
                    value={pwdForm.current}
                    onChange={(event) =>
                      setPwdForm({ ...pwdForm, current: event.target.value })
                    }
                    className="rounded-xl border border-slate-800 bg-slate-900/80 px-3 py-2 text-slate-100 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
                  />
                </div>
                <div className="flex flex-col gap-1">
                  <span className="text-xs tracking-[0.14em] text-slate-500 uppercase">
                    New password
                  </span>
                  <input
                    type="password"
                    value={pwdForm.next}
                    onChange={(event) =>
                      setPwdForm({ ...pwdForm, next: event.target.value })
                    }
                    className="rounded-xl border border-slate-800 bg-slate-900/80 px-3 py-2 text-slate-100 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
                  />
                </div>
                <div className="flex flex-col gap-1">
                  <span className="text-xs tracking-[0.14em] text-slate-500 uppercase">
                    Confirm password
                  </span>
                  <input
                    type="password"
                    value={pwdForm.confirm}
                    onChange={(event) =>
                      setPwdForm({ ...pwdForm, confirm: event.target.value })
                    }
                    className="rounded-xl border border-slate-800 bg-slate-900/80 px-3 py-2 text-slate-100 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
                  />
                </div>
                <button
                  onClick={updatePassword}
                  className="w-fit rounded-xl border border-emerald-300/60 bg-emerald-400/10 px-4 py-2 text-sm font-semibold text-emerald-100 shadow-sm transition hover:bg-emerald-400/20"
                >
                  Passwort ändern
                </button>
              </div>
            </div>

            <div className="rounded-2xl border border-slate-800 bg-slate-900/80 p-5 text-sm text-slate-200">
              <div className="text-sm font-semibold text-slate-100">Sessions & Logout</div>
              <div className="mt-3 flex flex-col gap-2">
                <button
                  onClick={logOut}
                  className="w-fit rounded-full border border-slate-700 bg-slate-900/70 px-4 py-2 text-sm text-slate-100 transition hover:border-emerald-300/60 hover:text-emerald-100"
                >
                  Logout
                </button>
                <button
                  onClick={logoutAll}
                  className="w-fit rounded-full border border-slate-700 bg-slate-900/70 px-4 py-2 text-sm text-slate-100 transition hover:border-emerald-300/60 hover:text-emerald-100"
                >
                  Logout all sessions
                </button>
              </div>
            </div>
          </div>
        )}

        {tab === "account" && (
          <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
            <div className="rounded-2xl border border-slate-800 bg-slate-900/80 p-5">
              <div className="text-sm font-semibold text-slate-100">E-Mail-notifications</div>
              <div className="mt-3 flex flex-col gap-3 text-sm text-slate-200">
                <label className="flex items-center justify-between rounded-xl border border-slate-800 bg-slate-900/70 px-3 py-2">
                  <span>Invitations over E-Mail</span>
                  <input
                    type="checkbox"
                    checked={notifForm.emailInvites}
                    onChange={(event) =>
                      setNotifForm({
                        ...notifForm,
                        emailInvites: event.target.checked,
                      })
                    }
                  />
                </label>
                <label className="flex items-center justify-between rounded-xl border border-slate-800 bg-slate-900/70 px-3 py-2">
                  <span>Deadline-warnings</span>
                  <input
                    type="checkbox"
                    checked={notifForm.emailDeadlines}
                    onChange={(event) =>
                      setNotifForm({
                        ...notifForm,
                        emailDeadlines: event.target.checked,
                      })
                    }
                  />
                </label>
                <button
                  onClick={saveNotifications}
                  className="w-fit rounded-xl border border-emerald-300/60 bg-emerald-400/15 px-4 py-2 text-sm font-semibold text-emerald-100 shadow-sm transition hover:bg-emerald-400/25"
                >
                  Save
                </button>
              </div>
            </div>

            <div className="rounded-2xl border border-rose-400/40 bg-rose-500/10 p-5 text-sm text-rose-50">
              <div className="text-sm font-semibold">Danger area</div>
              <button
                onClick={deleteAccount}
                className="mt-4 w-fit rounded-xl border border-rose-300/60 bg-rose-500/20 px-4 py-2 text-sm font-semibold text-rose-50 transition hover:bg-rose-500/30"
              >
                Delete account
              </button>
            </div>
          </div>
        )}
      </div>

      {(status || error) && (
        <div className="rounded-2xl border border-slate-800 bg-slate-900/70 p-4 text-sm shadow">
          {status && <div className="text-emerald-200">{status}</div>}
          {error && <div className="text-rose-300">{error}</div>}
        </div>
      )}
    </div>
  );
};

export default User;
