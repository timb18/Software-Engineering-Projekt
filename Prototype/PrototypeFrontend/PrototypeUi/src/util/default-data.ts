import type { Team, User } from "./types";

const adminA: User = {
  username: "admin",
  email: "adimin@company-a.de",
  role: "admin",
  teams: [],
};
const userA1: User = {
  username: "userA",
  email: "user@company-a.de",
  role: "user",
  teams: [],
};

export const users: User[] = [adminA];

export const teamms: Team[] = [{ name: "Company A", users: [adminA] }];
