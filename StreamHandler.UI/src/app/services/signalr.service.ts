import { Injectable, OnDestroy, signal } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Stream, StatusSummary } from '../models/stream.model';

@Injectable({ providedIn: 'root' })
export class SignalRService implements OnDestroy {
  private hub: signalR.HubConnection;

  readonly connected = signal(false);

  // Emitters — components subscribe via effect() or toObservable()
  private streamUpdateHandlers: ((s: Stream) => void)[] = [];
  private summaryUpdateHandlers: ((s: StatusSummary) => void)[] = [];

  constructor() {
    this.hub = new signalR.HubConnectionBuilder()
      .withUrl('http://localhost:5000/hubs/stream-status')
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    this.hub.on('StreamStatusUpdated', (stream: Stream) => {
      this.streamUpdateHandlers.forEach(h => h(stream));
    });

    this.hub.on('StatusSummaryUpdated', (summary: StatusSummary) => {
      this.summaryUpdateHandlers.forEach(h => h(summary));
    });

    this.hub.onreconnected(() => this.connected.set(true));
    this.hub.onreconnecting(() => this.connected.set(false));
    this.hub.onclose(() => this.connected.set(false));

    this.start();
  }

  private async start() {
    try {
      await this.hub.start();
      this.connected.set(true);
    } catch {
      // Retry after 5s if API isn't up yet
      setTimeout(() => this.start(), 5000);
    }
  }

  onStreamUpdate(handler: (s: Stream) => void): () => void {
    this.streamUpdateHandlers.push(handler);
    return () => { this.streamUpdateHandlers = this.streamUpdateHandlers.filter(h => h !== handler); };
  }

  onSummaryUpdate(handler: (s: StatusSummary) => void): () => void {
    this.summaryUpdateHandlers.push(handler);
    return () => { this.summaryUpdateHandlers = this.summaryUpdateHandlers.filter(h => h !== handler); };
  }

  ngOnDestroy() {
    this.hub.stop();
  }
}
