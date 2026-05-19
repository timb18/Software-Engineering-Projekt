import { createStore } from "zustand";
import type { User, Task } from "../util/types";
import { useStore } from "zustand";
import { createJSONStorage, persist } from "zustand/middleware";
import { defaultUser } from "../util/default-data";
import { getLegacyWorkSettings } from "../util/work-profile";
import { fetchTasks, createTask, updateTask, deleteTask } from "../util/task-api";
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

const assignTasksToOrganization = (tasks: Task[], organizationId: string | null | undefined) =>
  organizationId ? tasks.map((task) => ({ ...task, org: organizationId })) : tasks;

const memoryStorage = {
  getItem: () => null,
  setItem: () => {},
  removeItem: () => {},
};

const userStore = createStore<UserStore>()(
  persist(() => initialState, {
    name: "teapot-user-store",
    storage: createJSONStorage(() => {
      const browserStorage = typeof window !== "undefined" ? window.localStorage : null;

      return browserStorage && typeof browserStorage.setItem === "function" && typeof browserStorage.getItem === "function"
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
    applyStoredColorPreferences(profile.breakColor, profile.blockerColor, profile.orgColors);

    const [organizationsResult] = await Promise.allSettled([
      fetchOrganizationsByUserEmail(email),
    ]);

    const orgs =
      organizationsResult.status === "fulfilled"
        ? organizationsResult.value
        : previousState.user.email === email
          ? previousState.user.orgs
          : [];
    const activeOrganization =
      orgs.find((org) => org.id === previousState.activeOrganizationId) ?? orgs[0] ?? null;
    let activeWorkProfileId = activeOrganization
      ? activeOrganization.workProfileId ?? null
      : workProfileId ?? null;
    let workProfile = null;

    try {
      workProfile = (await fetchWorkProfile(userId, activeOrganization?.id ?? null)) ?? null;
      activeWorkProfileId = workProfile?.id ?? activeWorkProfileId;
    } catch (error) {
      console.error("fetchWorkProfile failed during initForUser", error);
    }

    const legacyWorkSettings = workProfile ? getLegacyWorkSettings(workProfile) : undefined;
    const orgsWithWorkProfile =
      activeOrganization && workProfile?.id
        ? orgs.map((org) =>
            org.id === activeOrganization.id
              ? { ...org, workProfileId: workProfile.id }
              : org,
          )
        : orgs;

    let tasks: Task[] = [];
    if (activeWorkProfileId) {
      try {
        tasks = assignTasksToOrganization(
          await fetchTasks(activeWorkProfileId),
          activeOrganization?.id,
        );
      } catch (error) {
        console.error("fetchTasks failed for active organization during initForUser", error);
        tasks = [];
      }
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
        appearanceBreakColor: profile.breakColor,
        appearanceBlockerColor: profile.blockerColor,
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
        orgs: orgsWithWorkProfile,
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
      return { userId: currentState.user.id, workProfileId: currentState.workProfileId };
    }

    userStore.setState({
      user: {
        ...defaultUser,
        id: sub,
        username: email.split("@")[0],
        displayName: displayName ?? email.split("@")[0],
        email,
        profileImage: profileImageUrl,
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
          : activeOrganization?.id ?? newUser.orgs[0]?.id ?? null,
      workProfileId:
        newUser.id === defaultUser.id || newUser.orgs.length === 0
          ? null
          : activeOrganization?.workProfileId ?? newUser.orgs[0]?.workProfileId ?? currentState.workProfileId,
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

    const selectedOrganization = state.user.orgs.find((org) => org.id === organizationId);
    if (!selectedOrganization) {
      return;
    }

    const workProfile = (await fetchWorkProfile(state.user.id, selectedOrganization.id)) ?? null;
    const selectedWorkProfileId = workProfile?.id ?? selectedOrganization.workProfileId ?? null;
    const tasks = selectedWorkProfileId
      ? assignTasksToOrganization(
          await fetchTasks(selectedWorkProfileId),
          selectedOrganization.id,
        )
      : [];
    const legacyWorkSettings = workProfile ? getLegacyWorkSettings(workProfile) : undefined;
    const orgs = workProfile?.id
      ? state.user.orgs.map((org) =>
          org.id === selectedOrganization.id ? { ...org, workProfileId: workProfile.id } : org,
        )
      : state.user.orgs;

    userStore.setState({
      activeOrganizationId: organizationId,
      workProfileId: selectedWorkProfileId,
      user: {
        ...state.user,
        tasks,
        orgs,
        workProfile: workProfile ?? undefined,
        hasPersistedWorkProfile: workProfile !== null,
        plannerViewStart: workProfile?.plannerViewStart,
        plannerViewEnd: workProfile?.plannerViewEnd,
        workCapacityHours: legacyWorkSettings?.workCapacityHours,
        workDays: legacyWorkSettings?.workDays,
        workStart: legacyWorkSettings?.workStart,
        workEnd: legacyWorkSettings?.workEnd,
        breakRules: legacyWorkSettings?.breakRules,
      },
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
        user: { ...s.user, tasks: [...(s.user.tasks ?? []), taskForActiveOrganization] },
      }));
      return taskForActiveOrganization;
    }
    // No backend connection is available, so keep the local store in sync anyway.
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
      // Offline fallback: keep local state in sync.
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

  return { ...state, setUser, setActiveOrganization, addTask, saveTask, removeTask };
};

export default useUserStore;
