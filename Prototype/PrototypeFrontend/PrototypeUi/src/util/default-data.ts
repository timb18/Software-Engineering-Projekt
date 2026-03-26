import type { Team, User } from "./types";

const adminA: User = {
  username: "admin",
  email: "admin@company-a.de",
  role: "admin",
  teams: [],
};
const userA1: User = {
  username: "userA",
  email: "user@company-a.de",
  role: "user",
  teams: [],
};

const adminB: User = {
  username: "admin",
  email: "admin@company-b.de",
  role: "admin",
  teams: [],
};
const userB1: User = {
  username: "userB",
  email: "user@company-b.de",
  role: "user",
  teams: [],
};

const companyA: Team = {
  name: "company a",
  users: [],
};
const companyB: Team = {
  name: "company b",
  users: [],
};

export const getDefaults = () => {
  companyA.users = [adminA, userA1];
  adminA.teams = [companyA];
  userA1.teams = [companyA];

  companyB.users = [adminB, userB1];
  adminB.teams = [companyB];
  userB1.teams = [companyB];

  return {
    users: [adminA, adminB, userA1, userB1],
    teams: [companyA, companyB],
  };
};

export const teamms: Team[] = [{ name: "Company A", users: [adminA] }];

export const defaultUser: User = {
  email: "example@default.com",
  role: "user",
  username: "defaultUser123",
  teams: [],
};
