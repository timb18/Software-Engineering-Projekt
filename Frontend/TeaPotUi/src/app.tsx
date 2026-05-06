import { Outlet, useLocation, useNavigate } from "react-router";
import Sidebar from "./components/sidebar";
import { useEffect, useRef } from "react";
import { useAuth0 } from "@auth0/auth0-react";
import { initForUser } from "./stores/user-store";
import acceptInvite from "./util/accept-invite";
import {
  clearPendingInvitation,
  getPendingInvitation,
  savePendingInvitation,
} from "./util/pending-invitation";

function App() {
  const { isAuthenticated, isLoading, user: authUser } = useAuth0();
  const navigate = useNavigate();
  const initialized = useRef(false);
  const acceptedInvitation = useRef<string | null>(null);
  const location = useLocation();

  useEffect(() => {
    const searchParams = new URLSearchParams(location.search);
    const invitationId = searchParams.get("invitationId");

    if (invitationId) {
      savePendingInvitation({
        invitationId,
        email: searchParams.get("email") ?? undefined,
      });
    }

    if (isLoading) {
      return;
    }

    if (!isAuthenticated) {
      navigate(`/login${location.search}`);
    }
  }, [isAuthenticated, isLoading, location.search, navigate]);

  useEffect(() => {
    if (isAuthenticated && authUser?.sub && authUser?.email && !initialized.current) {
      initialized.current = true;
      initForUser(authUser.sub, authUser.email, authUser.name, authUser.picture).catch(console.error);
    }
  }, [isAuthenticated, authUser?.sub, authUser?.email, authUser?.name, authUser?.picture]);

  useEffect(() => {
    if (!isAuthenticated || !authUser?.sub || !authUser.email) {
      return;
    }

    const userSub = authUser.sub;
    const userEmail = authUser.email;
    const userName = authUser.name;
    const userPicture = authUser.picture;
    const searchParams = new URLSearchParams(location.search);
    const invitationId = searchParams.get("invitationId");
    const pendingInvitation = invitationId
      ? { invitationId, email: searchParams.get("email") ?? undefined }
      : getPendingInvitation();

    if (!pendingInvitation || acceptedInvitation.current === pendingInvitation.invitationId) {
      return;
    }

    acceptedInvitation.current = pendingInvitation.invitationId;

    const acceptPendingInvitation = async () => {
      try {
        await initForUser(userSub, userEmail, userName, userPicture);
        await acceptInvite(pendingInvitation.invitationId, { email: userEmail });
        clearPendingInvitation();
        await initForUser(userSub, userEmail, userName, userPicture);
      } catch (error) {
        console.error("acceptInvite failed", error);
      } finally {
        navigate("/teams", { replace: true });
      }
    };

    void acceptPendingInvitation();
  }, [
    isAuthenticated,
    authUser?.sub,
    authUser?.email,
    authUser?.name,
    authUser?.picture,
    location.search,
    navigate,
  ]);

  return (
    <div className="flex h-screen w-full flex-col bg-linear-to-br from-slate-950 via-slate-900 to-slate-950 p-6 text-slate-50">
      <div className="grid min-h-0 flex-1 w-full grid-cols-[18.5rem_1fr] rounded-4xl border border-slate-800 bg-slate-900/60 shadow-2xl backdrop-blur">
        <Sidebar />
        <div className="min-h-0 h-full overflow-y-auto">
          <Outlet />
        </div>
      </div>
    </div>
  );
}

export default App;
