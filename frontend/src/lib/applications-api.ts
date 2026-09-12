import { apiRequest } from './api';
import type { ApplicationFilters, Recommendation, ShiftApplication } from '../types/applications';

function queryString(filters: ApplicationFilters) {
  const params = new URLSearchParams();
  if (filters.shiftId) params.set('shiftId', String(filters.shiftId));
  if (filters.status) params.set('status', filters.status);
  const value = params.toString();
  return value ? `?${value}` : '';
}

export const applicationsApi = {
  list: (filters: ApplicationFilters = {}) => apiRequest<ShiftApplication[]>(`/api/applications${queryString(filters)}`),
  apply: (shiftId: number) => apiRequest<ShiftApplication>(`/api/shifts/${shiftId}/applications`, { method: 'POST' }),
  approve: (applicationId: number) => apiRequest<ShiftApplication>(`/api/applications/${applicationId}/approval`, { method: 'POST' }),
  reject: (applicationId: number) => apiRequest<ShiftApplication>(`/api/applications/${applicationId}/rejection`, { method: 'POST' }),
  recommendations: (shiftId: number) => apiRequest<Recommendation[]>(`/api/shifts/${shiftId}/recommendations`),
};
