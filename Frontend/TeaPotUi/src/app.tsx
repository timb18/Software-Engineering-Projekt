import { Outlet, useLocation, useNavigate } from "react-router";
import Sidebar from "./components/sidebar";
import { useEffect, useRef } from "react";
import { useAuth0 } from "@auth0/auth0-react";
import { initForUser } from "./stores/user-store";

function App() {
  const { isAuthenticated, user: authUser } = useAuth0();
  const navigate = useNavigate();
  const initialized = useRef(false);
  const location = useLocation();

  useEffect(() => {
    if (!isAuthenticated) {
      navigate(`/login${location.search}`);
    }
  }, [isAuthenticated, location.search, navigate]);

  useEffect(() => {
    if (isAuthenticated && authUser?.sub && authUser?.email && !initialized.current) {
      initialized.current = true;
      initForUser(authUser.sub, authUser.email, authUser.name, authUser.picture).catch(console.error);
    }
  }, [isAuthenticated, authUser?.sub, authUser?.email, authUser?.name, authUser?.picture]);

  return (
    <div className="flex h-screen w-full flex-col bg-linear-to-br from-slate-950 via-slate-900 to-slate-950 p-6 text-slate-50">
      <div className="grid min-h-0 flex-1 w-full grid-cols-[18.5rem_1fr] rounded-4xl border border-slate-800 bg-slate-900/60 shadow-2xl backdrop-blur">
        <Sidebar />
        <div className="min-h-0 h-full overflow-y-auto">
          <Outlet />
        </div>
      </div>
    </div>
  );
}

export default App;
