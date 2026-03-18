import { createBrowserRouter } from "react-router";
import App from "./app";
import Login from "./components/login";
import Home from "./components/main-panel/home";

const router = createBrowserRouter([
  { path: "/", Component: App, children: [{
    index: true, Component: Home
  }] },
  { path: "/login", Component: Login },
]);

export default router;
