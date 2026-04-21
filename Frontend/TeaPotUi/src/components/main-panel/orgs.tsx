import { useEffect, useMemo, useState, type FC } from "react";
import useUserStore from "../../stores/user-store";
import type { Invitation, Org, User } from "../../util/types";

const tabOptions = ["members", "invites", "invite", "settings"] as const;
type Tab = (typeof tabOptions)[number];

const Orgs: FC = () => {
  const { user, setUser } = useUserStore();

  const [orgs, setOrgs] = useState<Org[]>(user?.orgs ?? []);
  const [invites, setInvites] = useState<Invitation[]>(user?.invites ?? []);
  const [selectedOrgId, setSelectedOrgId] = useState<string | null>(orgs[0]?.id ?? null);
  const [activeTab, setActiveTab] = useState<Tab>("members");
  const [newInviteEmail, setNewInviteEmail] = useState("");
  const [renameValue, setRenameValue] = useState("");
  const [deleteConfirm, setDeleteConfirm] = useState("");

  useEffect(() => {
    if (orgs.length > 0 && !selectedOrgId) {
      setSelectedOrgId(orgs[0].id);
    }
  }, [orgs, selectedOrgId]);

  const persist = (nextUser: User) => {
    setUser(nextUser);
    setOrgs(nextUser.orgs ?? []);
    setInvites(nextUser.invites ?? []);
  };

  const currentRole = (org: Org): "Admin" | "Member" =>
    org.adminEmails?.includes(user.email) ? "Admin" : "Member";

  const acceptInvite = (invite: Invitation) => {
    const remainingInvites = invites.filter((i) => i !== invite);
    const existingOrg = orgs.find((t) => t.id === invite.orgId);
    let nextOrg = orgs;
    if (existingOrg) {
      const alreadyInOrg = existingOrg.users.some((u) => u.email === user.email);
      nextOrg = orgs.map((t) =>
        t.id === existingOrg.id
          ? {
            ...t,
            users: alreadyInOrg ? t.users : [...t.users, user],
            invites: (t.invites ?? []).filter((i) => i.email !== user.email),
          }
          : t,
      );
    } else {
      const newOrg: Org = {
        id: invite.orgId,
        name: invite.orgName,
        users: [user],
        adminEmails: [],
        invites: [],
      };
      nextOrg = [...orgs, newOrg];
    }
    persist({ ...user, orgs: nextOrg, invites: remainingInvites });
  };

  const declineInvite = (invite: Invitation) => {
    const remainingInvites = invites.filter((i) => i !== invite);
    persist({ ...user, invites: remainingInvites });
  };

  const leaveOrg = (orgId: string) => {
    const nextOrgs = orgs.filter((t) => t.id !== orgId);
    persist({ ...user, orgs: nextOrgs });
    if (selectedOrgId === orgId) {
      setSelectedOrgId(nextOrgs[0]?.id ?? null);
    }
  };

  const toggleRole = (org: Org, email: string) => {
    const isAdmin = org.adminEmails?.includes(email) ?? false;
    const updatedOrg: Org = {
      ...org,
      adminEmails: isAdmin
        ? (org.adminEmails ?? []).filter((e) => e !== email)
        : [...(org.adminEmails ?? []), email],
    };
    const nextOrgss = orgs.map((t) => (t.id === org.id ? updatedOrg : t));
    persist({ ...user, orgs: nextOrgss });
  };

  const kickUser = (org: Org, email: string) => {
    const updatedOrg: Org = {
      ...org,
      users: org.users.filter((u) => u.email !== email),
      adminEmails: (org.adminEmails ?? []).filter((e) => e !== email),
    };
    let nextOrgs = orgs.map((t) => (t.id === org.id ? updatedOrg : t));
    if (email === user.email) {
      nextOrgs = nextOrgs.filter((t) => t.id !== org.id);
      setSelectedOrgId(nextOrgs[0]?.id ?? null);
    }
    persist({ ...user, orgs: nextOrgs });
  };

  const sendInvite = async (org: Org) => {
    if (!newInviteEmail.trim()) return;

    try {
      const res = await fetch("http://localhost:5000/api/invitations", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          organizationId: org.id,
          email: newInviteEmail.trim(),
        }),
      });

      if (!res.ok) {
        const err = await res.text();
        alert(err);
        return;
      }

      const data = await res.json();

      console.log("Invitation created:", data);

      setNewInviteEmail("");

      // OPTIONAL: reload invites from backend
    } catch (e) {
      console.error(e);
    }
  };

  const withdrawInvite = (org: Org, email: string) => {
    const updatedOrg: Org = {
      ...org,
      invites: (org.invites ?? []).filter((i) => i.email !== email),
    };
    const nextOrgs = orgs.map((t) => (t.id === org.id ? updatedOrg : t));
    persist({ ...user, orgs: nextOrgs });
  };

  const renameOrg = (org: Org) => {
    if (!renameValue.trim()) return;
    const updatedOrg: Org = { ...org, name: renameValue.trim() };
    const nextOrgs = orgs.map((t) => (t.id === org.id ? updatedOrg : t));
    persist({ ...user, orgs: nextOrgs });
    setRenameValue("");
  };

  const deleteOrg = (org: Org) => {
    if (deleteConfirm !== org.name) return;
    const nextOrgs = orgs.filter((t) => t.id !== org.id);
    const nextInvites = (user.invites ?? []).filter((i) => i.orgId !== org.id);
    persist({ ...user, orgs: nextOrgs, invites: nextInvites });
    setSelectedOrgId(nextOrgs[0]?.id ?? null);
    setDeleteConfirm("");
  };

  const selectedOrg = useMemo(() => orgs.find((t) => t.id === selectedOrgId) ?? null, [orgs, selectedOrgId]);
  const isSelectedAdmin = selectedOrg ? selectedOrg.adminEmails?.includes(user.email) : false;

  return (
    <div className="grid h-full w-full grid-rows-[3.5rem_1fr] gap-6 p-6">
      <div className="flex items-center justify-between">
        <div className="flex flex-col gap-1">
          <span className="text-xs uppercase tracking-[0.28em] text-emerald-300">Orgs</span>
          <h1 className="text-4xl font-semibold leading-tight">My orgs</h1>
          <span className="text-sm text-slate-400">Manage memberships, invites, and settings.</span>
        </div>
      </div>

      <div className="grid grid-cols-[1.1fr_0.9fr] gap-4 max-xl:grid-cols-1">
        <div className="flex flex-col gap-4 rounded-3xl border border-slate-800 bg-slate-900/70 p-5 shadow-xl backdrop-blur">
          <div className="text-lg font-semibold text-slate-50">Meine Orgs </div>
          {orgs.length === 0 && (
            <div className="rounded-2xl border border-dashed border-slate-800 bg-slate-900/60 p-4 text-slate-400">
              Du bist noch in keinem Org.
            </div>
          )}
          <div className="grid grid-cols-1 gap-3 md:grid-cols-2">
            {orgs.map((org) => (
              <div
                key={org.id}
                className={`rounded-2xl border ${selectedOrgId === org.id ? "border-emerald-300/70" : "border-slate-800"} bg-slate-900/80 p-4 shadow`}
              >
                <div className="flex items-start justify-between gap-2">
                  <div>
                    <div className="text-sm uppercase tracking-[0.16em] text-slate-400">Org</div>
                    <div className="text-lg font-semibold text-slate-50">{org.name}</div>
                  </div>
                  <div className="flex flex-col items-end gap-1 text-right">
                    <span className="rounded-full bg-slate-800 px-3 py-1 text-[11px] uppercase tracking-wide text-slate-200">
                      {currentRole(org)}
                    </span>
                    <span className="text-xs text-slate-400">{org.users.length} Mitglieder</span>
                  </div>
                </div>
                <div className="mt-3 flex gap-2">
                  <button
                    onClick={() => setSelectedOrgId(org.id)}
                    className={`flex-1 rounded-xl border px-3 py-2 text-sm font-semibold transition ${currentRole(org) === "Admin"
                      ? "border-emerald-300/60 bg-emerald-400/10 text-emerald-100 hover:bg-emerald-400/20"
                      : "border-slate-800 bg-slate-900/60 text-slate-300 hover:border-slate-700"
                      }`}
                  >
                    {currentRole(org) === "Admin" ? "Verwalten" : "Ansehen"}
                  </button>
                  <button
                    onClick={() => leaveOrg(org.id)}
                    className="rounded-xl border border-slate-800 bg-slate-900/60 px-3 py-2 text-sm text-slate-300 transition hover:border-rose-400/60 hover:text-rose-200"
                  >
                    Austreten
                  </button>
                </div>
              </div>
            ))}
          </div>

          <div className="mt-4 rounded-2xl border border-slate-800 bg-slate-900/80 p-4">
            <div className="text-sm font-semibold text-slate-100">Pending invitations</div>
            {invites.filter((i) => i.status === "pending").length === 0 && (
              <div className="mt-2 text-sm text-slate-500">No pending invitations.</div>
            )}
            <div className="mt-3 flex flex-col gap-3">
              {invites
                .filter((i) => i.status === "pending")
                .map((invite) => (
                  <div
                    key={`${invite.orgId}-${invite.email}`}
                    className="flex items-center justify-between rounded-xl border border-slate-800 bg-slate-900/70 px-3 py-2 text-sm text-slate-200"
                  >
                    <div>
                      <div className="font-semibold text-slate-50">{invite.orgName}</div>
                      <div className="text-xs text-slate-400">Invited as member</div>
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
          {!selectedOrg && (
            <div className="text-sm text-slate-400">Chose your organization to manage.</div>
          )}
          {selectedOrg && (
            <>
              <div className="flex items-center justify-between">
                <div>
                  <div className="text-xs uppercase tracking-[0.18em] text-emerald-300">Org verwalten</div>
                  <div className="text-2xl font-semibold text-slate-50">{selectedOrg.name}</div>
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
                    className={`rounded-full px-4 py-2 font-semibold transition ${activeTab === tab
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
                  {selectedOrg.users.map((member) => (
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
                          {selectedOrg.adminEmails?.includes(member.email) ? "Admin" : "Mitglied"}
                        </span>
                        {isSelectedAdmin && member.email !== user.email && (
                          <>
                            <button
                              onClick={() => toggleRole(selectedOrg, member.email)}
                              className="rounded-full border border-emerald-300/60 bg-emerald-400/10 px-3 py-1 text-[11px] font-semibold text-emerald-100 hover:bg-emerald-400/20"
                            >
                              Rolle ändern
                            </button>
                            <button
                              onClick={() => kickUser(selectedOrg, member.email)}
                              className="rounded-full border border-rose-300/60 bg-rose-500/10 px-3 py-1 text-[11px] font-semibold text-rose-100 hover:bg-rose-500/20"
                            >
                              Kick
                            </button>
                          </>
                        )}
                      </div>
                    </div>
                  ))}
                  {selectedOrg.users.length === 0 && (
                    <div className="text-sm text-slate-500">Keine Mitglieder im Org.</div>
                  )}
                </div>
              )}

              {activeTab === "invites" && (
                <div className="mt-4 flex flex-col gap-3">
                  {(selectedOrg.invites ?? []).length === 0 && (
                    <div className="text-sm text-slate-500">Keine offenen Einladungen.</div>
                  )}
                  {(selectedOrg.invites ?? []).map((inv) => (
                    <div
                      key={`${inv.email}-${inv.orgId}`}
                      className="flex items-center justify-between rounded-xl border border-slate-800 bg-slate-900/70 px-4 py-3 text-sm text-slate-200"
                    >
                      <div>
                        <div className="font-semibold text-slate-50">{inv.email}</div>
                        <div className="text-xs text-slate-400">Status: {inv.status}</div>
                      </div>
                      {isSelectedAdmin && inv.status === "pending" && (
                        <button
                          onClick={() => withdrawInvite(selectedOrg, inv.email)}
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
                      onClick={() => sendInvite(selectedOrg)}
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
                    <div className="text-sm font-semibold text-slate-100">Org umbenennen</div>
                    <div className="mt-2 flex gap-2 max-sm:flex-col">
                      <input
                        value={renameValue}
                        onChange={(e) => setRenameValue(e.target.value)}
                        placeholder={selectedOrg.name}
                        className="flex-1 rounded-xl border border-slate-800 bg-slate-900 px-3 py-2 text-sm text-slate-50 outline-none ring-emerald-400/40 focus:border-emerald-400/60 focus:ring"
                      />
                      <button
                        onClick={() => renameOrg(selectedOrg)}
                        disabled={!isSelectedAdmin}
                        className="rounded-xl border border-emerald-300/60 bg-emerald-400/15 px-4 py-2 text-sm font-semibold text-emerald-100 transition hover:bg-emerald-400/25 disabled:cursor-not-allowed disabled:border-slate-800 disabled:bg-slate-900/60 disabled:text-slate-500"
                      >
                        Speichern
                      </button>
                    </div>
                    {!isSelectedAdmin && <div className="text-xs text-slate-500">Nur Admins dürfen umbenennen.</div>}
                  </div>

                  <div className="rounded-2xl border border-rose-400/40 bg-rose-500/10 p-4">
                    <div className="text-sm font-semibold text-rose-50">Org auflösen</div>
                    <div className="mt-1 text-xs text-rose-100/80">Gib den Orgnamen ein, um zu bestätigen.</div>
                    <div className="mt-2 flex gap-2 max-sm:flex-col">
                      <input
                        value={deleteConfirm}
                        onChange={(e) => setDeleteConfirm(e.target.value)}
                        placeholder={selectedOrg.name}
                        className="flex-1 rounded-xl border border-rose-400/50 bg-rose-500/10 px-3 py-2 text-sm text-rose-50 outline-none ring-rose-400/40 focus:border-rose-300/80 focus:ring"
                      />
                      <button
                        onClick={() => deleteOrg(selectedOrg)}
                        disabled={!isSelectedAdmin || deleteConfirm !== selectedOrg.name}
                        className="rounded-xl border border-rose-300/60 bg-rose-500/20 px-4 py-2 text-sm font-semibold text-rose-50 transition hover:bg-rose-500/30 disabled:cursor-not-allowed disabled:border-slate-800 disabled:bg-slate-900/60 disabled:text-slate-500"
                      >
                        Org löschen
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

export default Orgs;
