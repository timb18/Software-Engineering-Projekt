import { useEffect, useState, type FC } from "react";
import { useForm, type SubmitHandler } from "react-hook-form";
import useLoginStore from "../stores/login-store";
import { useNavigate, useSearchParams } from "react-router";

type Login = {
  email: string;
  password: string;
};

const Login: FC = () => {
  const [showWrongPassword, setShowWrongPassword] = useState(false);
  const [inviteError, setInviteError] = useState<string | null>(null);
  const [isFinishingInvite, setIsFinishingInvite] = useState(false);
  const {
    register,
    formState: { errors },
    handleSubmit,
    setValue,
  } = useForm<Login>();
  const { tryLogin, ensureLocalAccount, syncAccountFromBackend } = useLoginStore();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const invitationId = searchParams.get("invitationId");
  const invitedEmail = searchParams.get("email");
  const inviteStatus = searchParams.get("inviteStatus");
  const inviteMessage = searchParams.get("message");

  useEffect(() => {
    if (invitedEmail) {
      setValue("email", invitedEmail);
    }
  }, [invitedEmail, setValue]);

  const onLogin: SubmitHandler<Login> = async (data) => {
    const normalizedEmail = data.email.trim().toLowerCase();
    const normalizedInvitedEmail = invitedEmail?.trim().toLowerCase();

    setInviteError(null);

    if (invitationId && normalizedInvitedEmail && normalizedEmail !== normalizedInvitedEmail) {
      setInviteError("Bitte melde dich mit der eingeladenen E-Mail-Adresse an.");
      return;
    }

    const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "";
    let loggedIn = tryLogin(normalizedEmail, data.password);
    if (!loggedIn && invitationId && normalizedInvitedEmail === normalizedEmail) {
      try {
        const registerResponse = await fetch(`${apiBaseUrl}/api/Auth/register`, {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
          },
          body: JSON.stringify({
            email: normalizedEmail,
            username: normalizedEmail.split("@")[0],
          }),
        });

        if (!registerResponse.ok) {
          const payload = await registerResponse.json().catch(() => null);
          throw new Error(payload?.message ?? "Konto konnte nicht erstellt werden.");
        }

        const registerPayload = await registerResponse.json();
        const account = registerPayload?.data;

        if (account?.id && account?.email && account?.username) {
          syncAccountFromBackend(account);
        } else {
          ensureLocalAccount(normalizedEmail);
        }

        loggedIn = true;
      } catch (error) {
        setInviteError(error instanceof Error ? error.message : "Konto konnte nicht erstellt werden.");
        return;
      }
    }

    if (!loggedIn) {
      setShowWrongPassword(true);
      return;
    }

    if (invitationId) {
      setIsFinishingInvite(true);
      try {
        const response = await fetch(`${apiBaseUrl}/api/Invitation/${invitationId}/accept`, {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
          },
          body: JSON.stringify({
            email: normalizedEmail,
          }),
        });

        if (!response.ok) {
          const payload = await response.json().catch(() => null);
          throw new Error(payload?.message ?? "Einladung konnte nicht angenommen werden.");
        }

        navigate("/teams?inviteStatus=accepted");
        return;
      } catch (error) {
        setInviteError(error instanceof Error ? error.message : "Einladung konnte nicht angenommen werden.");
        setIsFinishingInvite(false);
        return;
      }
    }

    navigate("/");
  };

  return (
    <div className="flex h-full w-full flex-col items-center justify-center">
      <div className="flex h-1/2 min-h-100 w-1/5 min-w-110 flex-col items-center gap-10 rounded-4xl bg-emerald-200 p-10">
        <h1 className="text-4xl font-bold">Welcome</h1>
        {inviteStatus === "pending" && invitedEmail && (
          <div className="rounded-2xl bg-white px-4 py-3 text-center text-sm text-slate-700">
            Einladung erkannt fuer <strong>{invitedEmail}</strong>. Bitte melde dich an oder erstelle hier ein lokales Konto,
            dann trittst du der Organisation bei.
          </div>
        )}
        {inviteStatus === "error" && inviteMessage && (
          <div className="rounded-2xl bg-rose-100 px-4 py-3 text-center text-sm text-rose-700">{inviteMessage}</div>
        )}
        <form
          className="flex flex-col items-center gap-2"
          onSubmit={handleSubmit(onLogin)}
        >
          {showWrongPassword && (
            <div className="text-red-600">Incorrect password or email</div>
          )}
          {inviteError && (
            <div className="text-red-600">{inviteError}</div>
          )}
          <div className="flex flex-col gap-1">
            <label htmlFor="email">Email</label>
            <input
              id="email"
              className="bg-white px-2"
              placeholder="email"
              type="email"
              {...register("email", {
                required: true,
                pattern: /^[\w\-.]+@([\w-]+\.)+[\w-]{2,}$/,
              })}
              aria-invalid={errors.email ? "true" : "false"}
            />
            {errors.email && <div>incorrect email format</div>}
          </div>
          <div className="flex flex-col gap-1">
            <label htmlFor="password">Password</label>
            <input
              id="password"
              className="bg-white px-2"
              placeholder="password"
              type="password"
              {...register("password", { required: true })}
            />
          </div>
          <button
            type="submit"
            disabled={isFinishingInvite}
            className="cursor-pointer rounded-2xl disabled:cursor-default bg-emerald-300 hover:bg-emerald-400 px-5 text-xl"
          >
            {invitationId ? "Login / Konto erstellen und Einladung annehmen" : "Login"}
          </button>
        </form>
      </div>
    </div>
  );
};

export default Login;
