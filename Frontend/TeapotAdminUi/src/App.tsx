import { postApiOrganization, type CreateOrganizationRequest } from "./client";
import { client } from "./client/client.gen";
import { useAuth0 } from "@auth0/auth0-react";
import Login from "./login";
import { jwtDecode, type JwtPayload } from "jwt-decode";
import { useForm } from "react-hook-form";
import { useEffect, useState } from "react";

interface MyPayload extends JwtPayload {
  permissions: string[];
}

function App() {
  const {
    getAccessTokenSilently: getAccessToken,
    isAuthenticated,
    logout,
  } = useAuth0();
  const { handleSubmit, register } = useForm<CreateOrganizationRequest>({
    defaultValues: { maxUsers: 20 },
  });

  const [isAdmin, setIsAdmin] = useState<boolean>(false);

  client.interceptors.request.use(async (request) => {
    const token = await getAccessToken();
    request.headers.append("Authorization", `Bearer ${token}`);
    return request;
  });

  useEffect(() => {
    const extractAdminRolefromToken = async () => {
      const token = await getAccessToken();
      const decode: MyPayload = jwtDecode(token);
      setIsAdmin(decode.permissions.includes("write:orgs"));
    };

    extractAdminRolefromToken();
  }, [getAccessToken]);

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

  return (
    <div className="flex h-screen w-full flex-col items-center justify-center bg-linear-to-br from-slate-950 via-slate-900 to-slate-950 p-6 text-slate-50">
      {!isAuthenticated ? (
        <Login />
      ) : (
        <div className="grid h-9/10 w-9/10 grid-cols-2 grid-rows-1 gap-5 rounded-3xl bg-slate-800/50 p-5">
          {!isAdmin ? (
            <div className="text-5xl">Please log in as an admin</div>
          ) : (
            <div className="h-full w-full">
              <form
                onSubmit={handleSubmit(onCreateOrg)}
                className="flex w-full flex-col items-center gap-5 rounded-2xl p-5"
              >
                <div className="flex w-full flex-col rounded-2xl border p-2">
                  <div className="flex-col rounded-2xl">Name</div>
                  <input
                    className="rounded-xl border px-2"
                    type="text"
                    {...register("organizationName", { required: true })}
                  />
                </div>
                <div className="flex w-full flex-col rounded-2xl border p-2">
                  <div className="flex-col rounded-2xl">Description</div>
                  <input
                    className="rounded-xl border px-2"
                    type="text"
                    {...register("organizationDescription", { required: true })}
                  />
                </div>
                <div className="flex w-full flex-col rounded-2xl border p-2">
                  <div className="flex-col rounded-2xl">Max Users</div>
                  <input
                    className="rounded-xl border px-2"
                    type="number"
                    {...register("maxUsers", { required: true, min: 1 })}
                  />
                </div>
                <div className="flex w-full flex-col rounded-2xl border p-2">
                  <div className="flex-col rounded-2xl">Organizer e-mail</div>
                  <input
                    className="rounded-xl border px-2"
                    type="text"
                    {...register("organizerEmail", {
                      required: true,
                      pattern: /^[\w\-.]+@([\w-]+\.)+[\w-]{2,}$/,
                    })}
                  />
                </div>
                <button
                  className="w-fit cursor-pointer rounded-4xl border px-5 py-2 text-lg font-bold"
                  type="submit"
                >
                  Create Organization
                </button>
              </form>
            </div>
          )}
          <div>
            <button className="h-10 cursor-pointer" onClick={() => logout()}>
              logout
            </button>
          </div>
        </div>
      )}
    </div>
  );
}

export default App;
