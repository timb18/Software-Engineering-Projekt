import type { Org, Invitation, User } from "./types";

const API_BASE = import.meta.env.VITE_API_BASE_URL ?? "";

/**
 * Request body for removing a user from an organization.
 *
 * All fields are required:
 * - initiatorUserId: The admin performing the removal (for audit/authorization)
 * - userId: The user to remove
 * - organizationId: The organization to remove them from
 */
type RemoveMembershipRequest = {
  initiatorUserId: string;
  userId: string;
  organizationId: string;
};

/**
 * Request body for renaming an organization.
 *
 * Fields:
 * - initiatorUserId: The admin performing rename (for authorization)
 * - organizationId: The organization to rename
 * - name: New organization name
 */
type RenameOrganizationRequest = {
  initiatorUserId: string;
  organizationId: string;
  name: string;
};

type DeleteOrganizationRequest = {
  initiatorUserId: string;
  organizationId: string;
  confirmationText: string;
};

/**
 * Request body for changing a user's role in an organization.
 *
 * Fields:
 * - initiatorUserId: The admin performing the change (for authorization)
 * - userId: The user whose role to change
 * - organizationId: The organization context
 * - role: "admin" or "user" (backend calls admins "organizer")
 */
type UpdateMembershipRoleRequest = {
  initiatorUserId: string;
  userId: string;
  organizationId: string;
  role: "admin" | "user";
};

/**
 * Raw API response format from /api/Organization endpoints.
 *
 * This structure is returned by the backend and must be mapped to the
 * frontend Org type using mapOrganization().
 *
 * Notable differences from frontend Org type:
 * - users array has role field as string ("organizer" or other)
 * - invites array has status "open" (backend) vs "pending" (frontend)
 * - invitationLink is the raw URL field from API
 */
type OrganizationApiResponse = {
  id: string;
  name: string;
  description?: string;
  maxUsers?: number;
  workProfileId?: string | null;
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

/**
 * Maps backend invitation object to frontend Invitation type.
 *
 * Transforms:
 * - status: "open" (backend) → "pending" (frontend)
 * - invitationLink → invitationUrl
 * - Adds orgName for display purposes
 * - Duplicates organizationId in both organizationId and orgId for compatibility
 *
 * @param invite - Raw invitation from API response
 * @param orgName - Organization name (for UI display)
 * @returns Mapped Invitation object
 */
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
  // Backend uses "open" for pending invitations; frontend uses "pending"
  status:
    invite.status === "open"
      ? "pending"
      : (invite.status as Invitation["status"]),
  invitationUrl: invite.invitationLink,
});

/**
 * Maps backend member object to frontend User type.
 *
 * Creates a minimal User object suitable for member lists.
 * Note: orgs, tasks, invites arrays are empty because member list doesn't need them.
 *
 * Transforms:
 * - role: "organizer" (backend) → "admin" (frontend)
 * - username → displayName (for consistency)
 *
 * @param member - Raw member from API response
 * @returns Mapped User object
 */
const mapMember = (member: OrganizationApiResponse["users"][number]): User => ({
  id: member.id,
  email: member.email,
  username: member.username,
  displayName: member.username,
  // Backend uses "organizer" for admins; frontend uses "admin"
  role: member.role === "organizer" ? "admin" : "user",
  orgs: [],
  tasks: [],
  invites: [],
});

/**
 * Sorts members for consistent UI display.
 *
 * Sort order:
 * 1. Admins first, then users
 * 2. Within each role, sort by username (alphabetically)
 *
 * @param members - Unsorted member list
 * @returns New array with sorted members
 */
const sortMembers = (members: User[]) =>
  [...members].sort((a, b) => {
    // Admins come first (return -1 to place `a` before `b`)
    if (a.role !== b.role) {
      return a.role === "admin" ? -1 : 1;
    }

    // Within same role, sort by username alphabetically
    return a.username.localeCompare(b.username);
  });

/**
 * Maps backend organization object to frontend Org type.
 *
 * Performs multiple transformations:
 * 1. Maps each member (user) to User type with role normalization
 * 2. Sorts members (admins first, then alphabetically)
 * 3. Maps each invitation to Invitation type with status normalization
 * 4. Extracts admin email list for quick lookup
 *
 * @param org - Raw organization from API response
 * @returns Mapped Org object ready for frontend
 */
const mapOrganization = (org: OrganizationApiResponse): Org => ({
  id: org.id,
  name: org.name,
  description: org.description,
  maxUsers: org.maxUsers,
  workProfileId: org.workProfileId ?? null,
  // Sort members: admins first, then users, alphabetically within each group
  users: sortMembers(org.users.map(mapMember)),
  // Extract admin emails for quick authorization checks
  adminEmails: org.users
    .filter((member) => member.role === "organizer")
    .map((member) => member.email),
  // Map all invitations with status normalization
  invites: org.invites.map((invite) => mapInvite(invite, org.name)),
});

/**
 * Fetches all organizations for a given user email address.
 *
 * This function:
 * 1. Queries backend for all organizations accessible by email
 * 2. Maps each raw response to frontend Org type
 * 3. Deduplicates by organization ID (in case backend returns duplicates)
 * 4. Returns sorted array of Org objects
 *
 * Used during user initialization to load all orgs the user belongs to.
 *
 * @param email - User email address (should be lowercase)
 * @param token - accesstoken for the api
 * @returns Promise resolving to array of Org objects
 * @throws Error with detailed message if fetch fails
 *
 * @remarks
 * Deduplication is needed because the backend may return the same org multiple
 * times for historical or data consistency reasons. We keep the first instance
 * and discard duplicates using a Map.
 */
export async function fetchOrganizationsByUserEmail(
  email: string,
  token: string,
): Promise<Org[]> {
  const res = await fetch(
    `${API_BASE}/api/Organization/by-user-email?email=${encodeURIComponent(email)}`,
    { headers: { Authorization: `Bearer ${token}` } },
  );

  if (!res.ok) {
    throw new Error(
      `Failed to fetch organizations: ${res.status} ${res.statusText}`,
    );
  }

  const organizations = (await res.json()) as OrganizationApiResponse[];
  // Deduplicate by organization ID (keeps first instance of each org)
  const deduped = new Map<string, Org>();

  organizations.map(mapOrganization).forEach((organization) => {
    if (!deduped.has(organization.id)) {
      deduped.set(organization.id, organization);
    }
  });

  return [...deduped.values()];
}

/**
 * Removes a user from an organization.
 *
 * Authorization: Only the initiating user (if they're an admin) or a system
 * admin can remove members from an organization.
 *
 * Flow:
 * 1. Build RemoveMembershipRequest with initiatorUserId for authorization
 * 2. Send DELETE to /api/Membership/remove
 * 3. If successful (2xx), membership is severed and user loses access
 * 4. If fails, error message is extracted from response text
 *
 * @param request - RemoveMembershipRequest with initiatorUserId, userId, organizationId
 * @param token -
 * @throws Error with backend message if removal fails (authorization, not found, etc.)
 *
 * @remarks
 * After successful removal:
 * - User no longer appears in organization member list
 * - User's tasks in that org may be reassigned or archived (backend dependent)
 * - User can be re-invited to the organization later
 */
export async function removeUserFromOrganization(
  request: RemoveMembershipRequest,
  token: string,
): Promise<void> {
  const res = await fetch(`${API_BASE}/api/Membership/remove`, {
    method: "DELETE",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${token}`,
    },
    body: JSON.stringify(request),
  });

  if (!res.ok) {
    const message = await res.text();
    throw new Error(
      message || "Member could not be removed from the organization.",
    );
  }
}

/**
 * Renames an organization.
 *
 * Authorization: Only admins of the organization can rename it.
 * The initiatorUserId is sent to the backend for authorization checking.
 *
 * Flow:
 * 1. Build RenameOrganizationRequest with initiatorUserId
 * 2. POST to /api/Organization/{organizationId}/rename
 * 3. Backend validates initiator is an admin
 * 4. If successful, organization.name is updated
 * 5. Frontend should refetch organizations to show updated name
 *
 * @param request - RenameOrganizationRequest with organizationId, initiatorUserId, name
 * @param token - accesstoken for the api
 * @throws Error with descriptive message if rename fails
 *
 * @remarks
 * Error handling:
 * - 404 (not found): Could indicate API route mismatch; suggests restarting backend
 * - 403 (forbidden): Initiator is not an admin
 * - 400 (bad request): Invalid organization ID or name format
 * - Other errors: Backend-specific validation failures
 */
export async function renameOrganization(
  request: RenameOrganizationRequest,
  token: string,
): Promise<void> {
  const res = await fetch(
    `${API_BASE}/api/Organization/${request.organizationId}/rename`,
    {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Authorization: `Bearer ${token}`,
      },
      body: JSON.stringify({
        initiatorUserId: request.initiatorUserId,
        name: request.name,
      }),
    },
  );

  if (!res.ok) {
    const message = await res.text();
    if (res.status === 404 && !message) {
      throw new Error(
        "Organization rename API route was not found. Restart the backend with the latest code and check VITE_API_BASE_URL.",
      );
    }

    throw new Error(
      message ||
        `Organization could not be renamed. (${res.status} ${res.statusText})`,
    );
  }
}

export async function deleteOrganization(
  request: DeleteOrganizationRequest,
  token: string,
): Promise<void> {
  const res = await fetch(`${API_BASE}/api/Organization/${request.organizationId}`, {
    method: "DELETE",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${token}`,
    },
    body: JSON.stringify({
      initiatorUserId: request.initiatorUserId,
      confirmationText: request.confirmationText,
    }),
  });

  if (!res.ok) {
    const message = await res.text();
    if (res.status === 404 && !message) {
      throw new Error(
        "Organization delete API route was not found. Restart the backend with the latest code and check VITE_API_BASE_URL.",
      );
    }

    throw new Error(
      message ||
        `Organization could not be deleted. (${res.status} ${res.statusText})`,
    );
  }
}

export async function updateMembershipRole(
  request: UpdateMembershipRoleRequest,
  token: string,
): Promise<void> {
  const res = await fetch(`${API_BASE}/api/Membership/role`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${token}`,
    },
    body: JSON.stringify(request),
  });

  if (!res.ok) {
    const message = await res.text();
    if (res.status === 404 && !message) {
      throw new Error(
        "Member role API route was not found. Restart the backend with the latest code and check VITE_API_BASE_URL.",
      );
    }

    throw new Error(
      message ||
        `Member role could not be updated. (${res.status} ${res.statusText})`,
    );
  }
}
