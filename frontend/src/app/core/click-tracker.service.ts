import { Injectable, inject } from '@angular/core';
import { ApiService } from './api.service';
import { LiveConnectionService } from './live-connection.service';

/**
 * Tracks meaningful user clicks across the whole site.
 *
 * Behaviour:
 * - Browser-only (no-op during SSR).
 * - Listens to pointerdown on the document with capture phase.
 * - Only counts clicks on interactive targets (button, a, [role=button], input).
 * - Throttled to one POST per second to keep noise out of the counter.
 * - Updates the shared stats signal optimistically — SignalR will reconcile.
 */
@Injectable({ providedIn: 'root' })
export class ClickTrackerService {
  private readonly api = inject(ApiService);
  private readonly live = inject(LiveConnectionService);

  private started = false;
  private lastSentAt = 0;
  private readonly minIntervalMs = 1000;

  start(): void {
    if (this.started) return;
    if (typeof window === 'undefined' || typeof document === 'undefined') return;
    this.started = true;

    document.addEventListener(
      'pointerdown',
      (event) => this.handle(event),
      { capture: true, passive: true }
    );
  }

  private handle(event: PointerEvent): void {
    const target = event.target as Element | null;
    if (!target) return;

    // Only track clicks on actually interactive targets.
    const interactive = target.closest(
      'button, a, [role="button"], input[type="button"], input[type="submit"], .ctrl, .slot, .cal-cell'
    );
    if (!interactive) return;

    const now = Date.now();
    if (now - this.lastSentAt < this.minIntervalMs) return;
    this.lastSentAt = now;

    this.api.recordClick().subscribe({
      next: (stats) => this.live.stats.set(stats),
      error: () => undefined
    });
  }
}
