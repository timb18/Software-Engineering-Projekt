import { useEffect, type FC } from "react";
import { useNavigate } from "react-router";
import useLoginStore from "../../stores/login-store";
import useUserStore from "../../stores/user-store";

const User: FC = () => {
  const { logout } = useLoginStore();
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

  const logOut = () => {
    logout();
    navigate("/login");
  };

  return (
    <div className="grid h-full w-full grid-rows-[3rem_1fr] gap-8 p-5">
      <h1 className="w-full text-left text-5xl font-bold">My Profile</h1>
      <div className="flex w-full flex-col items-start gap-5 p-5">
        <div className="aspect-square w-30 rounded-full bg-pink-400"></div>
        <div className="flex flex-row gap-x-2">
          <div>Name:</div>
          <div>admin</div>
        </div>
        <div className="flex flex-row gap-x-2">
          <div>email:</div>
          <div>{user.email}</div>
        </div>
        <button onClick={logOut} className="cursor-pointer">
          Logout
        </button>
      </div>
    </div>
  );
};

export default User;
