import { createStore } from "zustand";
import type { User } from "../util/types";
import { useStore } from "zustand";

type UserStore = {
  user?: User;
};

const userStore = createStore<UserStore>(() => ({}));

const useUserStore = () => {
  const state = useStore(userStore);

  const setUser = (newUser?: User) => {
    userStore.setState({ user: newUser });
  };

  return { ...state, setUser };
};

export default useUserStore;
