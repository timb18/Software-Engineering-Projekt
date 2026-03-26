import { createBrowserRouter } from "react-router";
import App from "./app";
import Login from "./components/login";
import Home from "./components/main-panel/home";
import User from "./components/main-panel/user";
import Teams from "./components/main-panel/teams";
import Tasks from "./components/main-panel/tasks";

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
        path: "teams",
        Component: Teams,
      },
      {
        path: "tasks",
        Component: Tasks
      }
    ],
  },
  { path: "/login", Component: Login },
]);

export default router;
