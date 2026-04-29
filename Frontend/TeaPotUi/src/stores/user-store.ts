import { createStore } from "zustand";
import type { User, Task } from "../util/types";
import { useStore } from "zustand";
import { persist } from "zustand/middleware";
import { defaultUser } from "../util/default-data";
import { getLegacyWorkSettings } from "../util/work-profile";
import {
  ensureUser,
  fetchTasks,
  createTask,
  updateTask,
  deleteTask,
} from "../util/task-api";
import { fetchWorkProfile } from "../util/work-profile-api";
import { fetchOrganizationsByUserEmail } from "../util/org-api";

type UserStore = {
  user: User;
  workProfileId: string | null;
};

const initialState: UserStore = {
  user: defaultUser,
  workProfileId: null,
};

const userStore = createStore<UserStore>()(
  persist(() => initialState, {
    name: "teapot-user-store",
  }),
);

const useUserStore = () => {
  /**
   * Called after Auth0 login. Registers the user in the backend (if new),
   * loads their persisted tasks, and sets up the store.
   */
  const initForUser = async (sub: string, email: string) => {
    const previousState = userStore.getState();

    try {
      const { userId, workProfileId } = await ensureUser(email);
      const [tasksResult, workProfileResult, organizationsResult] = await Promise.allSettled([
        fetchTasks(workProfileId),
        fetchWorkProfile(userId),
        fetchOrganizationsByUserEmail(email),
      ]);

      const tasks = tasksResult.status === "fulfilled" ? tasksResult.value : [];
      const workProfile =
        workProfileResult.status === "fulfilled" ? workProfileResult.value : null;
      const legacyWorkSettings = workProfile ? getLegacyWorkSettings(workProfile) : undefined;
      const orgs =
        organizationsResult.status === "fulfilled"
          ? organizationsResult.value
          : previousState.user.email === email
            ? previousState.user.orgs
            : [];

      userStore.setState({
        user: {
          ...defaultUser,
          id: userId,
          username: email.split("@")[0],
          email,
          tasks,
          workProfile: workProfile ?? undefined,
          hasPersistedWorkProfile: workProfile !== null,
          plannerViewStart: workProfile?.plannerViewStart,
          plannerViewEnd: workProfile?.plannerViewEnd,
          workCapacityHours: legacyWorkSettings?.workCapacityHours,
          workDays: legacyWorkSettings?.workDays,
          workStart: legacyWorkSettings?.workStart,
          workEnd: legacyWorkSettings?.workEnd,
          breakRules: legacyWorkSettings?.breakRules,
          orgs,
        },
        workProfileId,
      });
    } catch (err) {
      console.error("initForUser failed, keeping the last known user state when possible", err);
      if (
        previousState.user.email === email &&
        previousState.user.id !== defaultUser.id
      ) {
        return;
      }

      userStore.setState({
        user: {
          ...defaultUser,
          id: sub,
          username: email.split("@")[0],
          email,
          tasks: [],
        },
        workProfileId: null,
      });
    }
  };
  const state = useStore(userStore);

  const setUser = (newUser: User = defaultUser) => {
    userStore.setState({ user: newUser });
  };

  /** Persists a new task to the backend and adds it to the store. */
  const addTask = async (task: Task): Promise<Task> => {
    const { workProfileId } = userStore.getState();
    if (workProfileId) {
      const saved = await createTask(workProfileId, task);
      userStore.setState((s) => ({
        user: { ...s.user, tasks: [...(s.user.tasks ?? []), saved] },
      }));
      return saved;
    }
    // No backend connection – still update local state
    userStore.setState((s) => ({
      user: { ...s.user, tasks: [...(s.user.tasks ?? []), task] },
    }));
    return task;
  };

  /** Persists task changes to the backend and updates the store. */
  const saveTask = async (task: Task): Promise<void> => {
    const { workProfileId } = userStore.getState();
    const updateLocal = (updated: Task) =>
      userStore.setState((s) => ({
        user: {
          ...s.user,
          tasks: (s.user.tasks ?? []).map((t) =>
            t.id === updated.id ? updated : t,
          ),
        },
      }));

    if (workProfileId && task.id) {
      const saved = await updateTask(workProfileId, task.id, task);
      updateLocal(saved);
    } else if (task.id) {
      // Offline fallback: keep local state in sync
      updateLocal(task);
    }
  };

  /** Deletes a task from the backend and removes it from the store. */
  const removeTask = async (taskId: string): Promise<void> => {
    const { workProfileId } = userStore.getState();
    if (workProfileId) {
      await deleteTask(workProfileId, taskId);
    }
    userStore.setState((s) => ({
      user: {
        ...s.user,
        tasks: (s.user.tasks ?? []).filter((t) => t.id !== taskId),
      },
    }));
  };

  return { ...state, setUser, addTask, saveTask, removeTask, initForUser };
};

export default useUserStore;
