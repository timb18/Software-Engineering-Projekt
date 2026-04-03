import { useEffect, useMemo, useState, type FC } from "react";
import useUserStore from "../../stores/user-store";
import type { Invitation, Team, User } from "../../util/types";

const tabOptions = ["members", "invites", "invite", "settings"] as const;
type Tab = (typeof tabOptions)[number];

const Teams: FC = () => {
  const { user, setUser } = useUserStore();

  const [teams, setTeams] = useState<Team[]>(user?.teams ?? []);
  const [invites, setInvites] = useState<Invitation[]>(user?.invites ?? []);
  const [selectedTeamId, setSelectedTeamId] = useState<string | null>(teams[0]?.id ?? null);
  const [activeTab, setActiveTab] = useState<Tab>("members");
  const [newInviteEmail, setNewInviteEmail] = useState("");
  const [renameValue, setRenameValue] = useState("");
  const [deleteConfirm, setDeleteConfirm] = useState("");

  useEffect(() => {
    if (teams.length > 0 && !selectedTeamId) {
      setSelectedTeamId(teams[0].id);
    }
  }, [teams, selectedTeamId]);

  if (!user) {
    return <></>;
  }

  const persist = (nextUser: User) => {
    setUser(nextUser);
    setTeams(nextUser.teams ?? []);
    setInvites(nextUser.invites ?? []);
  };

  const currentRole = (team: Team): "Admin" | "Member" =>
    team.adminEmails?.includes(user.email) ? "Admin" : "Member";

  const acceptInvite = (invite: Invitation) => {
    const remainingInvites = invites.filter((i) => i !== invite);
    const existingTeam = teams.find((t) => t.id === invite.teamId);
    let nextTeams = teams;
    if (existingTeam) {
      const alreadyInTeam = existingTeam.users.some((u) => u.email === user.email);
      nextTeams = teams.map((t) =>
        t.id === existingTeam.id
          ? {
              ...t,
              users: alreadyInTeam ? t.users : [...t.users, user],
              invites: (t.invites ?? []).filter((i) => i.email !== user.email),
            }
          : t,
      );
    } else {
      const newTeam: Team = {
        id: invite.teamId,
        name: invite.teamName,
        users: [user],
        adminEmails: [],
        invites: [],
      };
      nextTeams = [...teams, newTeam];
    }
    persist({ ...user, teams: nextTeams, invites: remainingInvites });
  };

  const declineInvite = (invite: Invitation) => {
    const remainingInvites = invites.filter((i) => i !== invite);
    persist({ ...user, invites: remainingInvites });
  };

  const leaveTeam = (teamId: string) => {
    const nextTeams = teams.filter((t) => t.id !== teamId);
    persist({ ...user, teams: nextTeams });
    if (selectedTeamId === teamId) {
      setSelectedTeamId(nextTeams[0]?.id ?? null);
    }
  };

  const toggleRole = (team: Team, email: string) => {
    const isAdmin = team.adminEmails?.includes(email) ?? false;
    const updatedTeam: Team = {
      ...team,
      adminEmails: isAdmin
        ? (team.adminEmails ?? []).filter((e) => e !== email)
        : [...(team.adminEmails ?? []), email],
    };
    const nextTeams = teams.map((t) => (t.id === team.id ? updatedTeam : t));
    persist({ ...user, teams: nextTeams });
  };

  const kickUser = (team: Team, email: string) => {
    const updatedTeam: Team = {
      ...team,
      users: team.users.filter((u) => u.email !== email),
      adminEmails: (team.adminEmails ?? []).filter((e) => e !== email),
    };
    let nextTeams = teams.map((t) => (t.id === team.id ? updatedTeam : t));
    if (email === user.email) {
      nextTeams = nextTeams.filter((t) => t.id !== team.id);
      setSelectedTeamId(nextTeams[0]?.id ?? null);
    }
    persist({ ...user, teams: nextTeams });
  };

  const sendInvite = (team: Team) => {
    if (!newInviteEmail.trim()) return;
    const invite: Invitation = {
      teamId: team.id,
      teamName: team.name,
      email: newInviteEmail.trim(),
      status: "pending",
    };
    const updatedTeam: Team = {
      ...team,
      invites: [...(team.invites ?? []), invite],
    };
    const nextTeams = teams.map((t) => (t.id === team.id ? updatedTeam : t));
    setNewInviteEmail("");
    persist({ ...user, teams: nextTeams });
  };

  const withdrawInvite = (team: Team, email: string) => {
    const updatedTeam: Team = {
      ...team,
      invites: (team.invites ?? []).filter((i) => i.email !== email),
    };
    const nextTeams = teams.map((t) => (t.id === team.id ? updatedTeam : t));
    persist({ ...user, teams: nextTeams });
  };

  const renameTeam = (team: Team) => {
    if (!renameValue.trim()) return;
    const updatedTeam: Team = { ...team, name: renameValue.trim() };
    const nextTeams = teams.map((t) => (t.id === team.id ? updatedTeam : t));
    persist({ ...user, teams: nextTeams });
    setRenameValue("");
  };

  const deleteTeam = (team: Team) => {
    if (deleteConfirm !== team.name) return;
    const nextTeams = teams.filter((t) => t.id !== team.id);
    const nextInvites = (user.invites ?? []).filter((i) => i.teamId !== team.id);
    persist({ ...user, teams: nextTeams, invites: nextInvites });
    setSelectedTeamId(nextTeams[0]?.id ?? null);
    setDeleteConfirm("");
  };

  const selectedTeam = useMemo(() => teams.find((t) => t.id === selectedTeamId) ?? null, [teams, selectedTeamId]);
  const isSelectedAdmin = selectedTeam ? selectedTeam.adminEmails?.includes(user.email) : false;

  return (
    <div className="grid h-full w-full grid-rows-[3.5rem_1fr] gap-6 p-6">
      <div className="flex items-center justify-between">
        <div className="flex flex-col gap-1">
          <span className="text-xs uppercase tracking-[0.28em] text-emerald-300">Teams</span>
          <h1 className="text-4xl font-semibold leading-tight">My teams</h1>
          <span className="text-sm text-slate-400">Manage memberships, invites, and settings.</span>
        </div>
      </div>

      <div className="grid grid-cols-[1.1fr_0.9fr] gap-4 max-xl:grid-cols-1">
        <div className="flex flex-col gap-4 rounded-3xl border border-slate-800 bg-slate-900/70 p-5 shadow-xl backdrop-blur">
          <div className="text-lg font-semibold text-slate-50">Meine Teams</div>
          {teams.length === 0 && (
            <div className="rounded-2xl border border-dashed border-slate-800 bg-slate-900/60 p-4 text-slate-400">
              Du bist noch in keinem Team.
            </div>
          )}
          <div className="grid grid-cols-1 gap-3 md:grid-cols-2">
            {teams.map((team) => (
              <div
                key={team.id}
                className={`rounded-2xl border ${selectedTeamId === team.id ? "border-emerald-300/70" : "border-slate-800"} bg-slate-900/80 p-4 shadow`}
              >
                <div className="flex items-start justify-between gap-2">
                  <div>
                    <div className="text-sm uppercase tracking-[0.16em] text-slate-400">Team</div>
                    <div className="text-lg font-semibold text-slate-50">{team.name}</div>
                  </div>
                  <div className="flex flex-col items-end gap-1 text-right">
                    <span className="rounded-full bg-slate-800 px-3 py-1 text-[11px] uppercase tracking-wide text-slate-200">
                      {currentRole(team)}
                    </span>
                    <span className="text-xs text-slate-400">{team.users.length} Mitglieder</span>
                  </div>
                </div>
                <div className="mt-3 flex gap-2">
                  <button
                    onClick={() => setSelectedTeamId(team.id)}
                    className={`flex-1 rounded-xl border px-3 py-2 text-sm font-semibold transition ${
                      currentRole(team) === "Admin"
                        ? "border-emerald-300/60 bg-emerald-400/10 text-emerald-100 hover:bg-emerald-400/20"
                        : "border-slate-800 bg-slate-900/60 text-slate-300 hover:border-slate-700"
                    }`}
                  >
                    {currentRole(team) === "Admin" ? "Verwalten" : "Ansehen"}
                  </button>
                  <button
                    onClick={() => leaveTeam(team.id)}
                    className="rounded-xl border border-slate-800 bg-slate-900/60 px-3 py-2 text-sm text-slate-300 transition hover:border-rose-400/60 hover:text-rose-200"
                  >
                    Austreten
                  </button>
                </div>
              </div>
            ))}
          </div>

          <div className="mt-4 rounded-2xl border border-slate-800 bg-slate-900/80 p-4">
            <div className="text-sm font-semibold text-slate-100">Offene Einladungen</div>
            {invites.filter((i) => i.status === "pending").length === 0 && (
              <div className="mt-2 text-sm text-slate-500">Keine offenen Einladungen.</div>
            )}
            <div className="mt-3 flex flex-col gap-3">
              {invites
                .filter((i) => i.status === "pending")
                .map((invite) => (
                  <div
                    key={`${invite.teamId}-${invite.email}`}
                    className="flex items-center justify-between rounded-xl border border-slate-800 bg-slate-900/70 px-3 py-2 text-sm text-slate-200"
                  >
                    <div>
                      <div className="font-semibold text-slate-50">{invite.teamName}</div>
                      <div className="text-xs text-slate-400">Eingeladen als Mitglied</div>
                    </div>
                    <div className="flex gap-2">
                      <button
                        onClick={() => acceptInvite(invite)}
                        className="rounded-full border border-emerald-300/60 bg-emerald-400/15 px-3 py-1 text-xs font-semibold text-emerald-100 hover:bg-emerald-400/25"
                      >
                        Annehmen
                      </button>
                      <button
                        onClick={() => declineInvite(invite)}
                        className="rounded-full border border-slate-700 bg-slate-900/60 px-3 py-1 text-xs font-semibold text-slate-300 hover:border-rose-300/60 hover:text-rose-200"
                      >
                        Ablehnen
                      </button>
                    </div>
                  </div>
                ))}
            </div>
          </div>
        </div>

        <div className="flex h-full min-h-[62vh] flex-col gap-4 rounded-3xl border border-slate-800 bg-slate-900/80 p-5 shadow-xl backdrop-blur">
          {!selectedTeam && (
            <div className="text-sm text-slate-400">Wähle ein Team zum Verwalten.</div>
          )}
          {selectedTeam && (
            <>
              <div className="flex items-center justify-between">
                <div>
                  <div className="text-xs uppercase tracking-[0.18em] text-emerald-300">Team verwalten</div>
                  <div className="text-2xl font-semibold text-slate-50">{selectedTeam.name}</div>
                </div>
                {!isSelectedAdmin && (
                  <span className="rounded-full bg-slate-800 px-3 py-1 text-[11px] uppercase tracking-wide text-slate-300">
                    Nur Admins können bearbeiten
                  </span>
                )}
              </div>

              <div className="mt-3 flex flex-wrap gap-2 text-sm">
                {tabOptions.map((tab) => (
                  <button
                    key={tab}
                    onClick={() => setActiveTab(tab)}
                    className={`rounded-full px-4 py-2 font-semibold transition ${
                      activeTab === tab
                        ? "border border-emerald-300/60 bg-emerald-400/15 text-emerald-100"
                        : "border border-slate-800 bg-slate-900/60 text-slate-300 hover:border-emerald-300/40 hover:text-emerald-100"
                    }`}
                  >
                    {tab === "members" && "Aktive Mitglieder"}
                    {tab === "invites" && "Eingeladen"}
                    {tab === "invite" && "Einladen"}
                    {tab === "settings" && "Einstellungen"}
                  </button>
                ))}
              </div>

              {activeTab === "members" && (
                <div className="mt-4 flex flex-col gap-3">
                  {selectedTeam.users.map((member) => (
                    <div
                      key={member.email}
                      className="flex items-center justify-between rounded-xl border border-slate-800 bg-slate-900/70 px-4 py-3 text-sm text-slate-200"
                    >
                      <div>
                        <div className="font-semibold text-slate-50">{member.username}</div>
                        <div className="text-xs text-slate-400">{member.email}</div>
                      </div>
                      <div className="flex items-center gap-2">
                        <span className="rounded-full bg-slate-800 px-2 py-1 text-[11px] uppercase tracking-wide text-slate-300">
                          {selectedTeam.adminEmails?.includes(member.email) ? "Admin" : "Mitglied"}
                        </span>
                        {isSelectedAdmin && member.email !== user.email && (
                          <>
                            <button
                              onClick={() => toggleRole(selectedTeam, member.email)}
                              className="rounded-full border border-emerald-300/60 bg-emerald-400/10 px-3 py-1 text-[11px] font-semibold text-emerald-100 hover:bg-emerald-400/20"
                            >
                              Rolle ändern
                            </button>
                            <button
                              onClick={() => kickUser(selectedTeam, member.email)}
                              className="rounded-full border border-rose-300/60 bg-rose-500/10 px-3 py-1 text-[11px] font-semibold text-rose-100 hover:bg-rose-500/20"
                            >
                              Kick
                            </button>
                          </>
                        )}
                      </div>
                    </div>
                  ))}
                  {selectedTeam.users.length === 0 && (
                    <div className="text-sm text-slate-500">Keine Mitglieder im Team.</div>
                  )}
                </div>
              )}

              {activeTab === "invites" && (
                <div className="mt-4 flex flex-col gap-3">
                  {(selectedTeam.invites ?? []).length === 0 && (
                    <div className="text-sm text-slate-500">Keine offenen Einladungen.</div>
                  )}
                  {(selectedTeam.invites ?? []).map((inv) => (
                    <div
                      key={`${inv.email}-${inv.teamId}`}
                      className="flex items-center justify-between rounded-xl border border-slate-800 bg-slate-900/70 px-4 py-3 text-sm text-slate-200"
                    >
                      <div>
                        <div className="font-semibold text-slate-50">{inv.email}</div>
                        <div className="text-xs text-slate-400">Status: {inv.status}</div>
                      </div>
                      {isSelectedAdmin && inv.status === "pending" && (
                        <button
                          onClick={() => withdrawInvite(selectedTeam, inv.email)}
                          className="rounded-full border border-rose-300/60 bg-rose-500/10 px-3 py-1 text-[11px] font-semibold text-rose-100 hover:bg-rose-500/20"
                        >
                          Zurückziehen
                        </button>
                      )}
                    </div>
                  ))}
                </div>
              )}

              {activeTab === "invite" && (
                <div className="mt-4 flex flex-col gap-3 rounded-2xl border border-slate-800 bg-slate-900/70 p-4">
                  <div className="text-sm font-semibold text-slate-100">Nutzer per E-Mail einladen</div>
                  <div className="flex gap-2 max-sm:flex-col">
                    <input
                      value={newInviteEmail}
                      onChange={(e) => setNewInviteEmail(e.target.value)}
                      placeholder="email@example.com"
                      className="flex-1 rounded-xl border border-slate-800 bg-slate-900 px-3 py-2 text-sm text-slate-50 outline-none ring-emerald-400/40 focus:border-emerald-400/60 focus:ring"
                    />
                    <button
                      onClick={() => sendInvite(selectedTeam)}
                      disabled={!isSelectedAdmin}
                      className="rounded-xl border border-emerald-300/60 bg-emerald-400/15 px-4 py-2 text-sm font-semibold text-emerald-100 transition hover:bg-emerald-400/25 disabled:cursor-not-allowed disabled:border-slate-800 disabled:bg-slate-900/60 disabled:text-slate-500"
                    >
                      Senden
                    </button>
                  </div>
                  {!isSelectedAdmin && (
                    <div className="text-xs text-slate-500">Nur Admins dürfen einladen.</div>
                  )}
                </div>
              )}

              {activeTab === "settings" && (
                <div className="mt-4 flex flex-col gap-4">
                  <div className="rounded-2xl border border-slate-800 bg-slate-900/70 p-4">
                    <div className="text-sm font-semibold text-slate-100">Team umbenennen</div>
                    <div className="mt-2 flex gap-2 max-sm:flex-col">
                      <input
                        value={renameValue}
                        onChange={(e) => setRenameValue(e.target.value)}
                        placeholder={selectedTeam.name}
                        className="flex-1 rounded-xl border border-slate-800 bg-slate-900 px-3 py-2 text-sm text-slate-50 outline-none ring-emerald-400/40 focus:border-emerald-400/60 focus:ring"
                      />
                      <button
                        onClick={() => renameTeam(selectedTeam)}
                        disabled={!isSelectedAdmin}
                        className="rounded-xl border border-emerald-300/60 bg-emerald-400/15 px-4 py-2 text-sm font-semibold text-emerald-100 transition hover:bg-emerald-400/25 disabled:cursor-not-allowed disabled:border-slate-800 disabled:bg-slate-900/60 disabled:text-slate-500"
                      >
                        Speichern
                      </button>
                    </div>
                    {!isSelectedAdmin && <div className="text-xs text-slate-500">Nur Admins dürfen umbenennen.</div>}
                  </div>

                  <div className="rounded-2xl border border-rose-400/40 bg-rose-500/10 p-4">
                    <div className="text-sm font-semibold text-rose-50">Team auflösen</div>
                    <div className="mt-1 text-xs text-rose-100/80">Gib den Teamnamen ein, um zu bestätigen.</div>
                    <div className="mt-2 flex gap-2 max-sm:flex-col">
                      <input
                        value={deleteConfirm}
                        onChange={(e) => setDeleteConfirm(e.target.value)}
                        placeholder={selectedTeam.name}
                        className="flex-1 rounded-xl border border-rose-400/50 bg-rose-500/10 px-3 py-2 text-sm text-rose-50 outline-none ring-rose-400/40 focus:border-rose-300/80 focus:ring"
                      />
                      <button
                        onClick={() => deleteTeam(selectedTeam)}
                        disabled={!isSelectedAdmin || deleteConfirm !== selectedTeam.name}
                        className="rounded-xl border border-rose-300/60 bg-rose-500/20 px-4 py-2 text-sm font-semibold text-rose-50 transition hover:bg-rose-500/30 disabled:cursor-not-allowed disabled:border-slate-800 disabled:bg-slate-900/60 disabled:text-slate-500"
                      >
                        Team löschen
                      </button>
                    </div>
                    {!isSelectedAdmin && <div className="text-xs text-rose-100/80">Nur Admins dürfen löschen.</div>}
                  </div>
                </div>
              )}
            </>
          )}
        </div>
      </div>
    </div>
  );
};

export default Teams;
