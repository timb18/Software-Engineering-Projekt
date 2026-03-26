import { useEffect, type FC } from "react";
import { useNavigate } from "react-router";
import useUserStore from "../stores/user-store";

const Sidebar: FC = () => {
  const { user } = useUserStore();
  const navigate = useNavigate();

  useEffect(() => {
    if (!user) {
      navigate("/login");
    }
  }, [navigate, user]);

  if (!user) {
    return <></>;
  }

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
        <div className="text-4xl font-bold">{user.username}</div>
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
