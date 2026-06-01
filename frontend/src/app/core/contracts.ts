export type ConnectionState = 'connected' | 'reconnecting' | 'offline';

export interface PublicConfigResponse {
  turnstile: {
    enabled: boolean;
    siteKey: string;
  };
}

export interface HealthResponse {
  status: string;
  api: string;
  database: string;
  realtime: string;
  liveClients: number;
  uptimeSeconds: number;
  latencyMs: number;
  serverTimeUtc: string;
}

export interface StatsResponse {
  totalVisits: number;
  upClicks: number;
  clicks: number;
  algoRuns: number;
  liveClients: number;
  lastVisitAtUtc: string | null;
  lastUpAtUtc: string | null;
  updatedAtUtc: string;
}

export interface SiteEventResponse {
  id: number;
  kind: 'visit' | 'up' | 'flow' | 'algo' | string;
  label: string;
  createdAtUtc: string;
}

export interface UpResponse {
  message: string;
  stats: StatsResponse;
}

export interface FlowSimulationResponse {
  correlationId: string;
  message: string;
  createdAtUtc: string;
}

export type MeetingKind = 'Video' | 'Call' | 'InPerson';

export interface AvailabilitySlot {
  id: string;
  startUtc: string;
  endUtc: string;
  available: boolean;
}

export interface AvailabilityDay {
  date: string;          // yyyy-MM-dd
  slots: AvailabilitySlot[];
}

export interface AvailabilityResponse {
  fromDate: string;
  toDate: string;
  timeZone: string;
  days: AvailabilityDay[];
}


export interface AddressSuggestion {
  label: string;
  latitude?: string | null;
  longitude?: string | null;
}

export interface BookingRequest {
  slotId: string;
  name: string;
  email: string;
  message: string;
  kind: MeetingKind;
  meetingLocation?: string | null;
  emailVerificationToken: string;
  language: 'en' | 'fr' | 'nl';
}

export interface EmailVerificationRequest {
  email: string;
  language: 'en' | 'fr' | 'nl';
  turnstileToken?: string | null;
}

export interface EmailVerificationResponse {
  verificationId: string;
  expiresAtUtc: string;
}

export interface EmailVerificationConfirmRequest {
  verificationId: string;
  email: string;
  code: string;
}

export interface EmailVerificationConfirmResponse {
  email: string;
  emailVerificationToken: string;
}

export interface BookingResponse {
  bookingId: string;
  manageToken: string;
  startUtc: string;
  endUtc: string;
  kind: MeetingKind;
  status: string;
}

export interface BookingResult {
  booking: BookingResponse;
  manageUrl: string;
}

export interface BookingView {
  bookingId: string;
  startUtc: string;
  endUtc: string;
  kind: MeetingKind;
  status: string;
  name: string;
  email: string;
  message?: string;
  meetingLocation?: string | null;
  requestedStartUtc?: string | null;
  requestedEndUtc?: string | null;
}
