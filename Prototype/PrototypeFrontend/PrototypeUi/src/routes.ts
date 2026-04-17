import { createBrowserRouter } from "react-router";
import App from "./app";
import Login from "./components/login";
import Home from "./components/main-panel/home";
import User from "./components/main-panel/user";
import Orgs from "./components/main-panel/orgs";
import Tasks from "./components/main-panel/tasks";
import TaskList from "./components/main-panel/task-list";

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
        Component: Orgs,
      },
      {
        path: "planner",
        Component: Tasks,
      },
      {
        path: "tasks",
        Component: TaskList,
      },
    ],
  },
  { path: "/login", Component: Login },
]);

export default router;
