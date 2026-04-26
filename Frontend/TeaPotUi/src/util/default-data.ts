import dayjs from "dayjs";
import type { Task, Org, User } from "./types";

const startOfThisWeek = dayjs().startOf("week").add(1, "day");

const createTask = (
  dayOffset: number,
  start: [number, number],
  end: [number, number],
  name: string,
  description: string,
  priority: Task["priority"] = "medium",
  status: Task["status"] = "todo",
  isFixed = false,
): Task => {
  const startDate = startOfThisWeek
    .add(dayOffset, "day")
    .set("hour", start[0])
    .set("minute", start[1])
    .toDate();

  const endDate = startOfThisWeek
    .add(dayOffset, "day")
    .set("hour", end[0])
    .set("minute", end[1])
    .toDate();

  return {
    startDate,
    endDate,
    deadline: endDate,
    name,
    description,
    isFixed,
    priority,
    status,
    org: companyA.id,
    recurrence: "none",
    dependencies: [],
  };
};

const companyA: Org = {
  id: "org-a",
  name: "company a",
  users: [],
  adminEmails: ["admin@company-a.de"],
  invites: [],
};
const companyB: Org = {
  id: "org-b",
  name: "company b",
  users: [],
  adminEmails: ["admin@company-b.de"],
  invites: [
    {
      orgId: "org-b",
      orgName: "company b",
      email: "user@company-a.de",
      status: "pending",
    },
  ],
};

const defaultTasks: Task[] = [
  {
    name: "Sprint planning",
    description: "Lock scope and capacity for the week.",
    dependencies: [],
    startDate: new Date("2026-04-13T09:00:00Z"),
    endDate: new Date("2026-04-13T11:30:00Z"),
    org: companyA.id,
  },
  {
    name: "Backend Sync",
    description: "Align on API for time tracking entries.",
    dependencies: [],
    startDate: new Date("2026-04-14T14:00:00Z"),
    endDate: new Date("2026-04-14T15:00:00Z"),
    org: companyA.id,
    deadline: new Date("2026-04-14T15:00:00Z")
  },
  {
    name: "Client review",
    description: "Walk through the latest planner prototype.",
    dependencies: [],
    startDate: new Date("2026-04-16T10:30:00Z"),
    endDate: new Date("2026-04-16T12:00:00Z"),
    org: companyA.id,
    deadline: new Date("2026-04-16T12:00:00Z")
  },
];

const adminA: User = {
  id: "11111111-1111-1111-1111-111111111111",
  username: "admin",
  displayName: "Admin A",
  email: "admin@company-a.de",
  role: "admin",
  orgs: [],
  tasks: defaultTasks,
  invites: [],
  timezone: "Europe/Berlin",
  workCapacityHours: 8,
  workDays: ["Mon", "Tue", "Wed", "Thu", "Fri"],
  workStart: "09:00",
  workEnd: "17:00",
  breakRules: "30m lunch + 10m after 90m focus",
  notifications: { emailInvites: true, emailDeadlines: true },
};
const userA1: User = {
  id: "22222222-2222-2222-2222-222222222222",
  username: "userA",
  displayName: "Anna A",
  email: "user@company-a.de",
  role: "user",
  orgs: [],
  tasks: [],
  invites: [
    {
      orgId: "org-b",
      orgName: "company b",
      email: "user@company-a.de",
      status: "pending",
    },
  ],
  timezone: "Europe/Berlin",
  workCapacityHours: 6,
  workDays: ["Mon", "Tue", "Wed", "Thu"],
  workStart: "08:30",
  workEnd: "16:00",
  breakRules: "15m every 90m, 30m lunch",
  notifications: { emailInvites: true, emailDeadlines: false },
};

const adminB: User = {
  id: "33333333-3333-3333-3333-333333333333",
  username: "admin",
  displayName: "Admin B",
  email: "admin@company-b.de",
  role: "admin",
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
const userB1: User = {
  id: "44444444-4444-4444-4444-444444444444",
  username: "userB",
  displayName: "Ben B",
  email: "user@company-b.de",
  role: "user",
  orgs: [],
  tasks: [],
  invites: [],
  timezone: "Europe/Berlin",
  workCapacityHours: 7,
  workDays: ["Mon", "Tue", "Wed", "Thu"],
  workStart: "10:00",
  workEnd: "18:00",
  breakRules: "10m after 60m, 45m lunch",
  notifications: { emailInvites: true, emailDeadlines: true },
};

const adminATasks: Task[] = [
  createTask(
    0,
    [9, 0],
    [11, 30],
    "Sprint planning",
    "Lock scope and capacity for the week.",
    "high",
    "in-progress",
    true,
  ),
  createTask(
    1,
    [14, 0],
    [15, 0],
    "Backend sync",
    "Align on API for time tracking entries.",
    "medium",
    "todo",
    true,
  ),
  createTask(
    3,
    [10, 30],
    [12, 0],
    "Client review",
    "Walk through the latest planner prototype.",
    "high",
    "todo",
    true,
  ),
];

const userATasks: Task[] = [
  createTask(
    2,
    [13, 0],
    [15, 0],
    "UI polish",
    "Tidy up scheduler grid and interactions.",
    "medium",
    "in-progress",
    false,
  ),
];

export const getDefaults = () => {
  companyA.users = [adminA, userA1];
  adminA.orgs = [companyA];
  userA1.orgs = [companyA];

  companyB.users = [adminB, userB1];
  adminB.orgs = [companyB];
  userB1.orgs = [companyB];

  adminA.tasks = adminATasks;
  userA1.tasks = userATasks;
  adminB.tasks = [];
  userB1.tasks = [];

  return {
    users: [adminA, adminB, userA1, userB1],
    orgs: [companyA, companyB],
  };
};

export const orgs: Org[] = [
  { id: "org-company-a", name: "Company A", users: [adminA] },
];

export const defaultUser: User = {
  id: "00000000-0000-0000-0000-000000000000",
  email: "example@default.com",
  role: "user",
  username: "defaultUser123",
  orgs: [],
  tasks: [],
  invites: [],
  displayName: "Default User",
  timezone: "Europe/Berlin",
  workCapacityHours: 8,
  workDays: ["Mon", "Tue", "Wed", "Thu", "Fri"],
  workStart: "09:00",
  workEnd: "17:00",
  breakRules: "30m lunch",
  notifications: { emailInvites: true, emailDeadlines: true },
};
