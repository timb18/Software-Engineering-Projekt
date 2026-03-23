import type { FC } from "react";
import { useNavigate } from "react-router";

const Sidebar: FC = () => {
  const navigate = useNavigate();
  const goToLogin = () => {
    navigate("/user");
  };

  const goToMyTeams = () => {
    navigate("/teams");
  };

  const goToMyTasks = () => {
    navigate("/tasks");
  };

  return (
    <div className="flex h-full flex-col gap-5 border-r-2 p-5">
      <button onClick={goToLogin} className="h-20 w-full cursor-pointer">
        <div className="text-4xl font-bold">Username</div>
      </button>
      <div className="-mx-5 h-0.5 bg-black"></div>
      <button onClick={goToMyTeams} className="cursor-pointer">
        <div className="text-4xl font-bold">MyTeams</div>
      </button>
      <div className="-mx-5 h-0.5 bg-black"></div>
      <button onClick={goToMyTasks} className="w-full cursor-pointer">
        <div className="text-4xl font-bold">MyTasks</div>
      </button>
      <div className="-mx-5 h-0.5 bg-black"></div>
    </div>
  );
};

export default Sidebar;
