import { createBrowserRouter } from "react-router";
import App from "./app";
import Login from "./components/login";
import Home from "./components/main-panel/home";
import User from "./components/main-panel/user";
import Team from "./components/main-panel/team";

const router = createBrowserRouter([
  {
    path: "/",
    Component: App,
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
        path: "team",
        Component: Team,
      },
    ],
  },
  { path: "/login", Component: Login },
]);

export default router;
