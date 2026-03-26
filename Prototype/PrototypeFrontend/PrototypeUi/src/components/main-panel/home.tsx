import { useEffect, type FC } from "react";
import useUserStore from "../../stores/user-store";
import { useNavigate } from "react-router";

const Home: FC = () => {
  const { user } = useUserStore();
  const navigate = useNavigate();

  useEffect(() => {
    if (!user) {
      navigate("/login");
    }
  }, [navigate, user]);

  return (
    <div className="flex h-full w-full flex-col items-center pt-30">
      <div className="text-9xl font-bold">Wellcome {user?.username}!</div>
    </div>
  );
};

export default Home;
