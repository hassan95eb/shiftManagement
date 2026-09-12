export interface Project {
  id: number;
  employerId: number;
  name: string;
  isActive: boolean;
  createdAtUtc: string;
}

export interface Expert {
  id: number;
  userId: number;
  fullName: string;
  isActive: boolean;
  createdAtUtc: string;
}

export interface CreateProjectInput {
  name: string;
}

export interface UpdateProjectInput {
  name: string;
  isActive: boolean;
}

export interface CreateExpertInput {
  username: string;
  password: string;
  fullName: string;
}
