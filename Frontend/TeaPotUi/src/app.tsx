import { Outlet, useNavigate } from "react-router";
import Sidebar from "./components/sidebar";
import { useEffect } from "react";
import { useAuth0 } from "@auth0/auth0-react";
import { initForUser } from "./stores/user-store";

function App() {
  const { isAuthenticated, user: authUser } = useAuth0();
  const navigate = useNavigate();

  useEffect(() => {
    if (!isAuthenticated) {
      navigate("/login");
    }
  }, [isAuthenticated, navigate]);

  useEffect(() => {
    if (isAuthenticated && authUser?.sub && authUser?.email) {
      initForUser(authUser.sub, authUser.email).catch(console.error);
    }
  }, [isAuthenticated, authUser?.sub, authUser?.email]);

  return (
    <div className="min-h-screen w-full bg-linear-to-br from-slate-950 via-slate-900 to-slate-950 p-6 text-slate-50">
      <div className="grid min-h-[80vh] w-full grid-cols-[16rem_1fr] rounded-4xl border border-slate-800 bg-slate-900/60 shadow-2xl backdrop-blur">
        <Sidebar />
        <div className="min-h-[80vh]">
          <Outlet />
        </div>
      </div>
    </div>
  );
}

export default App;
