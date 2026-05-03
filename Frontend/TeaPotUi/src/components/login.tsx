import { useCallback, useEffect, type FC } from "react";
import { useNavigate, useSearchParams } from "react-router";
import { useAuth0 } from "@auth0/auth0-react";
import useUserStore from "../stores/user-store";
import acceptInvite from "../util/accept-invite";

const Login: FC = () => {
  const { loginWithPopup: login, isAuthenticated, user } = useAuth0();
  const { initForUser } = useUserStore();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const invitationId = searchParams.get("invitationId");

  const toLoginAsync = useCallback(async () => {
    if (!user) {
      return;
    }
    await initForUser(user.sub!, user.email!).catch(console.error);
    if (invitationId) {
      await acceptInvite(invitationId, { email: user.email! }).catch(
        console.error,
      );
    }
    navigate("/");
  }, [initForUser, invitationId, navigate, user]);

  useEffect(() => {
    if (!isAuthenticated) {
      return;
    }
    toLoginAsync();
  }, [initForUser, isAuthenticated, navigate, toLoginAsync, user]);

  return (
    <div className="flex h-screen w-full flex-col items-center justify-center">
      <div className="flex h-2/5 min-h-100 w-1/5 min-w-110 flex-col items-center gap-10 rounded-4xl bg-emerald-200 p-10">
        <h1 className="text-4xl font-bold">Welcome</h1>
        <div className="flex h-3/5 w-full flex-col items-center justify-center p-5">
          <button
            className="w-full rounded-2xl border bg-emerald-300 py-1 hover:bg-emerald-400"
            onClick={() => login()}
          >
            {invitationId
              ? "Login / Konto erstellen und Einladung annehmen"
              : "Login"}
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
