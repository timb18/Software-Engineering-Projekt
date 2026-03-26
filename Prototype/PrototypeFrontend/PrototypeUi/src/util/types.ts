export type User = {
  username: string;
  email: string;
  teams: Team[];
  tasks: Task[];
  role: Role;
};

export type Team = {
  name: string;
  users: User[];
};

export type Calendar = {
  tasks: Task[];
};

export type Task = {
  startDate: Date;
  endDate: Date;
  name: string;
  description: string;
  dependencies: Task[];
};

export type Role = "admin" | "user";
