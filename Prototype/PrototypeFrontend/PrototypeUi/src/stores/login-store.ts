import { createStore, useStore } from "zustand";
import { getDefaults } from "../util/default-data";
import useUserStore from "./user-store";

type LoginStore = {
  email: string;
  password: string;
};

const loginStore = createStore<LoginStore>(() => ({
  email: "",
  password: "",
}));

const useLoginStore = () => {
  const state = useStore(loginStore);
  const { setUser } = useUserStore();

  const tryLogin = (email: string, password: string) => {
    const users = getDefaults().users;

    const user = users.find((u) => u.email === email);

    if (!user) return false;

    loginStore.setState({ email, password });
    setUser(user);

    return true;
  };

  const logout = () => {
    loginStore.setState({ email: "", password: "" });
  };

  return { tryLogin, logout, ...state };
};

export default useLoginStore;
