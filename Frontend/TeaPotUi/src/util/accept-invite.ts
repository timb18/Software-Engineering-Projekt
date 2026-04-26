const API_BASE = import.meta.env.VITE_API_BASE_URL ?? "";

const acceptInvite = async (invitationId: string) => {
  const response = await fetch(`${API_BASE}/Invitation/${invitationId}/accept`);
  return response.ok;
};

export default acceptInvite;
