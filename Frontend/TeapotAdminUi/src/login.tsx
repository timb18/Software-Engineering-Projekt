import { useAuth0 } from "@auth0/auth0-react";

// Minimal login screen shown before the user is authenticated.
const Login = () => {
  const { loginWithRedirect: login } = useAuth0();
  return (
    <div className="flex h-screen w-full flex-col items-center justify-center">
      <div className="flex h-2/5 min-h-100 w-1/5 min-w-110 flex-col items-center gap-10 rounded-4xl bg-emerald-200 p-10">
        <h1 className="text-4xl font-bold">Welcome</h1>
        <div className="flex h-3/5 w-full flex-col items-center justify-center p-5">
          <button
            className="h-10 w-full rounded-2xl border bg-emerald-300 py-1 hover:bg-emerald-400"
            onClick={() => login()}
          >
            Login
          </button>
        </div>
      </div>
    </div>
  );
};

export default Login;
