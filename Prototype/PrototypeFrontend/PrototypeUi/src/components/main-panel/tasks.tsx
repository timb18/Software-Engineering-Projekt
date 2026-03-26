import dayjs from "dayjs";
import type { FC } from "react";

const weekDays = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];

const Tasks: FC = () => {
  const date = new Date();
  const currentDay = date.getDay() + 1;
  const weekStart = dayjs().startOf("day").subtract(currentDay, "day");
  const weekEnd = weekStart.add(6, "day");

  const timeIntervalCount = 24 * 4; // amount of 15 min intervals in a day

  return (
    <div className="grid w-full grid-rows-[3rem_1fr] gap-8 p-5">
      <h1 className="w-full text-left text-5xl font-bold">My Tasks</h1>
      <div className="grid grid-rows-[2rem_1rem_1fr] gap-5">
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
        <div>
          week from {weekStart.format("DD.MM.YYYY")} to{" "}
          {weekEnd.format("DD.MM.YYYY")}
        </div>
        <div className="overflow-x-hidden overflow-y-auto">
          <div className="h-[70vh] grid auto-rows-[2rem] grid-cols-[4rem_repeat(7,1fr)]">
            {weekDays.map((day, i) => (
              <div
                key={day}
                style={{ gridColumn: i + 2 }}
                className="border-b border-l text-center"
              >
                {day}
              </div>
            ))}
            {Array.from({ length: timeIntervalCount }).map((_, i) => {
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
  );
};

export default Tasks;
