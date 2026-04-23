import { useEffect, type FC } from "react";
import { useAuth0 } from "@auth0/auth0-react";
import { useNavigate } from "react-router";

const Login: FC = () => {
  const { loginWithPopup: login, isAuthenticated } = useAuth0();
  const navigate = useNavigate();

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
            Login
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
