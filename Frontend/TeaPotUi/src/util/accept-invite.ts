const API_BASE = import.meta.env.VITE_API_BASE_URL ?? "";

type AcceptInviteOptions = {
  email?: string;
  userId?: string;
};

type ApiErrorResponse = {
  message?: string;
  errors?: Record<string, string[]>;
};

const getErrorMessage = async (response: Response) => {
  const payload = (await response
    .json()
    .catch(() => null)) as ApiErrorResponse | null;

  const validationErrors = payload?.errors
    ? Object.values(payload.errors).flat().join(" ")
    : null;

  return (
    payload?.message ||
    validationErrors ||
    "Einladung konnte nicht angenommen werden."
  );
};

const acceptInvite = async (
  invitationId: string,
  { email, userId }: AcceptInviteOptions = {},
  token: string,
) => {
  const response = await fetch(
    `${API_BASE}/api/Invitation/${invitationId}/accept`,
    {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Authorization: `Bearer ${token}`,
      },
      body: JSON.stringify({ email, userId }),
    },
  );

  if (!response.ok) {
    throw new Error(await getErrorMessage(response));
  }

  return true;
};

export default acceptInvite;
