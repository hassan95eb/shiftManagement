export type ShiftStatus = 'Open' | 'Closed';

export interface Shift {
  id: number;
  projectId: number;
  startUtc: string;
  endUtc: string;
  status: ShiftStatus;
  createdAtUtc: string;
  rowVersion: string;
}

export interface ShiftFilters {
  projectId?: number;
  status?: ShiftStatus;
  fromUtc?: string;
  toUtc?: string;
}

export interface CreateShiftInput {
  projectId: number;
  startUtc: string;
  endUtc: string;
}

export interface UpdateShiftInput {
  startUtc: string;
  endUtc: string;
  rowVersion: string;
}

export interface Availability {
  id: number;
  expertId: number;
  startUtc: string;
  endUtc: string;
  createdAtUtc: string;
}

export interface AvailabilityInput {
  startUtc: string;
  endUtc: string;
}
