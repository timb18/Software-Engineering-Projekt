import { useCallback, useEffect, type FC } from "react";
import { useNavigate, useSearchParams } from "react-router";
import { useAuth0 } from "@auth0/auth0-react";
import { initForUser } from "../stores/user-store";
import acceptInvite from "../util/accept-invite";

const Login: FC = () => {
  const { loginWithPopup: login, isAuthenticated, user } = useAuth0();
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
    <div className="min-h-screen bg-[#070b14] text-slate-50">
      <div className="grid min-h-screen lg:grid-cols-[1.05fr_0.95fr]">
        <section className="relative hidden overflow-hidden border-r border-slate-800 bg-[#0b1220] lg:block">
          <div className="absolute inset-0 bg-[linear-gradient(rgba(148,163,184,0.08)_1px,transparent_1px),linear-gradient(90deg,rgba(148,163,184,0.08)_1px,transparent_1px)] bg-[size:44px_44px]" />
          <div className="relative flex h-full flex-col justify-between p-12">
            <div className="flex items-center gap-3">
              <div className="flex h-12 w-12 items-center justify-center rounded-lg border border-emerald-300/30 bg-slate-950/70">
                <img src="/logo_only_pot.png" alt="TeaPot" className="h-8 w-8 object-contain" />
              </div>
              <div>
                <div className="text-xl font-semibold tracking-tight">TeaPot</div>
                <div className="text-sm text-slate-400">Work planning</div>
              </div>
            </div>

            <div className="max-w-xl">
              <div className="mb-5 inline-flex rounded-lg border border-emerald-300/25 bg-emerald-400/10 px-3 py-1 text-sm font-medium text-emerald-100">
                {invitationId ? "Invitation ready" : "Welcome back"}
              </div>
              <h1 className="text-5xl font-semibold leading-tight tracking-tight text-white">
                Plan work where your team already is.
              </h1>
              <p className="mt-5 max-w-lg text-lg leading-8 text-slate-300">
                Tasks, organizations, and working hours stay together in one calm, focused workspace.
              </p>
            </div>

            <div className="grid max-w-xl grid-cols-3 gap-3 text-sm">
              {["Orgs", "Tasks", "Work profile"].map((item) => (
                <div key={item} className="rounded-lg border border-slate-800 bg-slate-950/55 p-4 text-slate-300">
                  <div className="h-1.5 w-8 rounded-full bg-emerald-300" />
                  <div className="mt-3 font-medium text-slate-100">{item}</div>
                </div>
              ))}
            </div>
          </div>
        </section>

        <main className="flex min-h-screen items-center justify-center px-6 py-10 sm:px-10">
          <div className="w-full max-w-md">
            <div className="mb-10 flex items-center gap-3 lg:hidden">
              <div className="flex h-11 w-11 items-center justify-center rounded-lg border border-emerald-300/30 bg-slate-900">
                <img src="/logo_only_pot.png" alt="TeaPot" className="h-7 w-7 object-contain" />
              </div>
              <div>
                <div className="text-lg font-semibold">TeaPot</div>
                <div className="text-sm text-slate-400">Work planning</div>
              </div>
            </div>

            <div className="rounded-lg border border-slate-800 bg-slate-900/80 p-7 shadow-2xl shadow-black/40 backdrop-blur">
              <div className="mb-8">
                <p className="text-sm font-medium uppercase tracking-[0.18em] text-emerald-300">
                  Sign in
                </p>
                <h2 className="mt-3 text-3xl font-semibold tracking-tight">
                  {invitationId ? "Accept invitation" : "Log in"}
                </h2>
                <p className="mt-3 text-sm leading-6 text-slate-400">
                  {invitationId
                    ? "Log in or create an account to join the organization."
                    : "Log in to open your workspace."}
                </p>
              </div>

              <div className="flex flex-col gap-3">
                <button
                  className="flex min-h-12 w-full items-center justify-center rounded-lg border border-emerald-300/50 bg-emerald-400 px-4 py-3 text-sm font-semibold text-slate-950 transition hover:bg-emerald-300"
                  onClick={() => login()}
                >
                  {invitationId ? "Log in and accept invitation" : "Log in"}
                </button>

                <button
                  className="flex min-h-12 w-full items-center justify-center rounded-lg border border-slate-700 bg-slate-950/50 px-4 py-3 text-sm font-semibold text-slate-100 transition hover:border-emerald-300/50 hover:bg-slate-950"
                  onClick={() =>
                    login({ authorizationParams: { screen_hint: "signup" } })
                  }
                >
                  Create account
                </button>
              </div>

              <div className="mt-7 border-t border-slate-800 pt-5 text-xs leading-5 text-slate-500">
                Authentication is handled securely through Auth0.
              </div>
            </div>
          </div>
        </main>
      </div>
    </div>
  );
};

export default Login;
