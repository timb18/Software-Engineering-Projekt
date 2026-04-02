import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import "./index.css";
import { RouterProvider } from "react-router";
import router from "./routes.ts";

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <div className="min-h-screen w-screen bg-slate-950">
      <RouterProvider router={router} />
    </div>
  </StrictMode>,
);
