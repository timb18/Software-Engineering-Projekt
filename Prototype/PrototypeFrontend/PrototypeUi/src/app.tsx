import { Outlet, useNavigate } from "react-router";
import Sidebar from "./components/sidebar";
import useUserStore from "./stores/user-store";
import { useEffect } from "react";

function App() {
  const {user} = useUserStore();
  const navigate = useNavigate()

  useEffect(() => {
    if (!user) {
      navigate("/login")
    }
  }, [navigate, user])

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
