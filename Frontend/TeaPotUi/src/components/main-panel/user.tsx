import { useEffect, useState, type FC } from "react";
import { useBlocker, useNavigate } from "react-router";
import useLoginStore from "../../stores/login-store";
import useUserStore from "../../stores/user-store";
import { useAuth0 } from "@auth0/auth0-react";
import WorkProfileConfigurator from "./work-profile-configurator";
import { defaultUser } from "../../util/default-data";
import { saveWorkProfile } from "../../util/work-profile-api";
import { getLegacyWorkSettings } from "../../util/work-profile";
import { updateUserProfile } from "../../util/user-api";
import { HexColorPicker, HexColorInput } from "react-colorful";
import {
  getBreakColor,
  getBlockerColor,
  getOrgColor,
  parseColorPreference,
  parseOrgColorPreferences,
  setBreakColor,
  setBlockerColor,
  setOrgColor,
  rgbToHex,
  hexToRgb,
  serializeColorPreference,
  serializeOrgColorPreferences,
  DEFAULT_ORG_COLOR,
  DEFAULT_BREAK_COLOR,
  DEFAULT_BLOCKER_COLOR,
  type RgbColor,
} from "../../util/color-prefs";
import { useForm, type FormValidateResult } from "react-hook-form";
import { changePassword } from "../../util/management-api";

type ChangePassword = {
  newPassword: string;
  confirmPassword: string;
};

type Tab = "general" | "work" | "security" | "account" | "appearance";

// ── Preset palette ────────────────────────────────────────────────────────────
const PALETTE_PRESETS = [
  "#10b981",
  "#3b82f6",
  "#8b5cf6",
  "#ec4899",
  "#f59e0b",
  "#ef4444",
  "#06b6d4",
  "#84cc16",
  "#f97316",
  "#64748b",
  "#a78bfa",
  "#fb7185",
];

// ── Color picker card ─────────────────────────────────────────────────────────
const ColorPickerCard: FC<{
  label: string;
  color: RgbColor;
  onChange: (c: RgbColor) => void;
  onReset: () => void;
}> = ({ label, color, onChange, onReset }) => {
  const hex = rgbToHex(color);
  return (
    <div className="rounded-2xl border border-slate-800 bg-slate-900/80 p-5">
      <div className="mb-4 flex items-center gap-3">
        <div
          className="h-6 w-6 shrink-0 rounded-full border border-slate-600 shadow-sm"
          style={{ background: hex }}
        />
        <div className="text-sm font-semibold text-slate-100">{label}</div>
        <button
          onClick={onReset}
          className="ml-auto rounded-full border border-slate-700 bg-slate-800 px-3 py-1 text-xs text-slate-300 hover:border-slate-500 hover:text-slate-100"
        >
          Reset
        </button>
      </div>

      {/* react-colorful saturation+hue picker */}
      <HexColorPicker
        color={hex}
        onChange={(h) => onChange(hexToRgb(h))}
        style={{ width: "100%", height: 180 }}
      />

      {/* Hex input */}
      <div className="mt-3 flex items-center gap-2">
        <span className="text-xs text-slate-500">#</span>
        <HexColorInput
          color={hex}
          onChange={(h) => onChange(hexToRgb(h))}
          prefixed={false}
          className="w-24 rounded-lg border border-slate-700 bg-slate-950/60 px-2 py-1 font-mono text-xs text-slate-50 uppercase ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
        />
        <div
          className="h-6 w-6 shrink-0 rounded border border-slate-600"
          style={{ background: hex }}
        />
        <span className="text-xs text-slate-500">
          rgb({color.r}, {color.g}, {color.b})
        </span>
      </div>

      {/* Preset swatches */}
      <div className="mt-4">
        <div className="mb-2 text-[10px] tracking-[0.15em] text-slate-500 uppercase">
          Presets
        </div>
        <div className="flex flex-wrap gap-2">
          {PALETTE_PRESETS.map((p) => (
            <button
              key={p}
              title={p}
              onClick={() => onChange(hexToRgb(p))}
              className="h-7 w-7 shrink-0 rounded-full border-2 transition hover:scale-110"
              style={{
                background: p,
                borderColor: hex.toLowerCase() === p ? "white" : "transparent",
              }}
            />
          ))}
        </div>
      </div>
    </div>
  );
};

const User: FC = () => {
  const { logout } = useLoginStore();
  const {
    user: userFromDb,
    setUser,
  } = useUserStore();
  const {
    logout: authLogout,
    user: userFromAuth,
    getAccessTokenSilently,
  } = useAuth0();
  const navigate = useNavigate();
  const {
    register: registerPwChange,
    handleSubmit: handlePwChange,
    formState: { errors: changePwError },
  } = useForm<ChangePassword>({
    validate: ({ formValues }): FormValidateResult<ChangePassword> => {
      if (formValues.newPassword !== formValues.confirmPassword) {
        return "new and confirmationpassword must be equal";
      }

      return true;
    },
  });

  const [tab, setTab] = useState<Tab>("general");

  const [isWorkDirty, setIsWorkDirty] = useState(false);
  const [isSavingWorkProfile, setIsSavingWorkProfile] = useState(false);
  const [pendingTabChange, setPendingTabChange] = useState<Tab | undefined>();
  const [status, setStatus] = useState<string | undefined>();
  const [error, setError] = useState<string | undefined>();
  const [isSavingProfile, setIsSavingProfile] = useState(false);
  const [isSavingAppearance, setIsSavingAppearance] = useState(false);
  const [isAppearanceDirty, setIsAppearanceDirty] = useState(false);
  const [profileForm, setProfileForm] = useState({
    displayName: userFromDb.displayName ?? userFromDb.username,
    email: userFromDb.email,
    timezone: userFromDb.timezone ?? "Europe/Berlin",
    profileImageUrl: userFromDb.profileImage ?? "",
  });
  const [showDeleteWorkProfileDialog, setShowDeleteWorkProfileDialog] =
    useState(false);
  const [isDeletingWorkProfile, setIsDeletingWorkProfile] = useState(false);

  // ── Appearance / color state ──────────────────────────────────────────
  const initOrgColors = () => {
    const savedOrgColors = parseOrgColorPreferences(
      userFromDb.appearanceOrgColors,
    );
    const map: Record<string, RgbColor> = {};
    for (const org of userFromDb.orgs ?? []) {
      map[org.id] = savedOrgColors[org.id] ?? getOrgColor(org.id);
    }
    return map;
  };
  const [orgColors, setOrgColorsState] =
    useState<Record<string, RgbColor>>(initOrgColors);
  const [breakColorState, setBreakColorState] = useState<RgbColor>(() =>
    parseColorPreference(userFromDb.appearanceBreakColor, getBreakColor()),
  );
  const [blockerColorState, setBlockerColorState] = useState<RgbColor>(() =>
    parseColorPreference(userFromDb.appearanceBlockerColor, getBlockerColor()),
  );

  const updateOrgColor = (orgId: string, color: RgbColor) => {
    setOrgColorsState((prev) => ({ ...prev, [orgId]: color }));
    setIsAppearanceDirty(true);
  };
  const updateBreakColorState = (color: RgbColor) => {
    setBreakColorState(color);
    setIsAppearanceDirty(true);
  };
  const updateBlockerColorState = (color: RgbColor) => {
    setBlockerColorState(color);
    setIsAppearanceDirty(true);
  };
  // ─────────────────────────────────────────────────────────────────────

  const defaultWorkProfile = {
    capacity: defaultUser.workCapacityHours ?? 8,
    workDays: defaultUser.workDays ?? ["Mon", "Tue", "Wed", "Thu", "Fri"],
    workStart: defaultUser.workStart ?? "09:00",
    workEnd: defaultUser.workEnd ?? "17:00",
    breakRules: defaultUser.breakRules ?? "30m lunch",
  };
  const hasBackendUserId = (value: string) =>
    /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(
      value,
    );

  const toTimeSpanString = (hours?: number) => {
    const safeHours = Math.max(0, hours ?? 0);
    const wholeHours = Math.floor(safeHours);
    const minutes = Math.round((safeHours - wholeHours) * 60);
    return `${wholeHours.toString().padStart(2, "0")}:${minutes.toString().padStart(2, "0")}:00`;
  };

  useEffect(() => {
    setProfileForm({
      displayName: userFromDb.displayName ?? userFromDb.username,
      email: userFromDb.email,
      timezone: userFromDb.timezone ?? "Europe/Berlin",
      profileImageUrl: userFromDb.profileImage ?? userFromAuth?.picture ?? "",
    });
  }, [userFromAuth?.picture, userFromDb]);

  useEffect(() => {
    setOrgColorsState(initOrgColors());
    setBreakColorState(
      parseColorPreference(userFromDb.appearanceBreakColor, getBreakColor()),
    );
    setBlockerColorState(
      parseColorPreference(userFromDb.appearanceBlockerColor, getBlockerColor()),
    );
    setIsAppearanceDirty(false);
  }, [userFromDb.appearanceBlockerColor,
    userFromDb.appearanceBreakColor,
    userFromDb.appearanceOrgColors,
    userFromDb.orgs,
  ]);

  /* const avatarStyle = useMemo(() => {
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
  }, [profileForm.profileImage]); */

  const persist = async (nextUser = userFromDb) => {
    if (nextUser.workProfile && nextUser.id && hasBackendUserId(nextUser.id)) {
      setIsSavingWorkProfile(true);
      try {
        const token = await getAccessTokenSilently();
        const savedWorkProfile = await saveWorkProfile(
          nextUser.id,
          {
            ...nextUser.workProfile,
            plannerViewStart: nextUser.plannerViewStart,
            plannerViewEnd: nextUser.plannerViewEnd,
            maxDailyLoad: toTimeSpanString(nextUser.workCapacityHours),
          },
          token,
        );
        const legacyWorkSettings = getLegacyWorkSettings(savedWorkProfile);
        const orgs = savedWorkProfile.id
          ? nextUser.orgs.map((org) => ({ ...org, workProfileId: savedWorkProfile.id }))
          : nextUser.orgs;
        setUser({
          ...nextUser,
          orgs,
          workProfile: savedWorkProfile,
          hasPersistedWorkProfile: true,
          plannerViewStart:
            savedWorkProfile.plannerViewStart ?? nextUser.plannerViewStart,
          plannerViewEnd:
            savedWorkProfile.plannerViewEnd ?? nextUser.plannerViewEnd,
          workCapacityHours: legacyWorkSettings.workCapacityHours,
          workDays: legacyWorkSettings.workDays,
          workStart: legacyWorkSettings.workStart,
          workEnd: legacyWorkSettings.workEnd,
          breakRules: legacyWorkSettings.breakRules,
        });
        setIsWorkDirty(false);
        setPendingTabChange(undefined);
        if (blocker.state === "blocked") {
          blocker.reset();
        }
      } finally {
        setIsSavingWorkProfile(false);
      }
      return;
    }

    setUser(nextUser);
    setIsWorkDirty(false);
    setPendingTabChange(undefined);
  };

  const saveProfile = async () => {
    setStatus(undefined);
    setError(undefined);

    if (!profileForm.displayName.trim()) {
      setError("Name is required.");
      return;
    }

    if (!hasBackendUserId(userFromDb.id)) {
      setError("User profile is not initialized yet.");
      return;
    }

    if (!profileForm.displayName.trim()) {
      setError("Name is required.");
      return;
    }

    setIsSavingProfile(true);

    try {
      const token = await getAccessTokenSilently();
      const savedProfile = await updateUserProfile(
        userFromDb.id,
        {
          displayName: profileForm.displayName.trim(),
          email: profileForm.email.trim(),
          profileImageUrl: profileForm.profileImageUrl.trim() || undefined,
          timezone: profileForm.timezone.trim() || "Europe/Berlin",
        },
        token,
      );

      setUser({
        ...userFromDb,
        id: savedProfile.id,
        username: savedProfile.username,
        displayName: savedProfile.displayName,
        email: savedProfile.email,
        profileImage: savedProfile.profileImageUrl,
        timezone: savedProfile.timezone,
      });
      setStatus("Profile updated.");
    } catch (saveError) {
      setError(
        saveError instanceof Error
          ? saveError.message
          : "Profile could not be saved.",
      );
    } finally {
      setIsSavingProfile(false);
    }
  };

  const saveAppearance = async () => {
    setStatus(undefined);
    setError(undefined);

    if (!hasBackendUserId(userFromDb.id)) {
      setError("User profile is not initialized yet.");
      return;
    }

    if (!profileForm.displayName.trim()) {
      setError("Name is required.");
      return;
    }

    const breakColor = serializeColorPreference(breakColorState);
    const blockerColor = serializeColorPreference(blockerColorState);
    const orgColorPrefs = serializeOrgColorPreferences(orgColors);

    setIsSavingAppearance(true);

    try {
      const token = await getAccessTokenSilently();
      const savedProfile = await updateUserProfile(
        userFromDb.id,
        {
          displayName: profileForm.displayName.trim(),
          email: profileForm.email.trim(),
          profileImageUrl: profileForm.profileImageUrl.trim() || undefined,
          timezone: profileForm.timezone.trim() || "Europe/Berlin",
          breakColor,
          blockerColor,orgColors: orgColorPrefs,
        },
        token,
      );

      setBreakColor(breakColorState);
      setBlockerColor(blockerColorState);
      for (const [orgId, color] of Object.entries(orgColors)) {
        setOrgColor(orgId, color);
      }

      setUser({
        ...userFromDb,
        id: savedProfile.id,
        username: savedProfile.username,
        displayName: savedProfile.displayName,
        email: savedProfile.email,
        profileImage: savedProfile.profileImageUrl,
        timezone: savedProfile.timezone,
        appearanceBreakColor: savedProfile.breakColor,
        appearanceBlockerColor: savedProfile.blockerColor,
        appearanceOrgColors: savedProfile.orgColors,
      });
      setIsAppearanceDirty(false);
      setStatus("Appearance saved.");
    } catch (saveError) {
      setError(
        saveError instanceof Error
          ? saveError.message
          : "Appearance could not be saved.",
      );
    } finally {
      setIsSavingAppearance(false);
    }
  };

  const deleteWorkProfile = async () => {
    const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "";

    setError(undefined);
    setStatus(undefined);
    setIsDeletingWorkProfile(true);

    try {
      const deletePath = userFromDb.id
        ? `${apiBaseUrl}/api/WorkProfile/${userFromDb.id}`
        : `${apiBaseUrl}/api/WorkProfile/by-email?email=${encodeURIComponent(userFromDb.email)}`;

      const token = await getAccessTokenSilently();
      const response = await fetch(deletePath, {
        method: "DELETE",
        headers: { Authorization: `Bearer ${token}` },
      });

      if (!response.ok) {
        const message = await response.text();
        throw new Error(message || "Work profile could not be deleted.");
      }

      void persist({
        ...userFromDb,
        workProfile: undefined,
        hasPersistedWorkProfile: false,
        plannerViewStart: defaultUser.plannerViewStart,
        plannerViewEnd: defaultUser.plannerViewEnd,
        workCapacityHours: defaultWorkProfile.capacity,
        workDays: [],
        workStart: undefined,
        workEnd: undefined,
        breakRules: defaultWorkProfile.breakRules,
      });
      setShowDeleteWorkProfileDialog(false);
      setIsWorkDirty(false);
      setStatus("Work profile deleted. Planning needs to be generated again.");
    } catch (deleteError) {
      if (deleteError instanceof TypeError) {
        setError("Backend not reachable. Start the API and try again.");
      } else {
        setError(
          deleteError instanceof Error
            ? deleteError.message
            : "Work profile could not be deleted.",
        );
      }
    } finally {
      setIsDeletingWorkProfile(false);
    }
  };

  const logOut = () => {
    logout();
    authLogout();
  };

  const deleteAccount = () => {
    const confirmed = window.confirm(
      "Account löschen? Dies loggt dich aus. (Demo)",
    );
    if (!confirmed) return;
    logout();
    navigate("/login");
  };

  const blocker = useBlocker(
    ({ currentLocation, nextLocation }) =>
      !isSavingWorkProfile &&
      isWorkDirty &&
      tab === "work" &&
      currentLocation.pathname !== nextLocation.pathname,
  );

  const handleTabClick = (next: Tab) => {
    if (tab === "work" && isSavingWorkProfile && next !== "work") {
      return;
    }

    if (tab === "work" && isWorkDirty && next !== "work") {
      setPendingTabChange(next);
    } else {
      setTab(next);
    }
  };

  const confirmLeave = () => {
    if (blocker.state === "blocked") {
      blocker.proceed();
    } else if (pendingTabChange) {
      setTab(pendingTabChange);
      setPendingTabChange(undefined);
    }
    setIsWorkDirty(false);
  };

  const cancelLeave = () => {
    if (blocker.state === "blocked") {
      blocker.reset();
    }
    setPendingTabChange(undefined);
  };

  const showUnsavedDialog =
    !isSavingWorkProfile &&
    isWorkDirty &&
    (blocker.state === "blocked" || pendingTabChange !== undefined);

  useEffect(() => {
    if (isWorkDirty) {
      return;
    }

    if (blocker.state === "blocked") {
      blocker.reset();
    }

    if (pendingTabChange !== undefined) {
      setPendingTabChange(undefined);
    }
  }, [blocker, isWorkDirty, pendingTabChange]);

  const onPwChange = async (newPassword: ChangePassword) => {
    if (!userFromAuth?.email) {
      alert("there was an issue with changing the password");
      return;
    }
    try {
      const token = await getAccessTokenSilently();
      const result = await changePassword(
        userFromAuth?.email,
        newPassword.newPassword,
        token,
      );
      alert(
        result
          ? "password was successfully changed"
          : "there was an issue with changing the password",
      );
    } catch (error) {
      alert(`there was an issue with changing the password: ${error}`);
    }
  };

  return (
    <div className="grid min-h-full w-full min-w-0 grid-rows-[3.5rem_auto] gap-6 p-6 text-slate-50">
      <div className="flex flex-col gap-4 xl:flex-row xl:items-center xl:justify-between">
        <div className="flex flex-col gap-1">
          <span className="text-xs tracking-[0.28em] text-emerald-300 uppercase">
            Profile
          </span>
          <h1 className="text-4xl leading-tight font-semibold">My Profile</h1>
          <span className="text-sm text-slate-400">
            Manage account, work profile, security and appearance
          </span>
        </div>
        <div className="min-w-0 overflow-x-auto pb-1">
          <div className="flex min-w-max flex-nowrap gap-2 pr-1 text-sm xl:min-w-0 xl:flex-wrap">
            {(
              ["general", "work", "security", "account", "appearance"] as Tab[]
            ).map((t) => (
              <button
                key={t}
                onClick={() => handleTabClick(t)}
                className={`shrink-0 rounded-full px-4 py-2 font-semibold transition ${
                  tab === t
                    ? "border border-emerald-300/70 bg-emerald-400/15 text-emerald-100 shadow-[0_0_0_1px_rgba(52,211,153,0.18),0_12px_28px_rgba(16,185,129,0.12)]"
                    : "border border-slate-800 bg-slate-900/60 text-slate-300 hover:border-emerald-300/40 hover:bg-emerald-400/5 hover:text-emerald-100"
                }`}
              >
                {t === "general" && "General"}
                {t === "work" && "Work profile"}
                {t === "security" && "Security"}
                {t === "account" && "Account"}
                {t === "appearance" && "Appearance"}
              </button>
            ))}
          </div>
        </div>
      </div>

      <div className="min-w-0 overflow-hidden rounded-3xl border border-slate-800 bg-slate-900/70 p-6 shadow-2xl">
        {tab === "general" && (
          <div className="grid grid-cols-1 gap-6 lg:grid-cols-[1.1fr_0.9fr]">
            <div className="flex flex-col gap-4">
              <div className="flex items-center gap-4 rounded-2xl border border-slate-800 bg-slate-900/80 p-4">
                <div className="relative">
                  <div className="aspect-square w-24 rounded-full border border-slate-700">
                    {profileForm.profileImageUrl || userFromAuth?.picture ? (
                      <img
                        src={
                          profileForm.profileImageUrl || userFromAuth?.picture
                        }
                        alt="Profile"
                        className="h-full w-full rounded-full object-cover object-center"
                      />
                    ) : (
                      <div
                        style={{
                          background:
                            "linear-gradient(135deg, #34d399, #2563eb)",
                        }}
                      ></div>
                    )}
                  </div>
                </div>
                <div className="flex flex-col gap-2 text-sm">
                  <div className="text-xs tracking-[0.14em] text-slate-500 uppercase">
                    Profile Picture
                  </div>
                  {/* <div className="flex gap-2">
                    {["gradient-1", "gradient-2", "gradient-3"].map((g) => (
                      <button
                        key={g}
                        onClick={() =>
                          setProfileForm({ ...profileForm, profileImage: g })
                        }
                        className={`h-10 w-10 rounded-full border ${
                          profileForm.profileImage === g
                            ? "border-emerald-300"
                            : "border-slate-700"
                        }`}
                        style={{
                          backgroundImage:
                            g === "gradient-1"
                              ? "linear-gradient(135deg, #34d399, #2563eb)"
                              : g === "gradient-2"
                                ? "linear-gradient(135deg, #ec4899, #8b5cf6)"
                                : "linear-gradient(135deg, #f59e0b, #ef4444)",
                        }}
                      />
                    ))}
                  </div> */}
                  <input
                    type="url"
                    placeholder="Bild-URL (optional)"
                    value={profileForm.profileImageUrl}
                    className="rounded-xl border border-slate-800 bg-slate-900/80 px-3 py-2 text-xs text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
                    onChange={(e) =>
                      setProfileForm({
                        ...profileForm,
                        profileImageUrl: e.target.value,
                      })
                    }
                  />
                </div>
              </div>

              <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
                <div className="flex flex-col gap-1">
                  <label className="text-xs tracking-[0.14em] text-slate-500 uppercase">
                    Name
                  </label>
                  <input
                    value={profileForm.displayName}
                    onChange={(e) =>
                      setProfileForm({
                        ...profileForm,
                        displayName: e.target.value,
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
                    onChange={(e) =>
                      setProfileForm({ ...profileForm, email: e.target.value })
                    }
                    className="rounded-xl border border-slate-800 bg-slate-900/80 px-3 py-2 text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
                  />
                </div>
              </div>

              <button
                onClick={saveProfile}
                disabled={isSavingProfile}
                className="w-fit rounded-xl border border-emerald-300/60 bg-emerald-400/15 px-4 py-2 text-sm font-semibold text-emerald-100 shadow-sm transition hover:bg-emerald-400/25"
              >
                {isSavingProfile ? "Saving..." : "Save changes"}
              </button>
            </div>
          </div>
        )}

        {tab === "work" && (
          <div className="flex flex-col gap-4">
            <WorkProfileConfigurator
              key={`${userFromDb.workProfile?.id ?? "new"}-${userFromDb.username}-${userFromDb.email}-${userFromDb.workCapacityHours ?? 8}-${userFromDb.workStart ?? "09:00"}-${userFromDb.workEnd ?? "17:00"}-${userFromDb.breakRules ?? "default"}-${userFromDb.workProfile?.days.length ?? 0}`}
              user={userFromDb}
              onSaveUser={persist}
              onStatusChange={setStatus}
              onErrorChange={setError}
              onDirtyChange={setIsWorkDirty}
            />
            <div className="flex justify-end">
              <button
                onClick={() => setShowDeleteWorkProfileDialog(true)}
                disabled={userFromDb.orgs.length < 1}
                className="cursor-pointer rounded-xl border border-rose-300/60 bg-rose-500/15 px-4 py-2 text-sm font-semibold text-rose-100 shadow-sm transition hover:bg-rose-500/25 disabled:cursor-not-allowed disabled:opacity-70 disabled:hover:bg-rose-500/15"
              >
                Delete work profile
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
              <form
                className="mt-4 flex flex-col gap-3 text-sm text-slate-200"
                onSubmit={handlePwChange(onPwChange)}
              >
                <div className="flex flex-col gap-1">
                  <span className="text-xs tracking-[0.14em] text-slate-500 uppercase">
                    New password
                  </span>
                  <input
                    type="password"
                    {...registerPwChange("newPassword", { required: true })}
                    className="rounded-xl border border-slate-800 bg-slate-900/80 px-3 py-2 text-slate-100 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
                  />
                  {changePwError.newPassword && (
                    <div className="rounded-xl border border-rose-300/60 bg-rose-500/20 px-4 py-2 text-sm font-semibold text-rose-50 transition hover:bg-rose-500/30">
                      {changePwError.newPassword.message}
                    </div>
                  )}
                </div>
                <div className="flex flex-col gap-1">
                  <span className="text-xs tracking-[0.14em] text-slate-500 uppercase">
                    Confirm password
                  </span>
                  <input
                    type="password"
                    {...registerPwChange("confirmPassword", { required: true })}
                    className="rounded-xl border border-slate-800 bg-slate-900/80 px-3 py-2 text-slate-100 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
                  />
                  {changePwError.confirmPassword && (
                    <div className="rounded-xl border border-rose-300/60 bg-rose-500/20 px-4 py-2 text-sm font-semibold text-rose-50 transition hover:bg-rose-500/30">
                      {changePwError.confirmPassword.message}
                    </div>
                  )}
                </div>
                <button
                  type="submit"
                  className="w-fit rounded-xl border border-emerald-300/60 bg-emerald-400/10 px-4 py-2 text-sm font-semibold text-emerald-100 shadow-sm transition hover:bg-emerald-400/20"
                >
                  Passwort ändern
                </button>
                {changePwError.form && (
                  <div className="rounded-xl border border-rose-300/60 bg-rose-500/20 px-4 py-2 text-sm font-semibold text-rose-50 transition hover:bg-rose-500/30">
                    {changePwError.form.message}
                  </div>
                )}
              </form>
            </div>

            <div className="rounded-2xl border border-slate-800 bg-slate-900/80 p-5 text-sm text-slate-200">
              <div className="text-sm font-semibold text-slate-100">
                Sessions & Logout
              </div>
              <div className="mt-3 flex flex-col gap-2">
                <button
                  onClick={logOut}
                  className="w-fit rounded-full border border-slate-700 bg-slate-900/70 px-4 py-2 text-sm text-slate-100 transition hover:border-emerald-300/60 hover:text-emerald-100"
                >
                  Logout
                </button>
              </div>
            </div>
          </div>
        )}

        {tab === "appearance" && (
          <div className="flex flex-col gap-6">
            <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
              <div className="text-xs tracking-[0.2em] text-slate-400 uppercase">
                Calendar colors
              </div>
              <button
                type="button"
                onClick={saveAppearance}
                disabled={isSavingAppearance || !isAppearanceDirty}
                className="w-fit rounded-xl border border-emerald-300/60 bg-emerald-500 px-4 py-2 text-sm font-semibold text-white shadow-sm transition hover:bg-emerald-400 disabled:cursor-not-allowed disabled:border-slate-700 disabled:bg-slate-800 disabled:text-slate-400"
              >
                {isSavingAppearance ? "Saving..." : "Save appearance"}
              </button>
            </div>

            {/* Break color */}
            <ColorPickerCard
              label="Breaks"
              color={breakColorState}
              onChange={updateBreakColorState}
              onReset={() => updateBreakColorState({ ...DEFAULT_BREAK_COLOR })}
            />

            {/* Blocker color */}
            <ColorPickerCard
              label="Recurring Blockers"
              color={blockerColorState}
              onChange={updateBlockerColorState}
              onReset={() => updateBlockerColorState({ ...DEFAULT_BLOCKER_COLOR })}
            />

            {/* Per-org colors */}
            {(userFromDb.orgs ?? []).map((org) => {
              const c = orgColors[org.id] ?? { ...DEFAULT_ORG_COLOR };
              return (
                <ColorPickerCard
                  key={org.id}
                  label={org.name}
                  color={c}
                  onChange={(next) => updateOrgColor(org.id, next)}
                  onReset={() =>
                    updateOrgColor(org.id, { ...DEFAULT_ORG_COLOR })
                  }
                />
              );
            })}

            {(userFromDb.orgs ?? []).length === 0 && (
              <div className="rounded-2xl border border-dashed border-slate-700 bg-slate-900/60 p-4 text-sm text-slate-400">
                No organizations yet. Join an organization to customize its
                colors.
              </div>
            )}
          </div>
        )}

        {tab === "account" && (
          <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
            <div className="rounded-2xl border border-rose-400/40 bg-rose-500/10 p-5 text-sm text-rose-50">
              <div className="text-sm font-semibold">Danger area</div>
              {/* <p className="mt-2 text-rose-100/90">This removes your account and logs you out. Demo: no server operation.</p> */}
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

      {showUnsavedDialog && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/70 backdrop-blur-sm"
          onClick={cancelLeave}
        >
          <div
            className="flex w-full max-w-sm flex-col gap-5 rounded-2xl border border-slate-700 bg-slate-900 p-6 shadow-2xl"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-start gap-3">
              <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full border border-amber-400/30 bg-amber-400/10 text-xl">
                ⚠️
              </div>
              <div>
                <p className="text-sm font-semibold text-slate-50">
                  Unsaved changes
                </p>
                <p className="mt-1 text-xs text-slate-400">
                  Your work profile has unsaved changes. Do you want to leave
                  without saving?
                </p>
              </div>
            </div>
            <div className="flex justify-end gap-2">
              <button
                type="button"
                onClick={cancelLeave}
                className="rounded-xl border border-slate-700 bg-slate-800 px-4 py-2 text-sm font-semibold text-slate-300 transition hover:border-slate-500 hover:text-slate-100"
              >
                Stay & save
              </button>
              <button
                type="button"
                onClick={confirmLeave}
                className="rounded-xl border border-rose-400/40 bg-rose-500/15 px-4 py-2 text-sm font-semibold text-rose-200 transition hover:bg-rose-500/25"
              >
                Leave without saving
              </button>
            </div>
          </div>
        </div>
      )}

      {showDeleteWorkProfileDialog && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/70 p-6 backdrop-blur-sm">
          <div className="w-full max-w-lg rounded-3xl border border-rose-400/40 bg-slate-900 p-6 shadow-2xl">
            <div className="text-xs tracking-[0.2em] text-rose-300 uppercase">
              Confirm deletion
            </div>
            <h2 className="mt-2 text-2xl font-semibold text-slate-50">
              Delete work profile?
            </h2>
            <p className="mt-3 text-sm text-slate-300">
              This removes your work profile, load capacity, break setup and
              dependent planning data. A new plan will need to be generated
              afterwards.
            </p>
            <div className="mt-6 flex flex-wrap justify-end gap-3">
              <button
                onClick={() => setShowDeleteWorkProfileDialog(false)}
                disabled={isDeletingWorkProfile}
                className="rounded-xl border border-slate-700 bg-slate-900/80 px-4 py-2 text-sm font-semibold text-slate-200 transition hover:border-slate-500"
              >
                Cancel
              </button>
              <button
                onClick={deleteWorkProfile}
                disabled={isDeletingWorkProfile}
                className="rounded-xl border border-rose-300/60 bg-rose-500/20 px-4 py-2 text-sm font-semibold text-rose-50 transition hover:bg-rose-500/30 disabled:cursor-not-allowed disabled:opacity-60"
              >
                {isDeletingWorkProfile ? "Deleting..." : "Delete work profile"}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default User;
