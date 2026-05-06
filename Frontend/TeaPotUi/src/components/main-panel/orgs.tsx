import { useEffect, useMemo, useState, type FC } from "react";
import useUserStore from "../../stores/user-store";
import type { Invitation, Org, User } from "../../util/types";
import acceptInvite from "../../util/accept-invite";
import {
  fetchOrganizationsByUserEmail,
  renameOrganization,
  removeUserFromOrganization,
  updateMembershipRole,
} from "../../util/org-api";

const tabOptions = ["members", "invites", "invite", "settings"] as const;
type Tab = (typeof tabOptions)[number];
const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "";
const apiUrl = (path: string) => `${apiBaseUrl}${path}`;
const guidPattern =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
const emailPattern = /^[^\s@]+@[^\s@.]+(?:\.[^\s@.]+)+$/;

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

const sortMembersByRole = (members: User[]) =>
  [...members].sort((a, b) => {
    if (a.role !== b.role) {
      return a.role === "admin" ? -1 : 1;
    }

    return a.username.localeCompare(b.username);
  });

const Orgs: FC = () => {
  const { user, setUser, activeOrganizationId, setActiveOrganization } = useUserStore();

  const [orgs, setOrgs] = useState<Org[]>(user?.orgs ?? []);
  const [invites, setInvites] = useState<Invitation[]>(user?.invites ?? []);
  const [activeTab, setActiveTab] = useState<Tab>("members");
  const [newInviteEmail, setNewInviteEmail] = useState("");
  const [inviteError, setInviteError] = useState<string | null>(null);
  const [inviteSuccess, setInviteSuccess] = useState<string | null>(null);
  const [lastInviteLink, setLastInviteLink] = useState<string | null>(null);
  const [copiedInviteId, setCopiedInviteId] = useState<string | null>(null);
  const [isSendingInvite, setIsSendingInvite] = useState(false);
  const [withdrawingInviteId, setWithdrawingInviteId] = useState<string | null>(
    null,
  );
  const [leaveError, setLeaveError] = useState<string | null>(null);
  const [isLeavingOrgId, setIsLeavingOrgId] = useState<string | null>(null);
  const [isKickingMemberKey, setIsKickingMemberKey] = useState<string | null>(null);
  const [isChangingRoleKey, setIsChangingRoleKey] = useState<string | null>(null);
  const [renameValue, setRenameValue] = useState("");
  const [renameError, setRenameError] = useState<string | null>(null);
  const [isRenamingOrg, setIsRenamingOrg] = useState(false);
  const [deleteConfirm, setDeleteConfirm] = useState("");
  const [deleteError, setDeleteError] = useState<string | null>(null);
  const [deleteSuccess, setDeleteSuccess] = useState<string | null>(null);
  const [isDeletingOrg, setIsDeletingOrg] = useState(false);

  const persist = (nextUser: User) => {
    const nextOrgs = nextUser.orgs ?? [];

    setUser(nextUser);
    setOrgs(nextOrgs);
    setInvites(nextUser.invites ?? []);

    if (nextOrgs.length > 0 && !nextOrgs.some((o) => o.id === activeOrganizationId)) {
      void setActiveOrganization(nextOrgs[0].id);
    }
  };

  const refreshOrganizationsFromBackend = async () => {
    const organizations = await fetchOrganizationsByUserEmail(user.email);
    const nextOrgs = await Promise.all(
      organizations.map(async (org) => ({
        ...org,
        invites: await fetchOrganizationInvites(org),
      })),
    );

    persist({ ...user, orgs: nextOrgs });
    return nextOrgs;
  };

  const fetchOrganizationInvites = async (org: Org): Promise<Invitation[]> => {
    const response = await fetch(apiUrl(`/api/Invitation/organization/${org.id}`));

    if (!response.ok) {
      return org.invites ?? [];
    }

    const payload = (await response.json()) as {
      success: boolean;
      data?: InvitationResponse[];
    };

    return (payload.data ?? [])
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
  };

  useEffect(() => {
    const loadOrganizations = async () => {
      if (!user.email || user.email === "example@default.com") {
        return;
      }

      try {
        const organizations = await fetchOrganizationsByUserEmail(user.email);
        const nextOrgs = await Promise.all(
          organizations.map(async (org) => ({
            ...org,
            invites: await fetchOrganizationInvites(org),
          })),
        );
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
                "Unknown organization",
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

  const copyInvitationLink = async (link: string, inviteId: string) => {
    try {
      await navigator.clipboard.writeText(link);
      setCopiedInviteId(inviteId);
      window.setTimeout(() => setCopiedInviteId(null), 1800);
    } catch {
      setInviteError("The link could not be copied automatically. Select it and copy it manually.");
    }
  };

  const syncOrganizationInvites = async (org: Org) => {
    const nextInvites = await fetchOrganizationInvites(org);

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
      await acceptInvite(invite.id, { userId: user.id });
    } catch (error) {
      alert(
        error instanceof Error
          ? error.message
          : "There was an issue with accepting the invite.",
      );
      return;
    }

    try {
      const nextOrgs = await refreshOrganizationsFromBackend();
      const acceptedOrg = nextOrgs.find((org) => org.id === invite.orgId);
      if (acceptedOrg) {
        void setActiveOrganization(acceptedOrg.id);
      }
      const remainingInvites = invites.filter((i) => i.id !== invite.id);
      persist({ ...user, orgs: nextOrgs, invites: remainingInvites });
    } catch (error) {
      alert(
        error instanceof Error
          ? error.message
          : "Invite was accepted, but organizations could not be refreshed.",
      );
    }
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
          payload?.message ?? "Invitation could not be declined.",
        );
      }

      const remainingInvites = invites.filter((i) => i.id !== invite.id);
      persist({ ...user, invites: remainingInvites });
    } catch (error) {
      alert(
        error instanceof Error
          ? error.message
          : "Invitation could not be declined.",
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
          message || "Organization could not be left.",
        );
      }

      const nextOrgs = orgs.filter((t) => t.id !== orgId);
      persist({ ...user, orgs: nextOrgs });
      if (activeOrganizationId === orgId) {
        void setActiveOrganization(nextOrgs[0]?.id ?? null);
      }
    } catch (error) {
      if (error instanceof TypeError) {
        setLeaveError(
          "Backend is unreachable. Start the API and check whether it is running on port 5186.",
        );
      } else {
        setLeaveError(
          error instanceof Error
            ? error.message
            : "Organization could not be left.",
        );
      }
    } finally {
      setIsLeavingOrgId(null);
    }
  };

  const toggleRole = async (org: Org, email: string) => {
    if (!guidPattern.test(org.id)) {
      setLeaveError(
        "This organization is not loaded from the database yet. Reload after the backend is running, then try again.",
      );
      return;
    }

    const memberToUpdate = org.users.find((u) => u.email === email);

    if (!memberToUpdate) {
      setLeaveError("Member was not found in this organization.");
      return;
    }

    const isAdmin = org.adminEmails?.includes(email) ?? false;
    const nextRole = isAdmin ? "user" : "admin";
    const memberKey = `${org.id}:${email}`;

    setLeaveError(null);
    setIsChangingRoleKey(memberKey);

    try {
      await updateMembershipRole({
        initiatorUserId: user.id,
        userId: memberToUpdate.id,
        organizationId: org.id,
        role: nextRole,
      });

      const updatedOrg: Org = {
        ...org,
        adminEmails: isAdmin
          ? (org.adminEmails ?? []).filter((e) => e !== email)
          : [...(org.adminEmails ?? []), email],
        users: org.users.map((member) =>
          member.email === email ? { ...member, role: nextRole } : member,
        ),
      };
      const nextOrgs = orgs.map((t) => (t.id === org.id ? updatedOrg : t));
      persist({ ...user, orgs: nextOrgs });
    } catch (error) {
      if (error instanceof TypeError) {
        setLeaveError(
          "Backend is unreachable. Start the API and check whether it is running on port 5186.",
        );
      } else {
        setLeaveError(
          error instanceof Error
            ? error.message
            : "Member role could not be updated.",
        );
      }
    } finally {
      setIsChangingRoleKey(null);
    }
  };

  const kickUser = async (org: Org, email: string) => {
    if (!guidPattern.test(org.id)) {
      setLeaveError(
        "This organization is not loaded from the database yet. Reload after the backend is running, then try again.",
      );
      return;
    }

    const memberToKick = org.users.find((u) => u.email === email);

    if (!memberToKick) {
      setLeaveError("Member was not found in this organization.");
      return;
    }

    const memberKey = `${org.id}:${email}`;
    setLeaveError(null);
    setIsKickingMemberKey(memberKey);

    try {
      await removeUserFromOrganization({
        initiatorUserId: user.id,
        userId: memberToKick.id,
        organizationId: org.id,
      });

      const updatedOrg: Org = {
        ...org,
        users: org.users.filter((u) => u.email !== email),
        adminEmails: (org.adminEmails ?? []).filter((e) => e !== email),
      };
      const nextOrgs = orgs.map((t) => (t.id === org.id ? updatedOrg : t));
      persist({ ...user, orgs: nextOrgs });
    } catch (error) {
      if (error instanceof TypeError) {
        setLeaveError(
          "Backend is unreachable. Start the API and check whether it is running on port 5186.",
        );
      } else {
        setLeaveError(
          error instanceof Error
            ? error.message
            : "Member could not be removed.",
        );
      }
    } finally {
      setIsKickingMemberKey(null);
    }
  };

  const sendInvite = async (org: Org) => {
    if (!newInviteEmail.trim()) return;

    const email = newInviteEmail.trim();

    setInviteError(null);
    setInviteSuccess(null);
    setLastInviteLink(null);

    if (!emailPattern.test(email)) {
      setInviteError("Bitte gib eine gültige E-Mail-Adresse ein.");
      return;
    }

    setIsSendingInvite(true);

    try {
      if (!guidPattern.test(org.id)) {
        throw new Error(
          "This organization does not have a real database ID yet. Load organizations from the backend instead of mock data.",
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
          "Invitation could not be created.";

        if (message.includes("Email format is invalid")) {
          message = "Bitte gib eine gültige E-Mail-Adresse ein.";
        }

        if (message.includes("An open invitation already exists")) {
          await syncOrganizationInvites(org);
          message =
            "There is already an open invitation for this email. I refreshed the Invited list.";
        }

        throw new Error(message);
      }

      const payload = (await response.json()) as {
        data?: {
          id?: string;
          organizationId?: string;
          invitationLink?: string;
          emailSent?: boolean | null;
          emailError?: string | null;
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
      setLastInviteLink(payload.data?.invitationLink ?? null);
      setInviteSuccess(
        payload.data?.emailSent
          ? "Invitation was created and sent by email."
          : payload.data?.invitationLink
            ? "Email delivery could not be confirmed. Copy the invitation link and send it manually."
          : "Invitation was created and sent by email.",
      );
    } catch (error) {
      if (error instanceof TypeError) {
        setInviteError(
          "Backend is unreachable. Start the API and check whether it is running on port 5186.",
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
      setInviteError("This invitation does not have a backend ID.");
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
          payload?.message ?? "Invitation could not be withdrawn.",
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
          : "Invitation could not be withdrawn.",
      );
    } finally {
      setWithdrawingInviteId(null);
    }
  };

  const renameOrg = async (org: Org) => {
    const nextName = renameValue.trim();
    if (!nextName) return;

    setRenameError(null);

    if (!guidPattern.test(org.id)) {
      setRenameError(
        "This organization is not loaded from the database yet. Reload after the backend is running, then try again.",
      );
      return;
    }

    setIsRenamingOrg(true);

    try {
      await renameOrganization({
        initiatorUserId: user.id,
        organizationId: org.id,
        name: nextName,
      });

      const updatedOrg: Org = {
        ...org,
        name: nextName,
        invites: (org.invites ?? []).map((invite) => ({
          ...invite,
          orgName: nextName,
        })),
      };
      const nextOrgs = orgs.map((t) => (t.id === org.id ? updatedOrg : t));
      persist({ ...user, orgs: nextOrgs });
      setRenameValue("");
    } catch (error) {
      if (error instanceof TypeError) {
        setRenameError(
          "Backend is unreachable. Start the API and check whether it is running on port 5186.",
        );
      } else {
        setRenameError(error instanceof Error ? error.message : "Organization could not be renamed.");
      }
    } finally {
      setIsRenamingOrg(false);
    }
  };

  const deleteOrg = async (org: Org) => {
    if (deleteConfirm !== org.name) return;

    setDeleteError(null);
    setDeleteSuccess(null);

    if (!guidPattern.test(org.id)) {
      setDeleteError(
        "This organization is not loaded from the database yet. Reload after the backend is running, then try again.",
      );
      return;
    }

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
        throw new Error(message || "Organization could not be deleted.");
      }

      const nextOrgs = orgs.filter((t) => t.id !== org.id);
      const nextInvites = (user.invites ?? []).filter((i) => i.orgId !== org.id);
      persist({ ...user, orgs: nextOrgs, invites: nextInvites });
      void setActiveOrganization(nextOrgs[0]?.id ?? null);
      setDeleteConfirm("");
      setDeleteSuccess("Organization was permanently deleted.");
    } catch (error) {
      if (error instanceof TypeError) {
        setDeleteError("Backend is unreachable. Start the API and check whether it is running on port 5186.");
      } else {
        setDeleteError(error instanceof Error ? error.message : "Organization could not be deleted.");
      }
    } finally {
      setIsDeletingOrg(false);
    }
  };

  const selectedOrg = useMemo(
    () => orgs.find((t) => t.id === activeOrganizationId) ?? orgs[0] ?? null,
    [activeOrganizationId, orgs],
  );
  const isSelectedAdmin = selectedOrg
    ? selectedOrg.adminEmails?.includes(user.email)
    : false;
  const visibleTabs = isSelectedAdmin ? tabOptions : (["members"] as const);
  const sortedSelectedMembers = useMemo(
    () => sortMembersByRole(selectedOrg?.users ?? []),
    [selectedOrg?.users],
  );

  useEffect(() => {
    if (!isSelectedAdmin && activeTab !== "members") {
      setActiveTab("members");
    }
  }, [activeTab, isSelectedAdmin]);

  useEffect(() => {
    if (activeTab !== "invites" || !selectedOrg || !isSelectedAdmin) {
      return;
    }

    void syncOrganizationInvites(selectedOrg);
    // Keep the invite list honest when recipients accept/decline in another tab.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [activeTab, selectedOrg?.id, isSelectedAdmin]);

  useEffect(() => {
    if (!selectedOrg || !isSelectedAdmin) {
      return;
    }

    const syncOnFocus = () => {
      if (document.visibilityState === "visible" && activeTab === "invites") {
        void syncOrganizationInvites(selectedOrg);
      }
    };

    document.addEventListener("visibilitychange", syncOnFocus);
    window.addEventListener("focus", syncOnFocus);

    return () => {
      document.removeEventListener("visibilitychange", syncOnFocus);
      window.removeEventListener("focus", syncOnFocus);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [activeTab, selectedOrg?.id, isSelectedAdmin]);

  return (
    <div className="grid min-h-full w-full min-w-0 grid-rows-[3.5rem_auto] gap-6 p-6">
      <div className="flex items-center justify-between">
        <div className="flex flex-col gap-1">
          <span className="text-xs tracking-[0.28em] text-emerald-300 uppercase">
            Teams
          </span>
          <h1 className="text-4xl leading-tight font-semibold">Organizations</h1>
          <span className="text-sm text-slate-400">
            Members, invitations, and organization settings.
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
          <div className="text-lg font-semibold text-slate-50">Organizations</div>
          {orgs.length === 0 && (
            <div className="rounded-2xl border border-dashed border-slate-800 bg-slate-900/60 p-4 text-slate-400">
              You are not in any organization yet.
            </div>
          )}
          <div className="flex flex-col gap-3">
            {orgs.map((org) => (
              <div
                key={org.id}
                className={`min-w-0 w-full min-h-[12rem] rounded-2xl border ${activeOrganizationId === org.id ? "border-emerald-300/70" : "border-slate-800"} bg-slate-900/80 p-4 shadow`}
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
                      {org.users.length} members
                    </span>
                  </div>
                </div>
                <div className="mt-3 flex flex-col gap-2 sm:flex-row">
                  <button
                    onClick={() => void setActiveOrganization(org.id)}
                    className={`flex-1 rounded-xl border px-3 py-2 text-sm font-semibold transition ${
                      currentRole(org) === "Admin"
                        ? "border-emerald-300/60 bg-emerald-400/10 text-emerald-100 hover:bg-emerald-400/20"
                        : "border-slate-800 bg-slate-900/60 text-slate-300 hover:border-slate-700"
                    }`}
                  >
                    Select
                  </button>
                  <button
                    onClick={() => leaveOrg(org.id)}
                    disabled={isLeavingOrgId === org.id}
                    className="rounded-xl border border-slate-800 bg-slate-900/60 px-3 py-2 text-sm text-slate-300 transition hover:border-rose-400/60 hover:text-rose-200"
                  >
                    {isLeavingOrgId === org.id ? "Leaving..." : "Leave"}
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
                        Accept
                      </button>
                      <button
                        onClick={() => declineInvite(invite)}
                        className="rounded-full border border-slate-700 bg-slate-900/60 px-3 py-1 text-xs font-semibold text-slate-300 hover:border-rose-300/60 hover:text-rose-200"
                      >
                        Decline
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
                  <div className="text-xs uppercase tracking-[0.18em] text-emerald-300">Organization</div>
                  <div className="break-words text-2xl font-semibold text-slate-50">{selectedOrg.name}</div>
                </div>
                {!isSelectedAdmin && (
                  <span className="rounded-full bg-slate-800 px-3 py-1 text-[11px] tracking-wide text-slate-300 uppercase">
                    Member view
                  </span>
                )}
              </div>

              <div className="mt-3 flex flex-wrap gap-2 text-sm">
                {visibleTabs.map((tab) => (
                  <button
                    key={tab}
                    onClick={() => setActiveTab(tab)}
                    className={`rounded-full px-4 py-2 font-semibold transition ${
                      activeTab === tab
                        ? "border border-emerald-300/60 bg-emerald-400/15 text-emerald-100"
                        : "border border-slate-800 bg-slate-900/60 text-slate-300 hover:border-emerald-300/40 hover:text-emerald-100"
                    }`}
                  >
                    {tab === "members" && "Active members"}
                    {tab === "invites" && "Invited"}
                    {tab === "invite" && "Invite"}
                    {tab === "settings" && "Settings"}
                  </button>
                ))}
              </div>

              {activeTab === "members" && (
                <div className="mt-4 flex min-w-0 flex-col gap-3">
                  {sortedSelectedMembers.map((member) => (
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
                          {member.role === "admin" ? "Admin" : "Member"}
                        </span>
                        {isSelectedAdmin && member.email !== user.email && (
                          <>
                            <button
                              onClick={() =>
                                void toggleRole(selectedOrg, member.email)
                              }
                              disabled={isChangingRoleKey === `${selectedOrg.id}:${member.email}`}
                              className="rounded-full border border-emerald-300/60 bg-emerald-400/10 px-3 py-1 text-[11px] font-semibold text-emerald-100 hover:bg-emerald-400/20"
                            >
                              {isChangingRoleKey === `${selectedOrg.id}:${member.email}`
                                ? "Saving..."
                                : "Change role"}
                            </button>
                            <button
                              onClick={() =>
                                void kickUser(selectedOrg, member.email)
                              }
                              disabled={isKickingMemberKey === `${selectedOrg.id}:${member.email}`}
                              className="rounded-full border border-rose-300/60 bg-rose-500/10 px-3 py-1 text-[11px] font-semibold text-rose-100 hover:bg-rose-500/20"
                            >
                              {isKickingMemberKey === `${selectedOrg.id}:${member.email}`
                                ? "Removing..."
                                : "Kick"}
                            </button>
                          </>
                        )}
                      </div>
                    </div>
                  ))}
                  {sortedSelectedMembers.length === 0 && (
                    <div className="text-sm text-slate-500">
                      No members in this organization.
                    </div>
                  )}
                </div>
              )}

              {activeTab === "invites" && (
                <div className="mt-4 flex min-w-0 flex-col gap-3">
                  {(selectedOrg.invites ?? []).length === 0 && (
                    <div className="text-sm text-slate-500">
                      No open invitations.
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
                          <div className="mt-2 flex min-w-0 flex-col gap-2">
                            <a
                              href={inv.invitationUrl}
                              target="_blank"
                              rel="noreferrer"
                              className="break-all text-xs text-emerald-300 underline decoration-emerald-400/40 underline-offset-2"
                            >
                              {inv.invitationUrl}
                            </a>
                            <button
                              onClick={() => copyInvitationLink(inv.invitationUrl!, inv.id ?? inv.email)}
                              className="w-fit rounded-full border border-emerald-300/60 bg-emerald-400/10 px-3 py-1 text-[11px] font-semibold text-emerald-100 hover:bg-emerald-400/20"
                            >
                              {copiedInviteId === (inv.id ?? inv.email) ? "Copied" : "Copy link"}
                            </button>
                          </div>
                        )}
                      </div>
                      {isSelectedAdmin && inv.status === "pending" && (
                        <button
                          onClick={() => withdrawInvite(selectedOrg, inv)}
                          disabled={withdrawingInviteId === inv.id}
                          className="rounded-full border border-rose-300/60 bg-rose-500/10 px-3 py-1 text-[11px] font-semibold text-rose-100 hover:bg-rose-500/20"
                        >
                          {withdrawingInviteId === inv.id
                            ? "Withdrawing..."
                            : "Withdraw"}
                        </button>
                      )}
                    </div>
                  ))}
                </div>
              )}

              {activeTab === "invite" && (
                <div className="mt-4 flex flex-col gap-3 rounded-2xl border border-slate-800 bg-slate-900/70 p-4">
                  <div className="text-sm font-semibold text-slate-100">
                    Invite a user by email
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
                      {isSendingInvite ? "Sending..." : "Send"}
                    </button>
                  </div>
                  {inviteSuccess && (
                    <div className="rounded-xl border border-emerald-300/30 bg-emerald-400/10 p-3 text-xs text-emerald-100">
                      <div>{inviteSuccess}</div>
                      {lastInviteLink && (
                        <div className="mt-2 flex min-w-0 flex-col gap-2">
                          <a
                            href={lastInviteLink}
                            target="_blank"
                            rel="noreferrer"
                            className="break-all text-emerald-200 underline decoration-emerald-400/40 underline-offset-2"
                          >
                            {lastInviteLink}
                          </a>
                          <button
                            onClick={() => copyInvitationLink(lastInviteLink, "latest")}
                            className="w-fit rounded-full border border-emerald-300/60 bg-emerald-400/10 px-3 py-1 text-[11px] font-semibold text-emerald-100 hover:bg-emerald-400/20"
                          >
                            {copiedInviteId === "latest" ? "Copied" : "Copy link"}
                          </button>
                        </div>
                      )}
                    </div>
                  )}
                  {inviteError && (
                    <div className="text-xs text-rose-300">{inviteError}</div>
                  )}
                  {!isSelectedAdmin && (
                    <div className="text-xs text-slate-500">
                      Only admins can invite users.
                    </div>
                  )}
                </div>
              )}

              {activeTab === "settings" && (
                <div className="mt-4 flex flex-col gap-4">
                  <div className="rounded-2xl border border-slate-800 bg-slate-900/70 p-4">
                    <div className="text-sm font-semibold text-slate-100">
                      Rename organization
                    </div>
                    <div className="mt-2 flex gap-2 max-sm:flex-col">
                      <input
                        value={renameValue}
                        onChange={(e) => setRenameValue(e.target.value)}
                        placeholder={selectedOrg.name}
                        className="flex-1 rounded-xl border border-slate-800 bg-slate-900 px-3 py-2 text-sm text-slate-50 ring-emerald-400/40 outline-none focus:border-emerald-400/60 focus:ring"
                      />
                      <button
                        onClick={() => void renameOrg(selectedOrg)}
                        disabled={!isSelectedAdmin || isRenamingOrg}
                        className="rounded-xl border border-emerald-300/60 bg-emerald-400/15 px-4 py-2 text-sm font-semibold text-emerald-100 transition hover:bg-emerald-400/25 disabled:cursor-not-allowed disabled:border-slate-800 disabled:bg-slate-900/60 disabled:text-slate-500"
                      >
                        {isRenamingOrg ? "Saving..." : "Save"}
                      </button>
                    </div>
                    {renameError && (
                      <div className="mt-2 text-xs text-rose-300">{renameError}</div>
                    )}
                    {!isSelectedAdmin && (
                      <div className="text-xs text-slate-500">
                        Only admins can rename the organization.
                      </div>
                    )}
                  </div>

                  <div className="rounded-2xl border border-rose-400/40 bg-rose-500/10 p-4">
                    <div className="text-sm font-semibold text-rose-50">
                      Delete organization
                    </div>
                    <div className="mt-1 text-xs text-rose-100/80">
                      All related data will be permanently deleted. The organization
                      must be empty before it can be deleted. Enter the exact organization
                      name to confirm.
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
                        {isDeletingOrg ? "Deleting..." : "Delete organization"}
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
                        Only admins can delete the organization.
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
