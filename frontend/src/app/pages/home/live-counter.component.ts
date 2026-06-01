import {
  Component,
  PLATFORM_ID,
  effect,
  inject,
  input,
  signal
} from '@angular/core';
import { isPlatformBrowser } from '@angular/common';

/**
 * A number that smoothly counts up to its target and pulses whenever the value
 * changes - so live SignalR updates feel alive instead of just snapping.
 */
@Component({
  selector: 'app-live-counter',
  template: `<span class="lc" [class.pulse]="pulsing()">{{ shown() }}</span>`,
  styles: [`
    .lc {
      display: inline-block;
      font-family: var(--font-display);
      font-weight: 800;
      font-variant-numeric: tabular-nums;
      transition: transform 0.25s ease, color 0.25s ease;
    }
    .lc.pulse { animation: lc-pulse 0.5s ease; }
    @keyframes lc-pulse {
      0% { transform: scale(1); }
      40% { transform: scale(1.22); color: var(--amber); }
      100% { transform: scale(1); }
    }
  `]
})
export class LiveCounterComponent {
  private readonly platformId = inject(PLATFORM_ID);

  readonly value = input<number>(0);

  protected readonly shown = signal(0);
  protected readonly pulsing = signal(false);

  private current = 0;
  private raf: number | null = null;

  constructor() {
    effect(() => {
      const target = this.value();
      if (!isPlatformBrowser(this.platformId)) {
        this.shown.set(target);
        this.current = target;
        return;
      }
      if (target === this.current) return;
      this.animateTo(target);
      this.pulse();
    });
  }

  private animateTo(target: number): void {
    if (this.raf !== null) cancelAnimationFrame(this.raf);
    const from = this.current;
    const delta = target - from;
    const duration = Math.min(900, 250 + Math.abs(delta) * 12);
    const start = performance.now();

    const tick = (now: number) => {
      const t = Math.min(1, (now - start) / duration);
      const eased = 1 - Math.pow(1 - t, 3);
      const val = Math.round(from + delta * eased);
      this.shown.set(val);
      if (t < 1) {
        this.raf = requestAnimationFrame(tick);
      } else {
        this.shown.set(target);
        this.current = target;
      }
    };
    this.raf = requestAnimationFrame(tick);
  }

  private pulse(): void {
    this.pulsing.set(false);
    requestAnimationFrame(() => {
      this.pulsing.set(true);
      window.setTimeout(() => this.pulsing.set(false), 500);
    });
  }
}
