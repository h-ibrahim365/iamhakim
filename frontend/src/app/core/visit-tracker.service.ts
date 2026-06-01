import { Injectable, inject } from '@angular/core';
import { ApiService } from './api.service';
import { LiveConnectionService } from './live-connection.service';

/**
 * Records exactly one visit per JavaScript runtime, regardless of which route
 * the user landed on. Called from the App component's constructor so it fires
 * at app boot — whether the user opens `/`, `/projects`, `/book`, or anything
 * else.
 *
 * Browser-only (no-op during SSR). No cookies, no localStorage, no
 * sessionStorage — the in-memory `started` flag survives within a tab and is
 * wiped on hard reload / tab close, which is the trade-off for not needing a
 * consent banner under ePrivacy.
 */
@Injectable({ providedIn: 'root' })
export class VisitTrackerService {
  private readonly api = inject(ApiService);
  private readonly live = inject(LiveConnectionService);

  private started = false;

  start(): void {
    if (this.started) return;
    if (typeof window === 'undefined') return;
    this.started = true;

    this.api.recordVisit().subscribe({
      next: (stats) => this.live.stats.set(stats),
      error: () => undefined
    });
  }
}
