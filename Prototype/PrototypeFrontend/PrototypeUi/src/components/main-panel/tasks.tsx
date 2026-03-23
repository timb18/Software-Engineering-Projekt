import dayjs from "dayjs";
import type { FC } from "react";

const weekDays = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];

const Tasks: FC = () => {
  const date = new Date();
  const currentDay = date.getDay() + 1;
  const weekStart = dayjs().startOf("day").subtract(currentDay, "day");
  const weekEnd = weekStart.add(6, "day");

  return (
    <div className="grid h-full w-full grid-rows-[3rem_1fr] gap-8 p-5">
      <h1 className="w-full text-left text-5xl font-bold">My Tasks</h1>
      <div className="flex h-full w-full flex-col gap-5">
        <div className="flex h-8 flex-row gap-5 text-2xl">
          <button className="cursor-pointer text-black disabled:cursor-default disabled:text-neutral-600">
            Day
          </button>
          <button
            disabled={true}
            className="cursor-pointer text-black disabled:cursor-default disabled:text-neutral-600"
          >
            Week
          </button>
          <button className="cursor-pointer text-black disabled:cursor-default disabled:text-neutral-600">
            Month
          </button>
        </div>
        <div className="flex w-full flex-col gap-5">
          <div>
            week from {weekStart.format("DD.MM.YYYY")} to{" "}
            {weekEnd.format("DD.MM.YYYY")}
          </div>
          <div className="h-full w-full overflow-x-clip overflow-y-auto">
            <div className="grid h-full grid-cols-[4rem_repeat(7,1fr)]">
              {weekDays.map((day, i) => (
                <div
                  key={day}
                  style={{ gridColumn: i + 2 }}
                  className="border-b border-l text-center"
                >
                  {day}
                </div>
              ))}
              {Array.from({ length: 56 }).map((_, i) => {
                const time = weekStart.add(15 * i, "minute").format("HH:mm");
                return (
                  <div
                    key={time}
                    style={{ gridRow: i + 2 }}
                    className="border-t border-r"
                  >
                    {time}
                  </div>
                );
              })}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};

export default Tasks;
