import { useMemo, type FC } from "react";
import { useLocation, useNavigate } from "react-router";
import { useAuth0 } from "@auth0/auth0-react";

const Sidebar: FC = () => {
  const { user } = useAuth0();
  const navigate = useNavigate();
  const { pathname } = useLocation();

  const isActive = (path: string) =>
    path === "/" ? pathname === "/" : pathname.startsWith(path);

  const navButtonClass = (active: boolean) =>
    `w-full cursor-pointer rounded-xl border px-4 py-3 text-left transition ${
      active
        ? "border-emerald-300/70 bg-emerald-400/15 text-emerald-100 shadow-[0_0_0_1px_rgba(52,211,153,0.18),0_14px_34px_rgba(16,185,129,0.12)]"
        : "border-slate-800 bg-slate-900/70 text-slate-200 hover:border-emerald-300/50 hover:bg-emerald-400/5 hover:text-emerald-100"
    }`;

  const goToProfile = () => {
    navigate("/user");
  };

  const goToSettings = () => {
    navigate("/settings");
  };

  const goToHome = () => {
    navigate("/");
  };

  const goToMyOrgs = () => {
    navigate("/teams");
  };

  const goToPlanner = () => {
    navigate("/planner");
  };

  const goToTaskBoard = () => {
    navigate("/tasks");
  };

  const avatarStyle = useMemo(() => {
    if (user?.profileImage?.startsWith("http")) {
      return {
        backgroundImage: `url(${user.profileImage})`,
        backgroundSize: "cover",
        backgroundPosition: "center",
      };
    }
    const gradients: Record<string, string> = {
      "gradient-1": "linear-gradient(135deg, #34d399, #2563eb)",
      "gradient-2": "linear-gradient(135deg, #ec4899, #8b5cf6)",
      "gradient-3": "linear-gradient(135deg, #f59e0b, #ef4444)",
    };
    return { backgroundImage: gradients[user?.profileImage ?? "gradient-1"] };
  }, [user?.profileImage]);

  return (
    <aside className="flex h-full flex-col gap-6 rounded-l-4xl border-r border-slate-800 bg-slate-900/70 p-6">
      <button
        onClick={goToProfile}
        className={`w-full cursor-pointer rounded-2xl border p-4 text-left shadow-sm transition ${
          isActive("/user")
            ? "border-emerald-300/70 bg-emerald-400/15 shadow-[0_0_0_1px_rgba(52,211,153,0.18),0_14px_34px_rgba(16,185,129,0.12)]"
            : "border-slate-800 bg-slate-900/80 hover:border-emerald-300/50 hover:bg-emerald-400/5"
        }`}
      >
        <div className="flex items-center gap-3">
          <div
            className="aspect-square h-12 w-12 rounded-full border border-slate-700"
            style={avatarStyle}
          ></div>
          <div className="flex flex-col">
            <div className="text-xs tracking-[0.16em] text-slate-400 uppercase">
              Signed in
            </div>
            <div className="text-lg leading-tight font-bold text-emerald-100">
              {user?.nickname}
            </div>
            <div className="text-[11px] text-slate-500">{user?.email}</div>
          </div>
        </div>
      </button>

      <div className="flex flex-col gap-3 text-sm font-semibold">
        <button onClick={goToHome} className={navButtonClass(isActive("/"))}>
          Overview
        </button>
        <button
          onClick={goToMyOrgs}
          className={navButtonClass(isActive("/teams"))}
        >
          My Orgs
        </button>
        <button
          onClick={goToPlanner}
          className={navButtonClass(isActive("/planner"))}
        >
          Planner
        </button>
        <button
          onClick={goToTaskBoard}
          className={navButtonClass(isActive("/tasks"))}
        >
          Tasks
        </button>
        <button
          onClick={goToSettings}
          className={navButtonClass(isActive("/settings"))}
        >
          Settings
        </button>
      </div>
    </aside>
  );
};

export default Sidebar;
