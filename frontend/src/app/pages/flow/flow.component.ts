import { Component, computed, inject, signal } from '@angular/core';
import { ApiService } from '../../core/api.service';
import { LiveConnectionService } from '../../core/live-connection.service';
import { TranslatePipe } from '../../i18n/translate.pipe';

type StepStatus = 'idle' | 'running' | 'done';

interface FlowStep {
  id: string;
  titleKey: string;
  detailKey: string;
  status: StepStatus;
}

@Component({
  selector: 'app-flow',
  imports: [TranslatePipe],
  templateUrl: './flow.component.html',
  styleUrl: './flow.component.scss'
})
export class FlowComponent {
  private readonly api = inject(ApiService);
  protected readonly live = inject(LiveConnectionService);
  protected readonly running = signal(false);
  protected readonly correlationId = signal<string | null>(null);
  protected readonly steps = signal<FlowStep[]>([
    { id: 'user', titleKey: 'flow.step.user.title', detailKey: 'flow.step.user.detail', status: 'idle' },
    { id: 'angular', titleKey: 'flow.step.angular.title', detailKey: 'flow.step.angular.detail', status: 'idle' },
    { id: 'api', titleKey: 'flow.step.api.title', detailKey: 'flow.step.api.detail', status: 'idle' },
    { id: 'db', titleKey: 'flow.step.db.title', detailKey: 'flow.step.db.detail', status: 'idle' },
    { id: 'signalr', titleKey: 'flow.step.signalr.title', detailKey: 'flow.step.signalr.detail', status: 'idle' },
    { id: 'ui', titleKey: 'flow.step.ui.title', detailKey: 'flow.step.ui.detail', status: 'idle' }
  ]);

  protected readonly doneCount = computed(() => this.steps().filter((step) => step.status === 'done').length);

  simulate(): void {
    if (this.running()) {
      return;
    }

    this.running.set(true);
    this.correlationId.set(null);
    this.steps.update((steps) => steps.map((step) => ({ ...step, status: 'idle' })));

    void this.playSteps();
  }

  private async playSteps(): Promise<void> {
    for (let index = 0; index < this.steps().length; index++) {
      this.setStatus(index, 'running');
      await this.delay(520);

      if (this.steps()[index].id === 'db') {
        await this.callBackendSimulation();
      }

      this.setStatus(index, 'done');
      await this.delay(160);
    }

    this.running.set(false);
  }

  private callBackendSimulation(): Promise<void> {
    return new Promise((resolve) => {
      this.api.simulateFlow().subscribe({
        next: (response) => this.correlationId.set(response.correlationId),
        error: () => this.live.state.set('offline'),
        complete: () => resolve()
      });
    });
  }

  private setStatus(index: number, status: StepStatus): void {
    this.steps.update((steps) => steps.map((step, currentIndex) => currentIndex === index ? { ...step, status } : step));
  }

  private delay(ms: number): Promise<void> {
    return new Promise((resolve) => window.setTimeout(resolve, ms));
  }
}
