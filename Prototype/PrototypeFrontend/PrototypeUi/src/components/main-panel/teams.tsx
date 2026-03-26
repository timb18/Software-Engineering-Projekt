import { useEffect, type FC } from "react";
import useUserStore from "../../stores/user-store";
import { useNavigate } from "react-router";

const Teams: FC = () => {
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

  return (
    <div className="grid h-full w-full grid-rows-[3rem_1fr] gap-8 p-5">
      <h1 className="w-full text-left text-5xl font-bold">My Teams</h1>
      <div className="flex h-full w-full flex-col gap-3">
        <div className="w-full">
          <h2 className="text-2xl font-bold">Pending invites:</h2>
          <div>no pending invites</div>
        </div>
        <div className="w-full">
          <h2 className="overflow-x-hidden overflow-y-auto text-2xl font-bold">
            Teams
          </h2>
          {user.teams.length === 0 ? (
            <div>Not in any Teams</div>
          ) : (
            <div className="grid auto-rows-[4rem] grid-cols-1">
              {user.teams.map((team) => (
                <div key={team.name} className="rounded-2xl bg-emerald-300 p-2 grid grid-rows-2">
                  <div className="font-bold">{team.name}</div>
                  <div className="flex flex-row gap-2">
                    {team.users.map((teamUser) => {
                      if (teamUser.username === user.username) {
                        return (<div key={teamUser.username}>Me</div>);
                      }
                      return (
                        <div key={teamUser.username}>{teamUser.username}</div>
                      );
                    })}
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
        <button className="w-full cursor-pointer text-left underline">Join a team</button>
      </div>
    </div>
  );
};

export default Teams;
