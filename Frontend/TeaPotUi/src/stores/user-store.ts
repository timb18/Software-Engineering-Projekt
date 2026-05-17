import { createStore } from "zustand";
import type { User, Task } from "../util/types";
import { useStore } from "zustand";
import { createJSONStorage, persist } from "zustand/middleware";
import { defaultUser } from "../util/default-data";
import { getLegacyWorkSettings } from "../util/work-profile";
import {
  fetchTasks,
  createTask,
  updateTask,
  deleteTask,
} from "../util/task-api";
import { fetchWorkProfile } from "../util/work-profile-api";
import { ensureUser, fetchUserProfile } from "../util/user-api";
import { fetchOrganizationsByUserEmail } from "../util/org-api";
import { applyStoredColorPreferences } from "../util/color-prefs";

type UserStore = {
  user: User;
  workProfileId: string | null;
  activeOrganizationId: string | null;
};

const initialState: UserStore = {
  user: defaultUser,
  workProfileId: null,
  activeOrganizationId: null,
};

const assignTasksToOrganization = (
  tasks: Task[],
  organizationId: string | null | undefined,
) =>
  organizationId
    ? tasks.map((task) => ({ ...task, org: organizationId }))
    : tasks;

const memoryStorage = {
  getItem: () => null,
  setItem: () => {},
  removeItem: () => {},
};

const userStore = createStore<UserStore>()(
  persist(() => initialState, {
    name: "teapot-user-store",
    storage: createJSONStorage(() => {
      const browserStorage =
        typeof window !== "undefined" ? window.localStorage : null;

      return browserStorage &&
        typeof browserStorage.setItem === "function" &&
        typeof browserStorage.getItem === "function"
        ? browserStorage
        : memoryStorage;
    }),
  }),
);

export const initForUser = async (
  sub: string,
  email: string,
  displayName?: string,
  profileImageUrl?: string,
) => {
  const previousState = userStore.getState();

  try {
    const { userId, workProfileId } = await ensureUser({
      email,
      authProviderSubject: sub,
      displayName,
      profileImageUrl,
    });
    const profile = await fetchUserProfile(userId);
    applyStoredColorPreferences(profile.breakColor, profile.orgColors);

    const [tasksResult, workProfileResult, organizationsResult] =
      await Promise.allSettled([
        workProfileId ? fetchTasks(workProfileId) : Promise.resolve([]),
        fetchWorkProfile(userId),
        fetchOrganizationsByUserEmail(email),
      ]);

    const initialTasks =
      tasksResult.status === "fulfilled" ? tasksResult.value : [];
    const workProfile =
      workProfileResult.status === "fulfilled" ? workProfileResult.value : null;
    const legacyWorkSettings = workProfile
      ? getLegacyWorkSettings(workProfile)
      : undefined;
    const orgs =
      organizationsResult.status === "fulfilled"
        ? organizationsResult.value
        : previousState.user.email === email
          ? previousState.user.orgs
          : [];
    const activeOrganization =
      orgs.find((org) => org.id === previousState.activeOrganizationId) ?? orgs[0] ?? null;
    const activeWorkProfileId = activeOrganization?.workProfileId ?? workProfileId ?? null;

    let tasks = assignTasksToOrganization(initialTasks, activeOrganization?.id);
    if (activeWorkProfileId && activeWorkProfileId !== workProfileId) {
      try {
        tasks = assignTasksToOrganization(
          await fetchTasks(activeWorkProfileId),
          activeOrganization?.id,
        );
      } catch (error) {
        console.error(
          "fetchTasks failed for active organization during initForUser",
          error,
        );
        tasks = [];
      }
    }

    if (tasksResult.status === "rejected") {
      console.error("fetchTasks failed during initForUser", tasksResult.reason);
    }
    if (workProfileResult.status === "rejected") {
      console.error(
        "fetchWorkProfile failed during initForUser",
        workProfileResult.reason,
      );
    }

    userStore.setState({
      user: {
        ...defaultUser,
        id: userId,
        email: profile.email,
        timezone: profile.timezone,
        appearanceBreakColor: profile.breakColor,
        appearanceOrgColors: profile.orgColors,
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
      workProfileId: activeWorkProfileId,
      activeOrganizationId: activeOrganization?.id ?? null,
    });

    return { userId, workProfileId: activeWorkProfileId };
  } catch (err) {
    console.error("initForUser failed, falling back to empty task list", err);
    const currentState = userStore.getState();
    const hasPersistedUser =
      currentState.user.id !== defaultUser.id &&
      (currentState.user.email === email || currentState.user.id === sub);

    if (hasPersistedUser) {
      return {
        userId: currentState.user.id,
        workProfileId: currentState.workProfileId,
      };
    }

    userStore.setState({
      user: {
        ...defaultUser,
        id: sub,
        email,
        tasks: [],
      },
      workProfileId: null,
      activeOrganizationId: null,
    });

    return { userId: sub, workProfileId: null };
  }
};

const useUserStore = () => {
  const state = useStore(userStore);

  const setUser = (newUser: User = defaultUser) => {
    const currentState = userStore.getState();
    const activeOrganization = newUser.orgs.find(
      (org) => org.id === currentState.activeOrganizationId,
    );

    userStore.setState({
      user: newUser,
      activeOrganizationId:
        newUser.id === defaultUser.id || newUser.orgs.length === 0
          ? null
          : (activeOrganization?.id ?? newUser.orgs[0]?.id ?? null),
      workProfileId:
        newUser.id === defaultUser.id || newUser.orgs.length === 0
          ? null
          : (activeOrganization?.workProfileId ??
            newUser.orgs[0]?.workProfileId ??
            currentState.workProfileId),
    });
  };

  const setActiveOrganization = async (organizationId: string | null) => {
    const state = userStore.getState();

    if (state.activeOrganizationId === organizationId) {
      return;
    }

    if (!organizationId) {
      userStore.setState({
        activeOrganizationId: null,
        workProfileId: null,
        user: { ...state.user, tasks: [] },
      });
      return;
    }

    const selectedOrganization = state.user.orgs.find(
      (org) => org.id === organizationId,
    );
    if (!selectedOrganization) {
      return;
    }

    if (!selectedOrganization.workProfileId) {
      userStore.setState({ activeOrganizationId: organizationId });
      return;
    }

    if (state.workProfileId === selectedOrganization.workProfileId) {
      userStore.setState({ activeOrganizationId: organizationId });
      return;
    }

    const tasks = assignTasksToOrganization(
      await fetchTasks(selectedOrganization.workProfileId),
      selectedOrganization.id,
    );
    userStore.setState({
      activeOrganizationId: organizationId,
      workProfileId: selectedOrganization.workProfileId,
      user: { ...state.user, tasks },
    });
  };

  /** Persists a new task to the backend and adds it to the store. */
  const addTask = async (task: Task): Promise<Task> => {
    const { workProfileId, activeOrganizationId } = userStore.getState();
    if (workProfileId) {
      const saved = await createTask(workProfileId, task);
      const taskForActiveOrganization = {
        ...saved,
        org: task.org || activeOrganizationId || saved.org,
      };
      userStore.setState((s) => ({
        user: {
          ...s.user,
          tasks: [...(s.user.tasks ?? []), taskForActiveOrganization],
        },
      }));
      return taskForActiveOrganization;
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
      updateLocal({ ...saved, org: task.org });
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

  return {
    ...state,
    setUser,
    setActiveOrganization,
    addTask,
    saveTask,
    removeTask,
  };
};

export default useUserStore;
