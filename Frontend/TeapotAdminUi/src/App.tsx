import { postApiOrganization, type CreateOrganizationRequest } from "./client";
import { client } from "./client/client.gen";
import { useAuth0 } from "@auth0/auth0-react";
import Login from "./login";
import { jwtDecode } from "jwt-decode";

function App() {
  const {
    getAccessTokenSilently: getAccessToken,
    isAuthenticated,
    logout,
  } = useAuth0();

  client.interceptors.request.use(async (request) => {
    const token = await getAccessToken();
    request.headers.append("Authorization", `Bearer ${token}`);
    return request;
  });

  const onCreateOrg = async (createOrgRequest: CreateOrganizationRequest) => {
    const { data, error } = await postApiOrganization({
      body: createOrgRequest,
    });

    if (!data) {
      alert(
        `There was an issue with creating the organization. Error: ${error}`,
      );
      return;
    }

    alert("Organization created succesfully");
  };

  const logClaims = async () => {
    const token = await getAccessToken();
    const decode: any = jwtDecode(token);
    console.log(decode.permissions[0] === "write:orgs");
    console.log(import.meta.env.VITE_AUTH0_AUDIENCE);
  };

  return (
    <div className="min-h-screen w-full bg-linear-to-br from-slate-950 via-slate-900 to-slate-950 p-6 text-slate-50">
      {!isAuthenticated ? (
        <Login />
      ) : (
        <div className="flex h-9/10 w-9/10 flex-col bg-emerald-50">
          <button className="h-10 w-40 text-black" onClick={logClaims}>
            claims
          </button>
          <button className="h-10 cursor-pointer" onClick={() => logout()}>
            logout
          </button>
        </div>
      )}
    </div>
  );
}

export default App;
