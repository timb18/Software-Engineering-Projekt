import { createStore } from "zustand";
import type { User } from "../util/types";

type UserStore = {
  user?: User;
};

const userStore = createStore<UserStore>(() => ({}));

const useUserStore = () => {
  const state = userStore.getState();

  const setUser = (newUser: User) => {
    userStore.setState({ user: newUser });
  };

  return { ...state, setUser };
};

export default useUserStore;
