const PENDING_INVITATION_KEY = "teapot-pending-invitation";

export type PendingInvitation = {
  invitationId: string;
  email?: string;
};

const hasStorage = () =>
  typeof window !== "undefined" && typeof window.sessionStorage !== "undefined";

export const savePendingInvitation = (invitation: PendingInvitation) => {
  if (!hasStorage()) {
    return;
  }

  window.sessionStorage.setItem(PENDING_INVITATION_KEY, JSON.stringify(invitation));
};

export const getPendingInvitation = (): PendingInvitation | null => {
  if (!hasStorage()) {
    return null;
  }

  const rawInvitation = window.sessionStorage.getItem(PENDING_INVITATION_KEY);
  if (!rawInvitation) {
    return null;
  }

  try {
    const invitation = JSON.parse(rawInvitation) as Partial<PendingInvitation>;
    return invitation.invitationId ? { invitationId: invitation.invitationId, email: invitation.email } : null;
  } catch {
    return null;
  }
};

export const clearPendingInvitation = () => {
  if (!hasStorage()) {
    return;
  }

  window.sessionStorage.removeItem(PENDING_INVITATION_KEY);
};
