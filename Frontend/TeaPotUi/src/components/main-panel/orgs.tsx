import { useEffect, useMemo, useState, type FC } from "react";
import useUserStore from "../../stores/user-store";
import type { Invitation, Org, User } from "../../util/types";
import acceptInvite from "../../util/accept-invite";
import { fetchOrganizationsByUserEmail } from "../../util/org-api";

const tabOptions = ["members", "invites", "invite", "settings"] as const;
type Tab = (typeof tabOptions)[number];
const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "";
const apiUrl = (path: string) => `${apiBaseUrl}${path}`;
const guidPattern =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;

type InvitationResponse = {
  id: string;
  organizationId: string;
  organizationName?: string;
  email: string;
  firstName?: string;
  lastName?: string;
  status: string;
  invitationLink?: string;
};

const mapInvitationStatus = (status: string): Invitation["status"] =>
  status.toLowerCase() === "open"
    ? "pending"
    : (status.toLowerCase() as Invitation["status"]);

const Orgs: FC = () => {
  const { user, setUser } = useUserStore();

  const [orgs, setOrgs] = useState<Org[]>(user?.orgs ?? []);
  const [invites, setInvites] = useState<Invitation[]>(user?.invites ?? []);
  const [selectedOrgId, setSelectedOrgId] = useState<string | null>(
    orgs[0]?.id ?? null,
  );
  const [activeTab, setActiveTab] = useState<Tab>("members");
  const [newInviteEmail, setNewInviteEmail] = useState("");
  const [inviteError, setInviteError] = useState<string | null>(null);
  const [inviteSuccess, setInviteSuccess] = useState<string | null>(null);
  const [isSendingInvite, setIsSendingInvite] = useState(false);
  const [withdrawingInviteId, setWithdrawingInviteId] = useState<string | null>(
    null,
  );
  const [leaveError, setLeaveError] = useState<string | null>(null);
  const [isLeavingOrgId, setIsLeavingOrgId] = useState<string | null>(null);
  const [renameValue, setRenameValue] = useState("");
  const [deleteConfirm, setDeleteConfirm] = useState("");
  const [deleteError, setDeleteError] = useState<string | null>(null);
  const [deleteSuccess, setDeleteSuccess] = useState<string | null>(null);
  const [isDeletingOrg, setIsDeletingOrg] = useState(false);

  const persist = (nextUser: User) => {
    const nextOrgs = nextUser.orgs ?? [];

    setUser(nextUser);
    setOrgs(nextOrgs);
    setInvites(nextUser.invites ?? []);

    if (nextOrgs.length > 0 && !nextOrgs.some((o) => o.id === selectedOrgId)) {
      setSelectedOrgId(nextOrgs[0].id);
    }
  };

  useEffect(() => {
    const loadOrganizations = async () => {
      if (!user.email || user.email === "example@default.com") {
        return;
      }

      try {
        const nextOrgs = await fetchOrganizationsByUserEmail(user.email);
        const pendingResponse = await fetch(
          apiUrl(
            `/api/Invitation/pending?email=${encodeURIComponent(user.email)}`,
          ),
        );
        const pendingPayload = pendingResponse.ok
          ? ((await pendingResponse.json()) as {
              success: boolean;
              data?: InvitationResponse[];
            })
          : null;
        const pendingInvites: Invitation[] = (pendingPayload?.data ?? []).map(
          (invite) => {
            const matchingOrg = nextOrgs.find(
              (org) => org.id === invite.organizationId,
            );

            return {
              id: invite.id,
              organizationId: invite.organizationId,
              orgId: invite.organizationId,
              orgName:
                invite.organizationName ??
                matchingOrg?.name ??
                "Organization invitation",
              email: invite.email,
              firstName: invite.firstName,
              lastName: invite.lastName,
              status: mapInvitationStatus(invite.status),
              invitationUrl: invite.invitationLink,
            };
          },
        );

        persist({ ...user, orgs: nextOrgs, invites: pendingInvites });
      } catch (error) {
        console.error(error);
      }
    };

    void loadOrganizations();
    // Organizations and pending invites should be loaded once for each account.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [user.email]);

  const currentRole = (org: Org): "Admin" | "Member" =>
    org.adminEmails?.includes(user.email) ? "Admin" : "Member";

  const syncOrganizationInvites = async (org: Org) => {
    const response = await fetch(apiUrl(`/api/Invitation/organization/${org.id}`));

    if (!response.ok) {
      return;
    }

    const payload = (await response.json()) as {
      success: boolean;
      data?: InvitationResponse[];
    };
    const nextInvites: Invitation[] = (payload.data ?? [])
      .filter((invite) => mapInvitationStatus(invite.status) === "pending")
      .map((invite) => ({
        id: invite.id,
        organizationId: invite.organizationId,
        orgId: invite.organizationId,
        orgName: org.name,
        email: invite.email,
        firstName: invite.firstName,
        lastName: invite.lastName,
        status: mapInvitationStatus(invite.status),
        invitationUrl: invite.invitationLink,
      }));

    const updatedOrg: Org = {
      ...org,
      invites: nextInvites,
    };
    const nextOrgs = orgs.map((t) => (t.id === org.id ? updatedOrg : t));

    persist({ ...user, orgs: nextOrgs });
  };

  const onAcceptInvite = async (invite: Invitation) => {
    if (!invite.id) {
      alert("The selected Invite has no ID");
      return;
    }
    try {
      await acceptInvite(invite.id, { email: user.email });
    } catch (error) {
      alert(
        error instanceof Error
          ? error.message
          : "There was an issue with accepting the invite.",
      );
      return;
    }

    alert("invite accepted succesfully");

    const newOrg: Org = {
      id: invite.orgId,
      name: invite.orgName,
      users: [user],
      adminEmails: [],
      invites: [],
    };
    const nextOrg = [...orgs, newOrg];

    const remainingInvites = invites.filter((i) => i.id !== invite.id);

    persist({ ...user, orgs: nextOrg, invites: remainingInvites });
  };

  const declineInvite = async (invite: Invitation) => {
    if (!invite.id) {
      const remainingInvites = invites.filter((i) => i !== invite);
      persist({ ...user, invites: remainingInvites });
      return;
    }

    try {
      const response = await fetch(
        apiUrl(`/api/Invitation/${invite.id}/reject`),
        {
          method: "POST",
        },
      );

      if (!response.ok) {
        const payload = await response.json().catch(() => null);
        throw new Error(
          payload?.message ?? "Einladung konnte nicht abgelehnt werden.",
        );
      }

      const remainingInvites = invites.filter((i) => i.id !== invite.id);
      persist({ ...user, invites: remainingInvites });
    } catch (error) {
      alert(
        error instanceof Error
          ? error.message
          : "Einladung konnte nicht abgelehnt werden.",
      );
    }
  };

  const leaveOrg = async (orgId: string) => {
    const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "";

    setLeaveError(null);
    setIsLeavingOrgId(orgId);

    try {
      const response = await fetch(`${apiBaseUrl}/api/Membership/leave`, {
        method: "DELETE",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          userId: user.id,
          organizationId: orgId,
        }),
      });

      if (!response.ok) {
        const message = await response.text();
        throw new Error(
          message || "Organisation konnte nicht verlassen werden.",
        );
      }

      const nextOrgs = orgs.filter((t) => t.id !== orgId);
      persist({ ...user, orgs: nextOrgs });
      if (selectedOrgId === orgId) {
        setSelectedOrgId(nextOrgs[0]?.id ?? null);
      }
    } catch (error) {
      if (error instanceof TypeError) {
        setLeaveError(
          "Backend nicht erreichbar. Starte die API und pruefe, ob sie auf Port 5186 laeuft.",
        );
      } else {
        setLeaveError(
          error instanceof Error
            ? error.message
            : "Organisation konnte nicht verlassen werden.",
        );
      }
    } finally {
      setIsLeavingOrgId(null);
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

    const email = newInviteEmail.trim();

    setInviteError(null);
    setInviteSuccess(null);
    setIsSendingInvite(true);

    try {
      if (!guidPattern.test(org.id)) {
        throw new Error(
          "Diese Organisation hat noch keine echte DB-ID. Lade die Organisationen zuerst aus dem Backend statt aus den Mock-Daten.",
        );
      }

      const response = await fetch(apiUrl("/api/Invitation/send"), {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          organizationId: org.id,
          email,
          createdByEmail: user.email,
        }),
      });

      if (!response.ok) {
        const payload = await response.json().catch(() => null);
        const validationErrors = payload?.errors
          ? Object.values(payload.errors)
              .flat()
              .filter((value): value is string => typeof value === "string")
              .join(" ")
          : null;
        let message =
          payload?.message ??
          validationErrors ??
          "Einladung konnte nicht erstellt werden.";

        if (message.includes("An open invitation already exists")) {
          await syncOrganizationInvites(org);
          message =
            "Für diese E-Mail gibt es bereits eine offene Einladung. Ich habe die Liste unter 'Eingeladen' aktualisiert.";
        }

        throw new Error(message);
      }

      const payload = (await response.json()) as {
        data?: {
          id?: string;
          organizationId?: string;
          invitationLink?: string;
        };
      };
      const invite: Invitation = {
        id: payload.data?.id,
        organizationId: payload.data?.organizationId ?? org.id,
        orgId: org.id,
        orgName: org.name,
        email,
        status: "pending",
        invitationUrl: payload.data?.invitationLink,
      };

      const updatedOrg: Org = {
        ...org,
        invites: [...(org.invites ?? []), invite],
      };

      const nextOrgs = orgs.map((t) => (t.id === org.id ? updatedOrg : t));
      persist({ ...user, orgs: nextOrgs });
      setNewInviteEmail("");
      setInviteSuccess(
        payload.data?.invitationLink
          ? "Einladungslink wurde erstellt und unten gespeichert."
          : "Einladung wurde erstellt und per E-Mail versendet.",
      );
    } catch (error) {
      if (error instanceof TypeError) {
        setInviteError(
          "Backend nicht erreichbar. Starte die API und pruefe, ob sie auf Port 5186 laeuft.",
        );
      } else {
        setInviteError(
          error instanceof Error
            ? error.message
            : "Invitation could not be created.",
        );
      }
    } finally {
      setIsSendingInvite(false);
    }
  };

  const withdrawInvite = async (org: Org, invite: Invitation) => {
    if (!invite.id) {
      setInviteError("Diese Einladung hat keine Backend-ID.");
      return;
    }

    setInviteError(null);
    setWithdrawingInviteId(invite.id);

    try {
      const response = await fetch(
        apiUrl(`/api/Invitation/${invite.id}/reject`),
        {
          method: "POST",
        },
      );

      if (!response.ok) {
        const payload = await response.json().catch(() => null);
        throw new Error(
          payload?.message ?? "Einladung konnte nicht zurueckgezogen werden.",
        );
      }

      const updatedOrg: Org = {
        ...org,
        invites: (org.invites ?? []).filter((i) => i.id !== invite.id),
      };
      const nextOrgs = orgs.map((t) => (t.id === org.id ? updatedOrg : t));
      persist({ ...user, orgs: nextOrgs });
    } catch (error) {
      setInviteError(
        error instanceof Error
          ? error.message
          : "Einladung konnte nicht zurueckgezogen werden.",
      );
    } finally {
      setWithdrawingInviteId(null);
    }
  };

  const renameOrg = (org: Org) => {
    if (!renameValue.trim()) return;
    const updatedOrg: Org = { ...org, name: renameValue.trim() };
    const nextOrgs = orgs.map((t) => (t.id === org.id ? updatedOrg : t));
    persist({ ...user, orgs: nextOrgs });
    setRenameValue("");
  };

  const deleteOrg = async (org: Org) => {
    if (deleteConfirm !== org.name) return;

    setDeleteError(null);
    setDeleteSuccess(null);
    setIsDeletingOrg(true);

    try {
      const response = await fetch(apiUrl(`/api/Organization/${org.id}`), {
        method: "DELETE",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          initiatorUserId: user.id,
          confirmationText: deleteConfirm,
        }),
      });

      if (!response.ok) {
        const message = await response.text();
        throw new Error(message || "Organisation konnte nicht gelöscht werden.");
      }

      const nextOrgs = orgs.filter((t) => t.id !== org.id);
      const nextInvites = (user.invites ?? []).filter((i) => i.orgId !== org.id);
      persist({ ...user, orgs: nextOrgs, invites: nextInvites });
      setSelectedOrgId(nextOrgs[0]?.id ?? null);
      setDeleteConfirm("");
      setDeleteSuccess("Organisation wurde endgültig gelöscht.");
    } catch (error) {
      if (error instanceof TypeError) {
        setDeleteError("Backend nicht erreichbar. Starte die API und pruefe, ob sie auf Port 5186 laeuft.");
      } else {
        setDeleteError(error instanceof Error ? error.message : "Organisation konnte nicht gelöscht werden.");
      }
    } finally {
      setIsDeletingOrg(false);
    }
  };

  const selectedOrg = useMemo(
    () => orgs.find((t) => t.id === selectedOrgId) ?? null,
    [orgs, selectedOrgId],
  );
  const isSelectedAdmin = selectedOrg
    ? selectedOrg.adminEmails?.includes(user.email)
    : false;

  return (
    <div className="grid h-full w-full min-w-0 grid-rows-[3.5rem_1fr] gap-6 p-6">
      <div className="flex items-center justify-between">
        <div className="flex flex-col gap-1">
          <span className="text-xs tracking-[0.28em] text-emerald-300 uppercase">
            Orgs
          </span>
          <h1 className="text-4xl leading-tight font-semibold">My orgs</h1>
          <span className="text-sm text-slate-400">
            Manage memberships, invites, and settings.
          </span>
        </div>
      </div>
      {leaveError && (
        <div className="rounded-2xl border border-rose-400/40 bg-rose-500/10 px-4 py-3 text-sm text-rose-100">
          {leaveError}
        </div>
      )}

      <div className="grid min-w-0 grid-cols-[1.1fr_0.9fr] gap-4 max-xl:grid-cols-1">
        <div className="min-w-0 flex flex-col gap-4 rounded-3xl border border-slate-800 bg-slate-900/70 p-5 shadow-xl backdrop-blur">
          <div className="text-lg font-semibold text-slate-50">Meine Orgs </div>
          {orgs.length === 0 && (
            <div className="rounded-2xl border border-dashed border-slate-800 bg-slate-900/60 p-4 text-slate-400">
              Du bist noch in keinem Org.
            </div>
          )}
          <div className="flex flex-col gap-3">
            {orgs.map((org) => (
              <div
                key={org.id}
                className={`min-w-0 w-full min-h-[12rem] rounded-2xl border ${selectedOrgId === org.id ? "border-emerald-300/70" : "border-slate-800"} bg-slate-900/80 p-4 shadow`}
              >
                <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                  <div className="min-w-0">
                    <div className="text-sm uppercase tracking-[0.16em] text-slate-400">Org</div>
                    <div className="break-words text-lg font-semibold text-slate-50">{org.name}</div>
                  </div>
                  <div className="flex flex-row flex-wrap items-center gap-2 sm:flex-col sm:items-end sm:gap-1 sm:text-right">
                    <span className="rounded-full bg-slate-800 px-3 py-1 text-[11px] uppercase tracking-wide text-slate-200">
                      {currentRole(org)}
                    </span>
                    <span className="text-xs text-slate-400">
                      {org.users.length} Mitglieder
                    </span>
                  </div>
                </div>
                <div className="mt-3 flex flex-col gap-2 sm:flex-row">
                  <button
                    onClick={() => setSelectedOrgId(org.id)}
                    className={`flex-1 rounded-xl border px-3 py-2 text-sm font-semibold transition ${
                      currentRole(org) === "Admin"
                        ? "border-emerald-300/60 bg-emerald-400/10 text-emerald-100 hover:bg-emerald-400/20"
                        : "border-slate-800 bg-slate-900/60 text-slate-300 hover:border-slate-700"
                    }`}
                  >
                    {currentRole(org) === "Admin" ? "Verwalten" : "Ansehen"}
                  </button>
                  <button
                    onClick={() => leaveOrg(org.id)}
                    disabled={isLeavingOrgId === org.id}
                    className="rounded-xl border border-slate-800 bg-slate-900/60 px-3 py-2 text-sm text-slate-300 transition hover:border-rose-400/60 hover:text-rose-200"
                  >
                    {isLeavingOrgId === org.id ? "Verlasse..." : "Austreten"}
                  </button>
                </div>
              </div>
            ))}
          </div>

          <div className="mt-4 rounded-2xl border border-slate-800 bg-slate-900/80 p-4">
            <div className="text-sm font-semibold text-slate-100">
              Pending invitations
            </div>
            {invites.filter((i) => i.status === "pending").length === 0 && (
              <div className="mt-2 text-sm text-slate-500">
                No pending invitations.
              </div>
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
                      <div className="font-semibold text-slate-50">
                        {invite.orgName}
                      </div>
                      <div className="text-xs text-slate-400">
                        Invited as member
                      </div>
                    </div>
                    <div className="flex gap-2">
                      <button
                        onClick={() => onAcceptInvite(invite)}
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

        <div className="min-w-0 flex h-full min-h-[62vh] flex-col gap-4 overflow-hidden rounded-3xl border border-slate-800 bg-slate-900/80 p-5 shadow-xl backdrop-blur">
          {!selectedOrg && (
            <div className="text-sm text-slate-400">
              Chose your organization to manage.
            </div>
          )}
          {selectedOrg && (
            <>
              <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                <div className="min-w-0">
                  <div className="text-xs uppercase tracking-[0.18em] text-emerald-300">Org verwalten</div>
                  <div className="break-words text-2xl font-semibold text-slate-50">{selectedOrg.name}</div>
                </div>
                {!isSelectedAdmin && (
                  <span className="rounded-full bg-slate-800 px-3 py-1 text-[11px] tracking-wide text-slate-300 uppercase">
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
                <div className="mt-4 flex min-w-0 flex-col gap-3">
                  {selectedOrg.users.map((member) => (
                    <div
                      key={member.email}
                      className="flex flex-col gap-3 rounded-xl border border-slate-800 bg-slate-900/70 px-4 py-3 text-sm text-slate-200 md:flex-row md:items-center md:justify-between"
                    >
                      <div className="min-w-0">
                        <div className="break-words font-semibold text-slate-50">{member.username}</div>
                        <div className="break-all text-xs text-slate-400">{member.email}</div>
                      </div>
                      <div className="flex flex-wrap items-center gap-2">
                        <span className="rounded-full bg-slate-800 px-2 py-1 text-[11px] uppercase tracking-wide text-slate-300">
                          {selectedOrg.adminEmails?.includes(member.email) ? "Admin" : "Mitglied"}
                        </span>
                        {isSelectedAdmin && member.email !== user.email && (
                          <>
                            <button
                              onClick={() =>
                                toggleRole(selectedOrg, member.email)
                              }
                              className="rounded-full border border-emerald-300/60 bg-emerald-400/10 px-3 py-1 text-[11px] font-semibold text-emerald-100 hover:bg-emerald-400/20"
                            >
                              Rolle ändern
                            </button>
                            <button
                              onClick={() =>
                                kickUser(selectedOrg, member.email)
                              }
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
                    <div className="text-sm text-slate-500">
                      Keine Mitglieder im Org.
                    </div>
                  )}
                </div>
              )}

              {activeTab === "invites" && (
                <div className="mt-4 flex min-w-0 flex-col gap-3">
                  {(selectedOrg.invites ?? []).length === 0 && (
                    <div className="text-sm text-slate-500">
                      Keine offenen Einladungen.
                    </div>
                  )}
                  {(selectedOrg.invites ?? []).map((inv) => (
                    <div
                      key={`${inv.email}-${inv.orgId}`}
                      className="flex flex-col gap-3 rounded-xl border border-slate-800 bg-slate-900/70 px-4 py-3 text-sm text-slate-200 md:flex-row md:items-center md:justify-between"
                    >
                      <div className="min-w-0">
                        <div className="break-all font-semibold text-slate-50">{inv.email}</div>
                        <div className="text-xs text-slate-400">Status: {inv.status}</div>
                        {inv.invitationUrl && (
                          <a
                            href={inv.invitationUrl}
                            target="_blank"
                            rel="noreferrer"
                            className="mt-1 block text-xs text-emerald-300 underline decoration-emerald-400/40 underline-offset-2"
                          >
                            Invitation-Link öffnen
                          </a>
                        )}
                      </div>
                      {isSelectedAdmin && inv.status === "pending" && (
                        <button
                          onClick={() => withdrawInvite(selectedOrg, inv)}
                          disabled={withdrawingInviteId === inv.id}
                          className="rounded-full border border-rose-300/60 bg-rose-500/10 px-3 py-1 text-[11px] font-semibold text-rose-100 hover:bg-rose-500/20"
                        >
                          {withdrawingInviteId === inv.id
                            ? "Ziehe zurueck..."
                            : "Zurückziehen"}
                        </button>
                      )}
                    </div>
                  ))}
                </div>
              )}

              {activeTab === "invite" && (
                <div className="mt-4 flex flex-col gap-3 rounded-2xl border border-slate-800 bg-slate-900/70 p-4">
                  <div className="text-sm font-semibold text-slate-100">
                    Nutzer per E-Mail einladen
                  </div>
                  <div className="flex gap-2 max-sm:flex-col">
                    <input
                      value={newInviteEmail}
                      onChange={(e) => setNewInviteEmail(e.target.value)}
                      placeholder="email@example.com"
                      className="flex-1 rounded-xl border border-slate-800 bg-slate-900 px-3 py-2 text-sm text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
                    />
                    <button
                      onClick={() => sendInvite(selectedOrg)}
                      disabled={!isSelectedAdmin || isSendingInvite}
                      className="rounded-xl border border-emerald-300/60 bg-emerald-400/15 px-4 py-2 text-sm font-semibold text-emerald-100 transition hover:bg-emerald-400/25 disabled:cursor-not-allowed disabled:border-slate-800 disabled:bg-slate-900/60 disabled:text-slate-500"
                    >
                      {isSendingInvite ? "Sende..." : "Senden"}
                    </button>
                  </div>
                  {inviteSuccess && (
                    <div className="text-xs text-emerald-300">
                      {inviteSuccess}
                    </div>
                  )}
                  {inviteError && (
                    <div className="text-xs text-rose-300">{inviteError}</div>
                  )}
                  {!isSelectedAdmin && (
                    <div className="text-xs text-slate-500">
                      Nur Admins dürfen einladen.
                    </div>
                  )}
                </div>
              )}

              {activeTab === "settings" && (
                <div className="mt-4 flex flex-col gap-4">
                  <div className="rounded-2xl border border-slate-800 bg-slate-900/70 p-4">
                    <div className="text-sm font-semibold text-slate-100">
                      Org umbenennen
                    </div>
                    <div className="mt-2 flex gap-2 max-sm:flex-col">
                      <input
                        value={renameValue}
                        onChange={(e) => setRenameValue(e.target.value)}
                        placeholder={selectedOrg.name}
                        className="flex-1 rounded-xl border border-slate-800 bg-slate-900 px-3 py-2 text-sm text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
                      />
                      <button
                        onClick={() => renameOrg(selectedOrg)}
                        disabled={!isSelectedAdmin}
                        className="rounded-xl border border-emerald-300/60 bg-emerald-400/15 px-4 py-2 text-sm font-semibold text-emerald-100 transition hover:bg-emerald-400/25 disabled:cursor-not-allowed disabled:border-slate-800 disabled:bg-slate-900/60 disabled:text-slate-500"
                      >
                        Speichern
                      </button>
                    </div>
                    {!isSelectedAdmin && (
                      <div className="text-xs text-slate-500">
                        Nur Admins dürfen umbenennen.
                      </div>
                    )}
                  </div>

                  <div className="rounded-2xl border border-rose-400/40 bg-rose-500/10 p-4">
                    <div className="text-sm font-semibold text-rose-50">
                      Organisation löschen
                    </div>
                    <div className="mt-1 text-xs text-rose-100/80">
                      Alle zugehörigen Daten werden unwiderruflich gelöscht. Die Organisation
                      muss leer sein, bevor sie gelöscht werden kann. Gib den Organisationsnamen
                      exakt ein, um zu bestätigen.
                    </div>
                    <div className="mt-2 flex gap-2 max-sm:flex-col">
                      <input
                        value={deleteConfirm}
                        onChange={(e) => setDeleteConfirm(e.target.value)}
                        placeholder={selectedOrg.name}
                        className="flex-1 rounded-xl border border-rose-400/50 bg-rose-500/10 px-3 py-2 text-sm text-rose-50 ring-rose-400/40 outline-none focus:border-rose-300/80 focus:ring"
                      />
                      <button
                        onClick={() => void deleteOrg(selectedOrg)}
                        disabled={
                          !isSelectedAdmin || deleteConfirm !== selectedOrg.name || isDeletingOrg
                        }
                        className="rounded-xl border border-rose-300/60 bg-rose-500/20 px-4 py-2 text-sm font-semibold text-rose-50 transition hover:bg-rose-500/30 disabled:cursor-not-allowed disabled:border-slate-800 disabled:bg-slate-900/60 disabled:text-slate-500"
                      >
                        {isDeletingOrg ? "Loesche..." : "Organisation löschen"}
                      </button>
                    </div>
                    {deleteSuccess && (
                      <div className="mt-2 text-xs text-emerald-300">{deleteSuccess}</div>
                    )}
                    {deleteError && (
                      <div className="mt-2 text-xs text-rose-300">{deleteError}</div>
                    )}
                    {!isSelectedAdmin && (
                      <div className="text-xs text-rose-100/80">
                        Nur Admins dürfen löschen.
                      </div>
                    )}
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
