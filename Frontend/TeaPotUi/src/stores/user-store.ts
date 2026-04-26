import { createStore } from "zustand";
import type { User, Task } from "../util/types";
import { useStore } from "zustand";
import { defaultUser } from "../util/default-data";
import { ensureUser, fetchTasks, createTask, updateTask, deleteTask } from "../util/task-api";

type UserStore = {
  user: User;
  workProfileId: string | null;
};

const userStore = createStore<UserStore>(() => ({ user: defaultUser, workProfileId: null }));

/**
 * Called after Auth0 login. Registers the user in the backend (if new),
 * loads their persisted tasks, and sets up the store.
 */
export const initForUser = async (sub: string, email: string) => {
  try {
    const { workProfileId } = await ensureUser(email);
    const tasks = await fetchTasks(workProfileId);
    userStore.setState({
      user: {
        ...defaultUser,
        id: sub,
        username: email.split("@")[0],
        email,
        tasks,
      },
      workProfileId,
    });
  } catch (err) {
    console.error("initForUser failed, falling back to empty task list", err);
    userStore.setState({
      user: { ...defaultUser, id: sub, username: email.split("@")[0], email, tasks: [] },
      workProfileId: null,
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
      userStore.setState((s) => ({ user: { ...s.user, tasks: [...(s.user.tasks ?? []), saved] } }));
      return saved;
    }
    // No backend connection – still update local state
    userStore.setState((s) => ({ user: { ...s.user, tasks: [...(s.user.tasks ?? []), task] } }));
    return task;
  };

  /** Persists task changes to the backend and updates the store. */
  const saveTask = async (task: Task): Promise<void> => {
    const { workProfileId } = userStore.getState();
    const updateLocal = (updated: Task) =>
      userStore.setState((s) => ({
        user: { ...s.user, tasks: (s.user.tasks ?? []).map((t) => (t.id === updated.id ? updated : t)) },
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
      user: { ...s.user, tasks: (s.user.tasks ?? []).filter((t) => t.id !== taskId) },
    }));
  };

  return { ...state, setUser, addTask, saveTask, removeTask };
};

export default useUserStore;
