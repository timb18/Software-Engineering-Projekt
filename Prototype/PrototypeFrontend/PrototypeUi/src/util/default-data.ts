import type { Team, User } from "./types";

const userA: User = {username: "admin", role: "admin", teams: []}

export const users: User[] = [
    userA,
]

export const teamms: Team[] = [
	{name: "Company A", users: [userA]}
]