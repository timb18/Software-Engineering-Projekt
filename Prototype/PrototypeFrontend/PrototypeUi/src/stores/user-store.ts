import { createStore } from "zustand";
import type { User } from "../util/types";
import { useStore } from "zustand";
import { defaultUser } from "../util/default-data";

type UserStore = {
  user: User;
};

const userStore = createStore<UserStore>(() => ({user: defaultUser}));

const useUserStore = () => {
  const state = useStore(userStore);

  const setUser = (newUser: User = defaultUser) => {
    userStore.setState({ user: newUser });
  };

  return { ...state, setUser };
};

export default useUserStore;
