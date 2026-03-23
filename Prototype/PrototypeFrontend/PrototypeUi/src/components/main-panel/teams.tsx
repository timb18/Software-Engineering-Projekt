import type { FC } from "react";

const Teams: FC = () => {
  return (
    <div className="grid h-full w-full grid-rows-[3rem_1fr] gap-8 p-5">
      <h1 className="w-full text-left text-5xl font-bold">My Teams</h1>
      <div className="flex h-full w-full flex-col gap-3">
        <div className="w-full">
          <h2 className="text-2xl font-bold overflow-y-auto overflow-x-hidden">Pending invites:</h2>
          <div>no pending invites</div>
        </div>
        <div className="w-full">
          <h2 className="text-2xl font-bold overflow-y-auto overflow-x-hidden">Teams</h2>
          <div>Not in any Teams</div>
        </div>
        <button className="w-full text-left cursor-pointer">Join a team</button>
      </div>
    </div>
  );
};

export default Teams;
