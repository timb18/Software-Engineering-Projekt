import type { Org, Invitation, User } from "./types";

const API_BASE = import.meta.env.VITE_API_BASE_URL ?? "";

type OrganizationApiResponse = {
  id: string;
  name: string;
  users: Array<{
    id: string;
    email: string;
    username: string;
    role: string;
  }>;
  invites: Array<{
    id: string;
    organizationId: string;
    email: string;
    firstName?: string;
    lastName?: string;
    status: string;
    invitationLink?: string;
  }>;
};

const mapInvite = (
  invite: OrganizationApiResponse["invites"][number],
  orgName: string,
): Invitation => ({
  id: invite.id,
  organizationId: invite.organizationId,
  orgId: invite.organizationId,
  orgName,
  email: invite.email,
  firstName: invite.firstName,
  lastName: invite.lastName,
  status:
    invite.status === "open"
      ? "pending"
      : (invite.status as Invitation["status"]),
  invitationUrl: invite.invitationLink,
});

const mapMember = (member: OrganizationApiResponse["users"][number]): User => ({
  id: member.id,
  email: member.email,
  username: member.username,
  displayName: member.username,
  role: member.role === "organizer" ? "admin" : "user",
  orgs: [],
  tasks: [],
  invites: [],
});

const mapOrganization = (org: OrganizationApiResponse): Org => ({
  id: org.id,
  name: org.name,
  users: org.users.map(mapMember),
  adminEmails: org.users
    .filter((member) => member.role === "organizer")
    .map((member) => member.email),
  invites: org.invites.map((invite) => mapInvite(invite, org.name)),
});

export async function fetchOrganizationsByUserEmail(email: string): Promise<Org[]> {
  const res = await fetch(
    `${API_BASE}/api/Organization/by-user-email?email=${encodeURIComponent(email)}`,
  );

  if (!res.ok) {
    throw new Error(`Failed to fetch organizations: ${res.status} ${res.statusText}`);
  }

  const organizations = (await res.json()) as OrganizationApiResponse[];
  const deduped = new Map<string, Org>();

  organizations.map(mapOrganization).forEach((organization) => {
    const key = organization.name.trim().toLowerCase();
    if (!deduped.has(key)) {
      deduped.set(key, organization);
    }
  });

  return [...deduped.values()];
}
