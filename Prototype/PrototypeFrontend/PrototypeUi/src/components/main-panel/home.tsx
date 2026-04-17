import dayjs from "dayjs";
import { type FC } from "react";
import useUserStore from "../../stores/user-store";

const Home: FC = () => {
  const { user } = useUserStore();

  if (!user) {
    return <></>;
  }

  const tasks = user.tasks ?? [];
  const now = dayjs();
  const upcomingTasks = tasks
    .filter((task) => dayjs(task.startDate).isAfter(now.subtract(1, "day")))
    .sort((a, b) => dayjs(a.startDate).valueOf() - dayjs(b.startDate).valueOf())
    .slice(0, 3);
  const upcomingDeadlines = tasks
    .map((task) => ({ task, deadline: dayjs(task.deadline ?? task.endDate) }))
    .filter(({ deadline, task }) => deadline.isAfter(now) && task.status !== "done")
    .sort((a, b) => a.deadline.valueOf() - b.deadline.valueOf())
    .slice(0, 3)
    .map(({ task }) => task);
  const teams = user.orgs ?? [];

  return (
    <div className="grid h-full w-full grid-rows-[auto_1fr] gap-6 bg-gradient-to-br from-slate-950 via-slate-900 to-slate-950 p-8 text-slate-50">
      <div className="rounded-3xl border border-slate-800 bg-slate-900/70 p-8 shadow-2xl backdrop-blur">
        <div className="text-sm uppercase tracking-[0.22em] text-emerald-300">Welcome back</div>
        <div className="mt-2 text-4xl font-semibold">Hi {user.username}, plan your week.</div>
        <div className="mt-2 text-slate-400">Keep tasks and teams aligned in one glance.</div>
      </div>

      <div className="grid grid-cols-3 gap-5 max-xl:grid-cols-2 max-md:grid-cols-1">
        <div className="rounded-2xl border border-slate-800 bg-slate-900/70 p-6 shadow-xl">
          <div className="text-xs uppercase tracking-[0.16em] text-slate-400">Teams</div>
          <div className="mt-2 text-3xl font-semibold text-emerald-100">{teams.length}</div>
          <div className="text-sm text-slate-400">Groups you collaborate with</div>
        </div>

        <div className="rounded-2xl border border-slate-800 bg-slate-900/70 p-6 shadow-xl">
          <div className="text-xs uppercase tracking-[0.16em] text-slate-400">Tasks this week</div>
          <div className="mt-2 text-3xl font-semibold text-emerald-100">{tasks.length}</div>
          <div className="text-sm text-slate-400">Scheduled items in your planner</div>
        </div>

        <div className="rounded-2xl border border-slate-800 bg-slate-900/70 p-6 shadow-xl">
          <div className="text-xs uppercase tracking-[0.16em] text-slate-400">Upcoming</div>
          <div className="mt-2 text-3xl font-semibold text-emerald-100">{upcomingTasks.length}</div>
          <div className="text-sm text-slate-400">Next tasks in the queue</div>
        </div>

        <div className="col-span-3 rounded-2xl border border-slate-800 bg-slate-900/70 p-6 shadow-xl max-xl:col-span-2 max-md:col-span-1">
          <div className="flex items-center justify-between">
            <div className="text-lg font-semibold text-slate-50">Next up</div>
          </div>
          <div className="mt-4 grid gap-3 max-md:grid-cols-1 md:grid-cols-3">
            {upcomingTasks.length === 0 && (
              <div className="col-span-3 rounded-xl border border-dashed border-slate-700 bg-slate-900/70 p-4 text-sm text-slate-400">
                Nothing queued. Head to Planner to schedule your day.
              </div>
            )}

            {upcomingTasks.map((task) => (
              <div
                key={`${task.name}-${task.startDate.toString()}`}
                className="rounded-xl border border-slate-800 bg-slate-900/80 p-4 shadow-sm"
              >
                <div className="text-xs uppercase tracking-[0.12em] text-emerald-200">
                  {dayjs(task.startDate).format("ddd, DD MMM")}
                </div>
                <div className="text-lg font-semibold text-slate-50">{task.name}</div>
                <div className="text-xs text-slate-300">
                  {dayjs(task.startDate).format("HH:mm")} - {dayjs(task.endDate).format("HH:mm")}
                </div>
                {task.description && (
                  <div className="mt-1 text-sm text-slate-400">{task.description}</div>
                )}
              </div>
            ))}
          </div>
        </div>

        <div className="col-span-3 rounded-2xl border border-slate-800 bg-slate-900/70 p-6 shadow-xl max-xl:col-span-2 max-md:col-span-1">
          <div className="flex items-center justify-between">
            <div className="text-lg font-semibold text-slate-50">Deadlines</div>
            <div className="text-xs text-slate-400">Next due items</div>
          </div>
          <div className="mt-4 grid gap-3 max-md:grid-cols-1 md:grid-cols-3">
            {upcomingDeadlines.length === 0 && (
              <div className="col-span-3 rounded-xl border border-dashed border-slate-700 bg-slate-900/70 p-4 text-sm text-slate-400">
                Keine anstehenden Deadlines. Plane neue Aufgaben im Planner.
              </div>
            )}

            {upcomingDeadlines.map((task) => (
              <div
                key={`${task.name}-${task.endDate.toString()}`}
                className="rounded-xl border border-rose-800/60 bg-rose-900/30 p-4 shadow-sm"
              >
                <div className="text-xs uppercase tracking-[0.12em] text-rose-200">
                  Fällig am {dayjs(task.deadline ?? task.endDate).format("ddd, DD MMM")}
                </div>
                <div className="text-lg font-semibold text-slate-50">{task.name}</div>
                <div className="text-xs text-slate-300">
                  {dayjs(task.deadline ?? task.endDate).format("HH:mm")}
                </div>
                {task.description && (
                  <div className="mt-1 text-sm text-slate-200">{task.description}</div>
                )}
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
};

export default Home;
