import { apiRequest } from './api';
import type { Availability, AvailabilityInput, CreateShiftInput, Shift, ShiftFilters, UpdateShiftInput } from '../types/scheduling';

function queryString(filters: ShiftFilters) {
  const params = new URLSearchParams();
  if (filters.projectId) params.set('projectId', String(filters.projectId));
  if (filters.status) params.set('status', filters.status);
  if (filters.fromUtc) params.set('fromUtc', filters.fromUtc);
  if (filters.toUtc) params.set('toUtc', filters.toUtc);
  const value = params.toString();
  return value ? `?${value}` : '';
}

export const shiftsApi = {
  list: (filters: ShiftFilters = {}) => apiRequest<Shift[]>(`/api/shifts${queryString(filters)}`),
  get: (id: number) => apiRequest<Shift>(`/api/shifts/${id}`),
  create: (input: CreateShiftInput) => apiRequest<Shift>('/api/shifts', { method: 'POST', body: JSON.stringify(input) }),
  update: (id: number, input: UpdateShiftInput) => apiRequest<Shift>(`/api/shifts/${id}`, { method: 'PUT', body: JSON.stringify(input) }),
  open: (projectId?: number) => apiRequest<Shift[]>(`/api/shifts/open${projectId ? `?projectId=${projectId}` : ''}`),
};

export const availabilityApi = {
  list: () => apiRequest<Availability[]>('/api/availability'),
  get: (id: number) => apiRequest<Availability>(`/api/availability/${id}`),
  create: (input: AvailabilityInput) => apiRequest<Availability>('/api/availability', { method: 'POST', body: JSON.stringify(input) }),
  update: (id: number, input: AvailabilityInput) => apiRequest<Availability>(`/api/availability/${id}`, { method: 'PUT', body: JSON.stringify(input) }),
  remove: (id: number) => apiRequest<void>(`/api/availability/${id}`, { method: 'DELETE' }),
};
