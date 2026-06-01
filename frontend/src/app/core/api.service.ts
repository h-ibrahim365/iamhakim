import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AddressSuggestion, AvailabilityResponse, BookingRequest, BookingResponse, BookingResult, BookingView, EmailVerificationConfirmRequest, EmailVerificationConfirmResponse, EmailVerificationRequest, EmailVerificationResponse, FlowSimulationResponse, HealthResponse, PublicConfigResponse, SiteEventResponse, StatsResponse, UpResponse } from './contracts';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);

  getPublicConfig(): Observable<PublicConfigResponse> {
    return this.http.get<PublicConfigResponse>('/api/public-config');
  }

  getHealth(): Observable<HealthResponse> {
    return this.http.get<HealthResponse>('/api/health');
  }

  getStats(): Observable<StatsResponse> {
    return this.http.get<StatsResponse>('/api/stats');
  }

  getEvents(limit = 10): Observable<SiteEventResponse[]> {
    return this.http.get<SiteEventResponse[]>(`/api/events?limit=${limit}`);
  }

  recordVisit(): Observable<StatsResponse> {
    return this.http.post<StatsResponse>('/api/visit', {});
  }

  pressUp(): Observable<UpResponse> {
    return this.http.post<UpResponse>('/api/up', {});
  }

  recordClick(): Observable<StatsResponse> {
    return this.http.post<StatsResponse>('/api/click', {});
  }

  simulateFlow(): Observable<FlowSimulationResponse> {
    return this.http.post<FlowSimulationResponse>('/api/flow/simulate', {});
  }

  recordAlgoRun(outcome: 'found' | 'no-path', expanded: number, maze = false): Observable<StatsResponse> {
    return this.http.post<StatsResponse>('/api/algo-run', { outcome, expanded, maze });
  }

  getAvailability(): Observable<AvailabilityResponse> {
    return this.http.get<AvailabilityResponse>('/api/availability');
  }

  searchBelgianAddresses(query: string): Observable<AddressSuggestion[]> {
    return this.http.get<AddressSuggestion[]>(`/api/address-search?q=${encodeURIComponent(query)}`);
  }

  requestEmailVerification(request: EmailVerificationRequest): Observable<EmailVerificationResponse> {
    return this.http.post<EmailVerificationResponse>('/api/bookings/email-verification/request', request);
  }

  confirmEmailVerification(request: EmailVerificationConfirmRequest): Observable<EmailVerificationConfirmResponse> {
    return this.http.post<EmailVerificationConfirmResponse>('/api/bookings/email-verification/confirm', request);
  }

  createBooking(request: BookingRequest): Observable<BookingResult> {
    return this.http.post<BookingResult>('/api/bookings', request);
  }

  getBooking(token: string): Observable<BookingView> {
    return this.http.get<BookingView>(`/api/bookings/manage?token=${encodeURIComponent(token)}`);
  }

  manageBooking(manageToken: string, action: 'cancel' | 'reschedule', newSlotId?: string): Observable<BookingResponse> {
    return this.http.post<BookingResponse>('/api/bookings/manage', { manageToken, action, newSlotId });
  }
}
