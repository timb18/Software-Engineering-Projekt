import { useEffect, useMemo, useState, type FC } from "react";
import { useNavigate } from "react-router";
import useLoginStore from "../../stores/login-store";
import useUserStore from "../../stores/user-store";

type Tab = "general" | "work" | "security" | "account";

const User: FC = () => {
  const { logout } = useLoginStore();
  const { user, setUser } = useUserStore();
  const navigate = useNavigate();

  const [tab, setTab] = useState<Tab>("general");
  const [status, setStatus] = useState<string | undefined>();
  const [error, setError] = useState<string | undefined>();
  const [pwdForm, setPwdForm] = useState({ current: "", next: "", confirm: "" });
  const [profileForm, setProfileForm] = useState({
    displayName: user?.displayName ?? user?.username ?? "",
    email: user?.email ?? "",
    timezone: user?.timezone ?? "Europe/Berlin",
    profileImage: user?.profileImage ?? "gradient-1",
  });
  const [workForm, setWorkForm] = useState({
    capacity: user?.workCapacityHours ?? 8,
    workDays: user?.workDays ?? ["Mon", "Tue", "Wed", "Thu", "Fri"],
    workStart: user?.workStart ?? "09:00",
    workEnd: user?.workEnd ?? "17:00",
    breakRules: user?.breakRules ?? "30m lunch + 10m after 90m focus",
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

  useEffect(() => {
    if (user) {
      setProfileForm({
        displayName: user.displayName ?? user.username,
        email: user.email,
        timezone: user.timezone ?? "Europe/Berlin",
        profileImage: user.profileImage ?? "gradient-1",
      });
      setWorkForm({
        capacity: user.workCapacityHours ?? 8,
        workDays: user.workDays ?? ["Mon", "Tue", "Wed", "Thu", "Fri"],
        workStart: user.workStart ?? "09:00",
        workEnd: user.workEnd ?? "17:00",
        breakRules: user.breakRules ?? "30m lunch",
      });
      setNotifForm({
        emailInvites: user.notifications?.emailInvites ?? true,
        emailDeadlines: user.notifications?.emailDeadlines ?? true,
      });
    }
  }, [user]);

  if (!user) {
    return <></>;
  }

  const avatarStyle = useMemo(() => {
    if (profileForm.profileImage?.startsWith("http")) {
      return { backgroundImage: `url(${profileForm.profileImage})`, backgroundSize: "cover" };
    }
    const gradients: Record<string, string> = {
      "gradient-1": "linear-gradient(135deg, #34d399, #2563eb)",
      "gradient-2": "linear-gradient(135deg, #ec4899, #8b5cf6)",
      "gradient-3": "linear-gradient(135deg, #f59e0b, #ef4444)",
    };
    return { backgroundImage: gradients[profileForm.profileImage ?? "gradient-1"] };
  }, [profileForm.profileImage]);

  const persist = (nextUser = user) => {
    setUser(nextUser);
  };

  const saveProfile = () => {
    setStatus(undefined);
    setError(undefined);
    if (!profileForm.displayName.trim()) {
      setError("Anzeigename darf nicht leer sein.");
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
    setStatus("Profil aktualisiert (Demo: E-Mail-Bestätigung ausgelöst)");
  };

  const saveWork = () => {
    setStatus(undefined);
    setError(undefined);
    if (!workForm.workStart || !workForm.workEnd) {
      setError("Arbeitszeiten benötigen Start und Ende.");
      return;
    }
    const nextUser = {
      ...user,
      workCapacityHours: workForm.capacity,
      workDays: workForm.workDays,
      workStart: workForm.workStart,
      workEnd: workForm.workEnd,
      breakRules: workForm.breakRules,
    };
    persist(nextUser);
    setStatus("Arbeitsprofil gespeichert.");
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
    setStatus("Benachrichtigungen aktualisiert.");
  };

  const logOut = () => {
    logout();
    navigate("/login");
  };

  const logoutAll = () => {
    setStatus("Alle Sessions werden abgemeldet (Demo: localStorage JWT gelöscht)");
    localStorage.removeItem("token");
  };

  const updatePassword = () => {
    setError(undefined);
    setStatus(undefined);
    if (!pwdForm.current || !pwdForm.next || !pwdForm.confirm) {
      setError("Bitte alle Felder ausfüllen.");
      return;
    }
    if (pwdForm.next.length < 8) {
      setError("Passwort muss mindestens 8 Zeichen haben.");
      return;
    }
    if (pwdForm.next !== pwdForm.confirm) {
      setError("Neue Passwörter stimmen nicht überein.");
      return;
    }
    setStatus("Passwort geändert (Demo)");
    setPwdForm({ current: "", next: "", confirm: "" });
  };

  const deleteAccount = () => {
    const confirmed = window.confirm("Account löschen? Dies loggt dich aus. (Demo)");
    if (!confirmed) return;
    logout();
    navigate("/login");
  };

  const toggleWorkDay = (day: string) => {
    const exists = workForm.workDays.includes(day);
    const next = exists ? workForm.workDays.filter((d) => d !== day) : [...workForm.workDays, day];
    setWorkForm({ ...workForm, workDays: next });
  };

  const timezones = ["Europe/Berlin", "UTC", "Europe/Vienna", "Europe/Zurich", "America/New_York"];
  const workDayOptions = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];

  return (
    <div className="grid h-full w-full grid-rows-[3.5rem_1fr] gap-6 p-6 text-slate-50">
      <div className="flex items-center justify-between">
        <div className="flex flex-col gap-1">
          <span className="text-xs uppercase tracking-[0.28em] text-emerald-300">Profile</span>
          <h1 className="text-4xl font-semibold leading-tight">My Profile</h1>
          <span className="text-sm text-slate-400">Konto, Arbeitsprofil und Sicherheit verwalten.</span>
        </div>
        <div className="flex gap-2 text-sm">
          {(["general", "work", "security", "account"] as Tab[]).map((t) => (
            <button
              key={t}
              onClick={() => setTab(t)}
              className={`rounded-full px-4 py-2 font-semibold transition ${
                tab === t
                  ? "border border-emerald-300/60 bg-emerald-400/15 text-emerald-100"
                  : "border border-slate-800 bg-slate-900/60 text-slate-300 hover:border-emerald-300/40 hover:text-emerald-100"
              }`}
            >
              {t === "general" && "Allgemein"}
              {t === "work" && "Arbeitsprofil"}
              {t === "security" && "Sicherheit"}
              {t === "account" && "Konto"}
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
                  <div className="text-xs uppercase tracking-[0.14em] text-slate-500">Profilbild</div>
                  <div className="flex gap-2">
                    {["gradient-1", "gradient-2", "gradient-3"].map((g) => (
                      <button
                        key={g}
                        onClick={() => setProfileForm({ ...profileForm, profileImage: g })}
                        className={`h-10 w-10 rounded-full border ${
                          profileForm.profileImage === g ? "border-emerald-300" : "border-slate-700"
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
                  </div>
                  <input
                    type="url"
                    placeholder="Bild-URL (optional)"
                    className="rounded-xl border border-slate-800 bg-slate-900/80 px-3 py-2 text-xs text-slate-50 outline-none ring-emerald-400/40 focus:border-emerald-400/60 focus:ring"
                    value={profileForm.profileImage.startsWith("http") ? profileForm.profileImage : ""}
                    onChange={(e) => setProfileForm({ ...profileForm, profileImage: e.target.value })}
                  />
                </div>
              </div>

              <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
                <div className="flex flex-col gap-1">
                  <label className="text-xs uppercase tracking-[0.14em] text-slate-500">Anzeigename</label>
                  <input
                    value={profileForm.displayName}
                    onChange={(e) => setProfileForm({ ...profileForm, displayName: e.target.value })}
                    className="rounded-xl border border-slate-800 bg-slate-900/80 px-3 py-2 text-slate-50 outline-none ring-emerald-400/40 focus:border-emerald-400/60 focus:ring"
                  />
                </div>
                <div className="flex flex-col gap-1">
                  <label className="text-xs uppercase tracking-[0.14em] text-slate-500">E-Mail</label>
                  <input
                    type="email"
                    value={profileForm.email}
                    onChange={(e) => setProfileForm({ ...profileForm, email: e.target.value })}
                    className="rounded-xl border border-slate-800 bg-slate-900/80 px-3 py-2 text-slate-50 outline-none ring-emerald-400/40 focus:border-emerald-400/60 focus:ring"
                  />
                  <span className="text-[11px] text-slate-500">Demo: Bestätigungsmail wird angenommen.</span>
                </div>
              </div>

              <div className="flex flex-col gap-1">
                <label className="text-xs uppercase tracking-[0.14em] text-slate-500">Zeitzone</label>
                <select
                  value={profileForm.timezone}
                  onChange={(e) => setProfileForm({ ...profileForm, timezone: e.target.value })}
                  className="rounded-xl border border-slate-800 bg-slate-900/80 px-3 py-2 text-slate-50 outline-none ring-emerald-400/40 focus:border-emerald-400/60 focus:ring"
                >
                  {timezones.map((tz) => (
                    <option key={tz} value={tz}>
                      {tz}
                    </option>
                  ))}
                </select>
                <span className="text-[11px] text-slate-500">Wichtig für Deadlines und Planer.</span>
              </div>

              <button
                onClick={saveProfile}
                className="w-fit rounded-xl border border-emerald-300/60 bg-emerald-400/15 px-4 py-2 text-sm font-semibold text-emerald-100 shadow-sm transition hover:bg-emerald-400/25"
              >
                Profil speichern
              </button>
            </div>

            <div className="flex flex-col gap-4 rounded-2xl border border-slate-800 bg-slate-900/80 p-4">
              <div className="text-sm font-semibold text-slate-100">Konto & Sicherheit (Kurz)</div>
              <div className="text-sm text-slate-300">Rolle: {user.role}</div>
              <div className="text-sm text-slate-300">Username: {user.username}</div>
              <div className="text-xs text-slate-500">Weitere Details in den Tabs Sicherheit/Konto.</div>
            </div>
          </div>
        )}

        {tab === "work" && (
          <div className="grid grid-cols-1 gap-6 lg:grid-cols-[1fr_1fr]">
            <div className="flex flex-col gap-3">
              <div className="text-sm font-semibold text-slate-100">Belastbarkeit</div>
              <div className="flex flex-col gap-1">
                <label className="text-xs uppercase tracking-[0.14em] text-slate-500">Stunden pro Tag</label>
                <input
                  type="number"
                  min={1}
                  max={16}
                  value={workForm.capacity}
                  onChange={(e) => setWorkForm({ ...workForm, capacity: Number(e.target.value) })}
                  className="rounded-xl border border-slate-800 bg-slate-900/80 px-3 py-2 text-slate-50 outline-none ring-emerald-400/40 focus:border-emerald-400/60 focus:ring"
                />
              </div>

              <div className="flex flex-col gap-2">
                <label className="text-xs uppercase tracking-[0.14em] text-slate-500">Arbeitstage</label>
                <div className="flex flex-wrap gap-2">
                  {workDayOptions.map((d) => {
                    const active = workForm.workDays.includes(d);
                    return (
                      <button
                        key={d}
                        onClick={() => toggleWorkDay(d)}
                        className={`rounded-full px-3 py-1 text-sm ${
                          active
                            ? "border border-emerald-300/60 bg-emerald-400/20 text-emerald-100"
                            : "border border-slate-700 bg-slate-900/60 text-slate-300"
                        }`}
                      >
                        {d}
                      </button>
                    );
                  })}
                </div>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div className="flex flex-col gap-1">
                  <label className="text-xs uppercase tracking-[0.14em] text-slate-500">Start</label>
                  <input
                    type="time"
                    value={workForm.workStart}
                    onChange={(e) => setWorkForm({ ...workForm, workStart: e.target.value })}
                    className="rounded-xl border border-slate-800 bg-slate-900/80 px-3 py-2 text-slate-50 outline-none ring-emerald-400/40 focus:border-emerald-400/60 focus:ring"
                  />
                </div>
                <div className="flex flex-col gap-1">
                  <label className="text-xs uppercase tracking-[0.14em] text-slate-500">Ende</label>
                  <input
                    type="time"
                    value={workForm.workEnd}
                    onChange={(e) => setWorkForm({ ...workForm, workEnd: e.target.value })}
                    className="rounded-xl border border-slate-800 bg-slate-900/80 px-3 py-2 text-slate-50 outline-none ring-emerald-400/40 focus:border-emerald-400/60 focus:ring"
                  />
                </div>
              </div>

              <div className="flex flex-col gap-1">
                <label className="text-xs uppercase tracking-[0.14em] text-slate-500">Pausenregeln</label>
                <textarea
                  value={workForm.breakRules}
                  onChange={(e) => setWorkForm({ ...workForm, breakRules: e.target.value })}
                  className="min-h-[90px] rounded-xl border border-slate-800 bg-slate-900/80 px-3 py-2 text-slate-50 outline-none ring-emerald-400/40 focus:border-emerald-400/60 focus:ring"
                  placeholder="z.B. 30m Mittag, 10m nach 90m Fokus"
                />
              </div>

              <button
                onClick={saveWork}
                className="w-fit rounded-xl border border-emerald-300/60 bg-emerald-400/15 px-4 py-2 text-sm font-semibold text-emerald-100 shadow-sm transition hover:bg-emerald-400/25"
              >
                Arbeitsprofil speichern
              </button>
            </div>

            <div className="rounded-2xl border border-slate-800 bg-slate-900/80 p-4 text-sm text-slate-200">
              <div className="text-sm font-semibold text-slate-100">Hinweis</div>
              <p className="mt-2 text-slate-300">
                Diese Werte nutzt der Planer, um Deadlines, Fokusblöcke und Meeting-Zeiten passend zu deiner Zeitzone und Kapazität zu legen.
              </p>
            </div>
          </div>
        )}

        {tab === "security" && (
          <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
            <div className="rounded-2xl border border-slate-800 bg-slate-900/80 p-5">
              <div className="text-sm uppercase tracking-[0.16em] text-slate-400">Passwort ändern</div>
              <div className="mt-4 flex flex-col gap-3 text-sm text-slate-200">
                <div className="flex flex-col gap-1">
                  <span className="text-xs uppercase tracking-[0.14em] text-slate-500">Aktuelles Passwort</span>
                  <input
                    type="password"
                    value={pwdForm.current}
                    onChange={(e) => setPwdForm({ ...pwdForm, current: e.target.value })}
                    className="rounded-xl border border-slate-800 bg-slate-900/80 px-3 py-2 text-slate-100 outline-none ring-emerald-400/40 focus:border-emerald-400/60 focus:ring"
                  />
                </div>
                <div className="flex flex-col gap-1">
                  <span className="text-xs uppercase tracking-[0.14em] text-slate-500">Neues Passwort</span>
                  <input
                    type="password"
                    value={pwdForm.next}
                    onChange={(e) => setPwdForm({ ...pwdForm, next: e.target.value })}
                    className="rounded-xl border border-slate-800 bg-slate-900/80 px-3 py-2 text-slate-100 outline-none ring-emerald-400/40 focus:border-emerald-400/60 focus:ring"
                  />
                </div>
                <div className="flex flex-col gap-1">
                  <span className="text-xs uppercase tracking-[0.14em] text-slate-500">Bestätigen</span>
                  <input
                    type="password"
                    value={pwdForm.confirm}
                    onChange={(e) => setPwdForm({ ...pwdForm, confirm: e.target.value })}
                    className="rounded-xl border border-slate-800 bg-slate-900/80 px-3 py-2 text-slate-100 outline-none ring-emerald-400/40 focus:border-emerald-400/60 focus:ring"
                  />
                </div>
                <button
                  onClick={updatePassword}
                  className="w-fit rounded-xl border border-emerald-300/60 bg-emerald-400/10 px-4 py-2 text-sm font-semibold text-emerald-100 shadow-sm transition hover:bg-emerald-400/20"
                >
                  Passwort ändern
                </button>
                <p className="text-xs text-slate-500">Demo: kein Backend-Call.</p>
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
                  Alle Sessions abmelden
                </button>
                <p className="text-xs text-slate-500">JWT aus localStorage wird entfernt (Demo).</p>
              </div>
            </div>
          </div>
        )}

        {tab === "account" && (
          <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
            <div className="rounded-2xl border border-slate-800 bg-slate-900/80 p-5">
              <div className="text-sm font-semibold text-slate-100">E-Mail-Benachrichtigungen</div>
              <div className="mt-3 flex flex-col gap-3 text-sm text-slate-200">
                <label className="flex items-center justify-between rounded-xl border border-slate-800 bg-slate-900/70 px-3 py-2">
                  <span>Einladungen per E-Mail</span>
                  <input
                    type="checkbox"
                    checked={notifForm.emailInvites}
                    onChange={(e) => setNotifForm({ ...notifForm, emailInvites: e.target.checked })}
                  />
                </label>
                <label className="flex items-center justify-between rounded-xl border border-slate-800 bg-slate-900/70 px-3 py-2">
                  <span>Deadline-Warnungen</span>
                  <input
                    type="checkbox"
                    checked={notifForm.emailDeadlines}
                    onChange={(e) => setNotifForm({ ...notifForm, emailDeadlines: e.target.checked })}
                  />
                </label>
                <button
                  onClick={saveNotifications}
                  className="w-fit rounded-xl border border-emerald-300/60 bg-emerald-400/15 px-4 py-2 text-sm font-semibold text-emerald-100 shadow-sm transition hover:bg-emerald-400/25"
                >
                  Speichern
                </button>
              </div>
            </div>

            <div className="rounded-2xl border border-rose-400/40 bg-rose-500/10 p-5 text-sm text-rose-50">
              <div className="text-sm font-semibold">Konto löschen</div>
              <p className="mt-2 text-rose-100/90">Dies entfernt dein Konto und loggt dich aus. Demo: keine Server-Operation.</p>
              <button
                onClick={deleteAccount}
                className="mt-4 w-fit rounded-xl border border-rose-300/60 bg-rose-500/20 px-4 py-2 text-sm font-semibold text-rose-50 transition hover:bg-rose-500/30"
              >
                Konto löschen
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
