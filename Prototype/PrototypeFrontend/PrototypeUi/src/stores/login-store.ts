import { createStore, useStore } from "zustand";
import { users } from "../util/default-data";

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

  const tryLogin = (email: string, password: string) => {
    if (!users.some((u) => u.email === email)) {
      return false;
    }

    loginStore.setState({ email, password });

    return true;
  };

  const logout = () => {
    loginStore.setState({ email: "", password: "" });
  };

  return { tryLogin, logout, ...state };
};

export default useLoginStore;
