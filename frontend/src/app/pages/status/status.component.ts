import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { ApiService } from '../../core/api.service';
import { HealthResponse } from '../../core/contracts';
import { LiveConnectionService } from '../../core/live-connection.service';
import { TranslatePipe } from '../../i18n/translate.pipe';
import { BookCtaComponent } from '../../shared/book-cta/book-cta.component';

@Component({
  selector: 'app-status',
  imports: [TranslatePipe, BookCtaComponent],
  templateUrl: './status.component.html',
  styleUrl: './status.component.scss'
})
export class StatusComponent implements OnInit, OnDestroy {
  private readonly api = inject(ApiService);
  protected readonly live = inject(LiveConnectionService);
  protected readonly health = signal<HealthResponse | null>(null);
  protected readonly loading = signal(false);
  private intervalId: number | null = null;

  ngOnInit(): void {
    if (typeof window === 'undefined') {
      return;
    }

    this.refresh();
    this.intervalId = window.setInterval(() => this.refresh(), 7000);
  }

  ngOnDestroy(): void {
    if (this.intervalId !== null) {
      window.clearInterval(this.intervalId);
    }
  }

  refresh(): void {
    this.loading.set(true);

    this.api.getHealth().subscribe({
      next: (health) => this.health.set(health),
      error: () => this.live.state.set('offline'),
      complete: () => this.loading.set(false)
    });
  }

  formatUptime(seconds: number | undefined): string {
    if (!seconds) {
      return '0s';
    }

    const days = Math.floor(seconds / 86400);
    const hours = Math.floor((seconds % 86400) / 3600);
    const minutes = Math.floor((seconds % 3600) / 60);

    if (days > 0) {
      return `${days}d ${hours}h ${minutes}m`;
    }

    if (hours > 0) {
      return `${hours}h ${minutes}m`;
    }

    return `${minutes}m ${seconds % 60}s`;
  }
}
