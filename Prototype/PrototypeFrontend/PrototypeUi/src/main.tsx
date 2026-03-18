import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import "./index.css";
import { RouterProvider } from "react-router";
import router from "./routes.ts";

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <div className="h-screen w-screen bg-mist-900">
      <RouterProvider router={router} />
    </div>
  </StrictMode>,
);
