import { createStore, useStore } from "zustand";
import { getDefaults } from "../util/default-data";
import useUserStore from "./user-store";
import type { User } from "../util/types";

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

  const ensureLocalAccount = (email: string) => {
    const users = getDefaults().users;
    const existingUser = users.find((u) => u.email === email);

    const user: User = existingUser ?? {
      id: crypto.randomUUID(),
      email,
      username: email.split("@")[0],
      displayName: email.split("@")[0],
      role: "user",
      orgs: [],
      tasks: [],
      invites: [],
      timezone: "Europe/Berlin",
      workCapacityHours: 8,
      workDays: ["Mon", "Tue", "Wed", "Thu", "Fri"],
      workStart: "09:00",
      workEnd: "17:00",
      breakRules: "30m lunch",
      notifications: { emailInvites: true, emailDeadlines: true },
    };

    loginStore.setState({ email, password: "" });
    setUser(user);
    return user;
  };

  const syncAccountFromBackend = (account: { id: string; email: string; username: string }) => {
    const users = getDefaults().users;
    const existingUser = users.find((u) => u.email === account.email);

    const user: User = existingUser
      ? { ...existingUser, id: account.id, username: account.username }
      : {
        id: account.id,
        email: account.email,
        username: account.username,
        displayName: account.username,
        role: "user",
        orgs: [],
        tasks: [],
        invites: [],
        timezone: "Europe/Berlin",
        workCapacityHours: 8,
        workDays: ["Mon", "Tue", "Wed", "Thu", "Fri"],
        workStart: "09:00",
        workEnd: "17:00",
        breakRules: "30m lunch",
        notifications: { emailInvites: true, emailDeadlines: true },
      };

    loginStore.setState({ email: account.email, password: "" });
    setUser(user);
    return user;
  };

  const logout = () => {
    loginStore.setState({ email: "", password: "" });
    setUser();
  };

  return { tryLogin, ensureLocalAccount, syncAccountFromBackend, logout, ...state };
};

export default useLoginStore;
