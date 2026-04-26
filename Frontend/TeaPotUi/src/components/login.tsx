import { useEffect, useState, type FC } from "react";
import { useForm, type SubmitHandler } from "react-hook-form";
import useLoginStore from "../stores/login-store";
import { useNavigate, useSearchParams } from "react-router";
import { useEffect, type FC } from "react";
import { useAuth0 } from "@auth0/auth0-react";
import { useNavigate } from "react-router";

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
  const { loginWithPopup: login, isAuthenticated } = useAuth0();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const invitationId = searchParams.get("invitationId");
  const invitedEmail = searchParams.get("email");
  const inviteStatus = searchParams.get("inviteStatus");
  const inviteMessage = searchParams.get("message");

  useEffect(() => {
    if (isAuthenticated) {
      navigate("/");
    }
  }, [isAuthenticated, navigate]);

  return (
    <div className="flex h-screen w-full flex-col items-center justify-center">
      <div className="flex h-2/5 min-h-100 w-1/5 min-w-110 flex-col items-center gap-10 rounded-4xl bg-emerald-200 p-10">
        <h1 className="text-4xl font-bold">Welcome</h1>
        <div className="flex h-3/5 w-full flex-col items-center justify-center p-5">
          <button
            className="w-full rounded-2xl border bg-emerald-300 py-1 hover:bg-emerald-400"
            onClick={() => login()}
          >
            {invitationId ? "Login / Konto erstellen und Einladung annehmen" : "Login"}
          </button>
          <div className="my-2 h-0.5 w-full rounded-full bg-neutral-700"></div>
          <button
            className="w-full rounded-2xl border bg-emerald-300 py-1 hover:bg-emerald-400"
            onClick={() =>
              login({ authorizationParams: { screen_hint: "signup" } })
            }
          >
            Signup
          </button>
        </div>
      </div>
    </div>
  );
};

export default Login;
