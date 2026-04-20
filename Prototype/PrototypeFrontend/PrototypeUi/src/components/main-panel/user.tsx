import { useEffect, useMemo, useState, type FC } from "react";
import { useBlocker, useNavigate } from "react-router";
import WorkProfileConfigurator from "./work-profile-configurator";
import useLoginStore from "../../stores/login-store";
import useUserStore from "../../stores/user-store";

type Tab = "general" | "work" | "security" | "account";

const User: FC = () => {
  const { logout } = useLoginStore();
  const { user, setUser } = useUserStore();
  const navigate = useNavigate();

  const [tab, setTab] = useState<Tab>("general");
  const [isWorkDirty, setIsWorkDirty] = useState(false);
  const [pendingTabChange, setPendingTabChange] = useState<Tab | undefined>();
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
  const [notifForm, setNotifForm] = useState({
    emailInvites: user?.notifications?.emailInvites ?? true,
    emailDeadlines: user?.notifications?.emailDeadlines ?? true,
  });

  useEffect(() => {
    if (!user) {
      navigate("/login");
    }
  }, [navigate, user]);

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

  if (!user) {
    return <></>;
  }

  const persist = (nextUser = user) => {
    setUser(nextUser);
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

  // Block react-router navigation when work tab has unsaved changes
  const blocker = useBlocker(({ currentLocation, nextLocation }) =>
    isWorkDirty &&
    tab === "work" &&
    currentLocation.pathname !== nextLocation.pathname,
  );

  const handleTabClick = (next: Tab) => {
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
    blocker.state === "blocked" || pendingTabChange !== undefined;

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
              onClick={() => handleTabClick(currentTab)}
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
          <WorkProfileConfigurator
            key={`${user.username}-${user.email}`}
            user={user}
            onSaveUser={persist}
            onStatusChange={setStatus}
            onErrorChange={setError}
            onDirtyChange={setIsWorkDirty}
          />
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

      {/* ── Unsaved work-profile changes dialog ── */}
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
                <p className="text-sm font-semibold text-slate-50">Unsaved changes</p>
                <p className="mt-1 text-xs text-slate-400">
                  Your work profile has unsaved changes. Do you want to leave without saving?
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
    </div>
  );
};

export default User;
