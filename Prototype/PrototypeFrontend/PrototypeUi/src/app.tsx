import { Outlet } from "react-router";
import Sidebar from "./components/sidebar";

function App() {
  return (
    <div className="h-full w-full p-5">
      <div className="grid grid-cols-[15rem_1fr] h-full rounded-4xl shadow-2xl bg-lime-600">
        <Sidebar />
        <Outlet />
      </div>
    </div>
  );
}

export default App;
