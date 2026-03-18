import type { FC } from "react";
import { useNavigate } from "react-router";

const Sidebar: FC = () => {
	let navigate = useNavigate();
  const goToLogin = () => {
    navigate("/login");
  };

  return (
    <div className="flex flex-col border-r-2 p-5 h-full gap-5">
      <button onClick={goToLogin} className="w-full hover:cursor-pointer">
        <div className="text-4xl font-bold">Username</div>
      </button>
      <div className="bg-black h-0.5 -mx-5"></div>
    </div>
  );
};

export default Sidebar;
