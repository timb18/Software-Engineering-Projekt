import { createBrowserRouter } from "react-router";
import App from "./app";
import Login from "./components/login";
import Home from "./components/main-panel/home";
import User from "./components/main-panel/user";
import Teams from "./components/main-panel/teams";
import Tasks from "./components/main-panel/tasks";
import TaskBoard from "./components/main-panel/task-board";

const router = createBrowserRouter([
  {
    path: "/",
    Component: App,
    loader: () => {
    },
    children: [
      {
        index: true,
        Component: Home,
      },
      {
        path: "user",
        Component: User,
      },
      {
        path: "settings",
        Component: User,
      },
      {
        path: "teams",
        Component: Teams,
      },
      {
        path: "planner",
        Component: Tasks,
      },
      {
        path: "tasks",
        Component: TaskBoard,
      },
    ],
  },
  { path: "/login", Component: Login },
]);

export default router;
