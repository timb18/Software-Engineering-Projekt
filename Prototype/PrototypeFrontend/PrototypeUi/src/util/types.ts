export type User = {
  username: string;
  displayName?: string;
  email: string;
  profileImage?: string;
  timezone?: string;
  workCapacityHours?: number;
  workDays?: string[];
  workStart?: string; // HH:mm
  workEnd?: string; // HH:mm
  breakRules?: string;
  notifications?: Notifications;
  teams: Team[];
  tasks: Task[];
  role: Role;
  invites?: Invitation[];
};

export type Team = {
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
  priority?: "low" | "medium" | "high";
  status?: "todo" | "in-progress" | "done";
  assigneeEmail?: string;
  recurrence?: "none" | "daily" | "weekly";
  deadline?: Date;
  dependencies: Task[];
};

export type Role = "admin" | "user";

export type Invitation = {
  teamId: string;
  teamName: string;
  email: string;
  status: "pending" | "accepted" | "declined";
};

export type Notifications = {
  emailInvites: boolean;
  emailDeadlines: boolean;
};
