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
  id?: string;
  membershipId?: string;
  maxDailyLoad?: string;
  plannerViewStart?: string;
  plannerViewEnd?: string;
  days: WorkDayProfile[];
};

export type User = {
  id: string;
  email: string;
  timezone?: string;
  plannerViewStart?: string; // HH:mm
  plannerViewEnd?: string; // HH:mm
  workProfile?: WorkProfile;
  hasPersistedWorkProfile?: boolean;
  workCapacityHours?: number;
  workDays?: string[];
  workStart?: string; // HH:mm
  workEnd?: string; // HH:mm
  breakRules?: string;
  orgs: Org[];
  tasks: Task[];
  role: Role;
  invites?: Invitation[];
};

export type OrgUser = {
  id: string;
  email: string;
  username: string;
  role: string;
};

export type Org = {
  id: string;
  name: string;
  auth0OrganizationId?: string;
  workProfileId?: string | null;
  users: OrgUser[];
  adminEmails?: string[];
  invites?: Invitation[];
};

export type Calendar = {
  tasks: Task[];
};

export type TaskIntensity = "light" | "normal" | "intensive";

export type Task = {
  id?: string;
  startDate: Date;
  endDate: Date;
  name: string;
  description: string;
  isFixed?: boolean;
  priority?: Priority;
  intensity?: TaskIntensity;
  status?: "todo" | "in-progress" | "done";
  org: string;
  recurrence?: "none" | "daily" | "weekly";
  deadline?: Date;
  dependencies: Task[];
  /** Original work-duration estimate in minutes, kept separate from startDate/endDate so
   *  editing a scheduled task does not corrupt the estimate. */
  timeEstimateMinutes?: number;
};

export type Priority = "low" | "medium" | "high";

export type Role = "admin" | "user";

export type Invitation = {
  id?: string;
  organizationId?: string;
  orgId: string;
  orgName: string;
  email: string;
  firstName?: string;
  lastName?: string;
  status: "pending" | "open" | "accepted" | "declined";
  invitationUrl?: string;
};

export type Notifications = {
  emailInvites: boolean;
  emailDeadlines: boolean;
};
