import { Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { LiveConnectionService } from '../../core/live-connection.service';
import { AstarComponent } from './astar.component';
import { LiveCounterComponent } from './live-counter.component';
import { TranslatePipe } from '../../i18n/translate.pipe';

@Component({
  selector: 'app-home',
  imports: [RouterLink, AstarComponent, LiveCounterComponent, TranslatePipe],
  templateUrl: './home.component.html',
  styleUrl: './home.component.scss'
})
export class HomeComponent {
  protected readonly api = inject(ApiService);
  protected readonly live = inject(LiveConnectionService);

  protected readonly stats = computed(() => this.live.stats());
  protected readonly totalVisits = computed(() => this.stats()?.totalVisits ?? 0);
  protected readonly upClicks = computed(() => this.stats()?.upClicks ?? 0);
  protected readonly clicks = computed(() => this.stats()?.clicks ?? 0);
  protected readonly algoRuns = computed(() => this.stats()?.algoRuns ?? 0);

  /** Persists every settled A* run; maze rounds are flagged so the backend can count plays. */
  onAlgoFinished(result: { outcome: 'found' | 'no-path'; expanded: number; mode: 'demo' | 'maze'; score: number }): void {
    this.api.recordAlgoRun(result.outcome, result.expanded, result.mode === 'maze').subscribe({
      next: (stats) => this.live.stats.set(stats),
      error: () => undefined
    });
  }
}
