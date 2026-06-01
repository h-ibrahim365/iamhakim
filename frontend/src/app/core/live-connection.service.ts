import { Injectable, signal } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { ConnectionState, SiteEventResponse, StatsResponse } from './contracts';

@Injectable({ providedIn: 'root' })
export class LiveConnectionService {
  readonly state = signal<ConnectionState>('offline');
  readonly stats = signal<StatsResponse | null>(null);
  readonly liveClients = signal(0);
  readonly timeline = signal<SiteEventResponse[]>([]);
  readonly lastError = signal<string | null>(null);

  private connection: signalR.HubConnection | null = null;
  private startPromise: Promise<void> | null = null;

  private hubUrl(): string {
    if (typeof window === 'undefined') {
      return '/hubs/live';
    }

    const host = window.location.hostname;
    if (host === 'localhost' || host === '127.0.0.1') {
      return `http://${host}:5172/hubs/live`;
    }

    return '/hubs/live';
  }

  start(): Promise<void> {
    if (typeof window === 'undefined') {
      return Promise.resolve();
    }

    if (this.startPromise) {
      return this.startPromise;
    }

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(this.hubUrl())
      .withAutomaticReconnect([0, 1500, 3500, 7000, 12000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    this.connection.on('statsUpdated', (stats: StatsResponse) => {
      this.stats.set(stats);
      this.liveClients.set(stats.liveClients);
    });

    this.connection.on('liveClientsUpdated', (count: number) => {
      this.liveClients.set(count);
    });

    this.connection.on('timelineEvent', (event: SiteEventResponse) => {
      this.timeline.update((events) => [event, ...events].slice(0, 8));
    });

    this.connection.onreconnecting((error) => {
      this.state.set('reconnecting');
      this.lastError.set(error?.message ?? 'Realtime channel is reconnecting.');
    });

    this.connection.onreconnected(() => {
      this.state.set('connected');
      this.lastError.set(null);
    });

    this.connection.onclose((error) => {
      this.state.set('offline');
      this.lastError.set(error?.message ?? null);
      this.startPromise = null;
      window.setTimeout(() => void this.start(), 4000);
    });

    this.startPromise = this.connection
      .start()
      .then(() => {
        this.state.set('connected');
        this.lastError.set(null);
      })
      .catch((error: unknown) => {
        this.state.set('offline');
        this.lastError.set(error instanceof Error ? error.message : 'Unable to connect to realtime channel.');
        this.startPromise = null;
        window.setTimeout(() => void this.start(), 4000);
      });

    return this.startPromise;
  }
}
