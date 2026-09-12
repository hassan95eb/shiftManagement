export type UserRole = 'Employer' | 'Expert';

export interface LoginRequest {
  username: string;
  password: string;
}

export interface LoginResponse {
  accessToken: string;
  expiresAtUtc: string;
  tokenType: string;
  userId: number;
  role: UserRole;
  employerId: number | null;
  expertId: number | null;
}

export interface AuthSession extends LoginResponse {
  username: string;
}
