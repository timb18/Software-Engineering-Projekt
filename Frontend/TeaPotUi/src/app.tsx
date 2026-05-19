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
  const {
    isAuthenticated,
    isLoading,
    user: authUser,
    getAccessTokenSilently,
  } = useAuth0();
  const navigate = useNavigate();
  const initialized = useRef(false);
  const acceptedInvitation = useRef<string | null>(null);
  const location = useLocation();

  useEffect(() => {
    // If the user arrived through an invitation link, persist it before any redirect happens.
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
    const initUser = async (
      sub: string,
      email: string,
      username?: string,
      profilePicture?: string,
    ) => {
      const token = await getAccessTokenSilently();
      initForUser(token, sub, email, username, profilePicture).catch(
        console.error,
      );
      initialized.current = true;
    };
    // Initialize the local store once after Auth0 has resolved the authenticated user.
    if (
      isAuthenticated &&
      authUser?.sub &&
      authUser?.email &&
      !initialized.current
    ) {
      initUser(authUser.sub, authUser.email, authUser.name, authUser.picture);
    }
  }, [
    isAuthenticated,
    authUser?.sub,
    authUser?.email,
    authUser?.name,
    authUser?.picture,
    getAccessTokenSilently,
  ]);

  useEffect(() => {
    // Accept a pending invitation automatically after login so the user lands in the team view.
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

    if (
      !pendingInvitation ||
      acceptedInvitation.current === pendingInvitation.invitationId
    ) {
      return;
    }

    acceptedInvitation.current = pendingInvitation.invitationId;

    const acceptPendingInvitation = async () => {
      try {
        const token = await getAccessTokenSilently();
        const { userId } = await initForUser(
          token,
          userSub,
          userEmail,
          userName,
          userPicture,
        );
        await acceptInvite(pendingInvitation.invitationId, { userId }, token);
        clearPendingInvitation();
        await initForUser(token, userSub, userEmail, userName, userPicture);
        navigate("/teams", { replace: true });
      } catch (error) {
        console.error("acceptInvite failed", error);
        acceptedInvitation.current = null;
        // Route back to the login page with a readable error message instead of dropping the context.
        const message =
          error instanceof Error
            ? error.message
            : "Einladung konnte nicht angenommen werden.";
        navigate(
          `/login?inviteStatus=error&message=${encodeURIComponent(message)}`,
          { replace: true },
        );
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
    getAccessTokenSilently,
  ]);

  return (
    <div className="flex h-screen w-full flex-col bg-linear-to-br from-slate-950 via-slate-900 to-slate-950 p-6 text-slate-50">
      <div className="grid min-h-0 w-full flex-1 grid-cols-[18.5rem_1fr] rounded-4xl border border-slate-800 bg-slate-900/60 shadow-2xl backdrop-blur">
        <Sidebar />
        <div className="h-full min-h-0 overflow-y-auto">
          <Outlet />
        </div>
      </div>
    </div>
  );
}

export default App;
