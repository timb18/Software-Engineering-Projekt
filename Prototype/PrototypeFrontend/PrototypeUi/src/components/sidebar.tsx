import { useMemo, type FC } from "react";
import { useNavigate } from "react-router";
import useUserStore from "../stores/user-store";

const Sidebar: FC = () => {
  const { user } = useUserStore();
  const navigate = useNavigate();

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
    if (user.profileImage?.startsWith("http")) {
      return { backgroundImage: `url(${user.profileImage})`, backgroundSize: "cover" };
    }
    const gradients: Record<string, string> = {
      "gradient-1": "linear-gradient(135deg, #34d399, #2563eb)",
      "gradient-2": "linear-gradient(135deg, #ec4899, #8b5cf6)",
      "gradient-3": "linear-gradient(135deg, #f59e0b, #ef4444)",
    };
    return { backgroundImage: gradients[user.profileImage ?? "gradient-1"] };
  }, [user.profileImage]);

  return (
    <aside className="flex h-full flex-col gap-6 border-r rounded-l-4xl border-slate-800 bg-slate-900/70 p-6">
      <button
        onClick={goToProfile}
        className="w-full cursor-pointer rounded-2xl border border-slate-800 bg-slate-900/80 p-4 text-left shadow-sm transition hover:border-emerald-300/50 hover:bg-emerald-400/5"
      >
        <div className="flex items-center gap-3">
          <div
            className="h-12 w-12 rounded-full border border-slate-700"
            style={avatarStyle}
          ></div>
          <div className="flex flex-col">
            <div className="text-xs uppercase tracking-[0.16em] text-slate-400">Signed in</div>
            <div className="text-lg font-bold text-emerald-100 leading-tight">{user.username}</div>
            <div className="text-[11px] text-slate-500">{user.email}</div>
          </div>
        </div>
      </button>

      <div className="flex flex-col gap-3 text-sm font-semibold">
        <button
          onClick={goToHome}
          className="w-full cursor-pointer rounded-xl border border-slate-800 bg-slate-900/70 px-4 py-3 text-left text-slate-200 transition hover:border-emerald-300/50 hover:text-emerald-100"
        >
          Overview
        </button>
        <button
          onClick={goToMyOrgs}
          className="w-full cursor-pointer rounded-xl border border-slate-800 bg-slate-900/70 px-4 py-3 text-left text-slate-200 transition hover:border-emerald-300/50 hover:text-emerald-100"
        >
          My Orgs
        </button>
        <button
          onClick={goToPlanner}
          className="w-full cursor-pointer rounded-xl border border-slate-800 bg-slate-900/70 px-4 py-3 text-left text-slate-200 transition hover:border-emerald-300/50 hover:text-emerald-100"
        >
          Planner
        </button>
        <button
          onClick={goToTaskBoard}
          className="w-full cursor-pointer rounded-xl border border-slate-800 bg-slate-900/70 px-4 py-3 text-left text-slate-200 transition hover:border-emerald-300/50 hover:text-emerald-100"
        >
          Tasks
        </button>
        <button
          onClick={goToSettings}
          className="w-full cursor-pointer rounded-xl border border-slate-800 bg-slate-900/70 px-4 py-3 text-left text-slate-200 transition hover:border-emerald-300/50 hover:text-emerald-100"
        >
          Settings
        </button>
      </div>
    </aside>
  );
};

export default Sidebar;
