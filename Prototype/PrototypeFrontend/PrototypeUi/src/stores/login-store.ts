import { createStore, useStore } from "zustand";

type LoginStore = {
  username: string;
  password: string;
};

const loginStore = createStore<LoginStore>(() => ({
  username: "",
  password: "",
}));

const useLoginStore = () => {
  const state = useStore(loginStore);

  const tryLogin = (username: string, password: string) => {

    

    return true;
  };
};

export default useLoginStore;
