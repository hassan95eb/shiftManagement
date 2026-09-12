import { apiRequest } from './api';
import type { CreateExpertInput, CreateProjectInput, Expert, Project, UpdateProjectInput } from '../types/management';

export const projectsApi = {
  list: () => apiRequest<Project[]>('/api/projects'),
  get: (id: number) => apiRequest<Project>(`/api/projects/${id}`),
  create: (input: CreateProjectInput) => apiRequest<Project>('/api/projects', {
    method: 'POST',
    body: JSON.stringify(input),
  }),
  update: (id: number, input: UpdateProjectInput) => apiRequest<Project>(`/api/projects/${id}`, {
    method: 'PUT',
    body: JSON.stringify(input),
  }),
  remove: (id: number) => apiRequest<void>(`/api/projects/${id}`, { method: 'DELETE' }),
  experts: (id: number) => apiRequest<Expert[]>(`/api/projects/${id}/experts`),
};

export const expertsApi = {
  list: () => apiRequest<Expert[]>('/api/experts'),
  get: (id: number) => apiRequest<Expert>(`/api/experts/${id}`),
  create: (input: CreateExpertInput) => apiRequest<Expert>('/api/experts', {
    method: 'POST',
    body: JSON.stringify(input),
  }),
  projects: (id: number) => apiRequest<Project[]>(`/api/experts/${id}/projects`),
  assign: (expertId: number, projectId: number) => apiRequest<void>(`/api/experts/${expertId}/projects/${projectId}`, { method: 'POST' }),
  unassign: (expertId: number, projectId: number) => apiRequest<void>(`/api/experts/${expertId}/projects/${projectId}`, { method: 'DELETE' }),
};
