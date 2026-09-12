export type ApplicationStatus = 'Pending' | 'Approved' | 'Rejected';

export interface ShiftApplication {
  id: number;
  shiftId: number;
  expertId: number;
  status: ApplicationStatus;
  appliedAtUtc: string;
  decidedByUserId: number | null;
  decidedAtUtc: string | null;
  decisionNote: string | null;
}

export interface ApplicationFilters {
  shiftId?: number;
  status?: ApplicationStatus;
}

export interface Recommendation {
  expertId: number;
  score: number;
  reason: string;
  computedAtUtc: string;
}
