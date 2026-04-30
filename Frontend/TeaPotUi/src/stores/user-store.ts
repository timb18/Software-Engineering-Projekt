import { createStore } from "zustand";
import type { User, Task } from "../util/types";
import { useStore } from "zustand";
import { persist } from "zustand/middleware";
import { defaultUser } from "../util/default-data";
import { fetchTasks, createTask, updateTask, deleteTask } from "../util/task-api";
import { fetchWorkProfile } from "../util/work-profile-api";
import { ensureUser, fetchUserProfile } from "../util/user-api";

type UserStore = {
  user: User;
  workProfileId: string | null;
};

const userStore = createStore<UserStore>()(
  persist(
    (): UserStore => ({ user: defaultUser, workProfileId: null }),
    {
      name: "teapot-user-store",
    },
  ),
);

/**
 * Called after Auth0 login. Registers the user in the backend (if new),
 * loads their persisted tasks, and sets up the store.
 */
export const initForUser = async (
  sub: string,
  email: string,
  displayName?: string,
  profileImageUrl?: string,
) => {
  try {
    const { userId, workProfileId } = await ensureUser({
      email,
      authProviderSubject: sub,
    });
    const profile = await fetchUserProfile(userId);

    const [tasksResult, workProfileResult] = await Promise.allSettled([
      fetchTasks(workProfileId),
      fetchWorkProfile(userId),
    ]);

    const tasks = tasksResult.status === "fulfilled" ? tasksResult.value : [];
    const workProfile = workProfileResult.status === "fulfilled" ? workProfileResult.value : null;

    if (tasksResult.status === "rejected") {
      console.error("fetchTasks failed during initForUser", tasksResult.reason);
    }

    if (workProfileResult.status === "rejected") {
      console.error("fetchWorkProfile failed during initForUser", workProfileResult.reason);
    }

    userStore.setState({
      user: {
        ...defaultUser,
        id: userId,
        username: profile.username,
        displayName: profile.displayName,
        email: profile.email,
        profileImage: profile.profileImageUrl,
        timezone: profile.timezone,
        tasks,
        workProfile: workProfile ?? undefined,
      },
      workProfileId,
    });
  } catch (err) {
    console.error("initForUser failed, falling back to empty task list", err);
    const currentState = userStore.getState();
    const hasPersistedUser =
      currentState.user.id !== defaultUser.id &&
      (currentState.user.email === email || currentState.user.id === sub);

    userStore.setState({
      user: hasPersistedUser
        ? currentState.user
        : {
            ...defaultUser,
            id: sub,
            username: email.split("@")[0],
            displayName: displayName ?? email.split("@")[0],
            email,
            profileImage: profileImageUrl,
            tasks: [],
          },
      workProfileId: hasPersistedUser ? currentState.workProfileId : null,
    });
  }
};

const useUserStore = () => {
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
