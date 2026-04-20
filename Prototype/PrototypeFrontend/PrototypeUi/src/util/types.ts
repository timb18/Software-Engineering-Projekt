export type WorkWeekDay = "Mon" | "Tue" | "Wed" | "Thu" | "Fri" | "Sat" | "Sun";

export type WorkBreak = {
  id: string;
  startTime: string; // HH:mm
  endTime: string; // HH:mm
};

export type WorkBlock = {
  id: string;
  companyId: string;
  companyName: string;
  startTime: string; // HH:mm
  endTime: string; // HH:mm
};

export type WorkDayProfile = {
  day: WorkWeekDay;
  blocks: WorkBlock[];
  breaks: WorkBreak[];
};

export type WorkProfile = {
  days: WorkDayProfile[];
};

export type User = {
  username: string;
  displayName?: string;
  email: string;
  profileImage?: string;
  timezone?: string;
  plannerViewStart?: string; // HH:mm
  plannerViewEnd?: string; // HH:mm
  workProfile?: WorkProfile;
  workCapacityHours?: number;
  workDays?: string[];
  workStart?: string; // HH:mm
  workEnd?: string; // HH:mm
  breakRules?: string;
  notifications?: Notifications;
  orgs: Org[];
  tasks: Task[];
  role: Role;
  invites?: Invitation[];
};

export type Org = {
  id: string;
  name: string;
  users: User[];
  adminEmails?: string[];
  invites?: Invitation[];
};

export type Calendar = {
  tasks: Task[];
};

export type Task = {
  startDate: Date;
  endDate: Date;
  name: string;
  description: string;
  isFixed?: boolean;
  priority?: Priority;
  status?: "todo" | "in-progress" | "done";
  org: Org;
  recurrence?: "none" | "daily" | "weekly";
  deadline?: Date;
  dependencies: Task[];
};

export type Priority = "low" | "medium" | "high";

export type Role = "admin" | "user";

export type Invitation = {
  orgId: string;
  orgName: string;
  email: string;
  status: "pending" | "accepted" | "declined";
};

export type Notifications = {
  emailInvites: boolean;
  emailDeadlines: boolean;
};
