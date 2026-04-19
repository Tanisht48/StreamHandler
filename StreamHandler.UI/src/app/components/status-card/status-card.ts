import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { StreamService } from '../../services/stream.service';
import { SignalRService } from '../../services/signalr.service';
import { StatusSummary } from '../../models/stream.model';

@Component({
  selector: 'app-status-card',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './status-card.html',
  styleUrl: './status-card.scss'
})
export class StatusCardComponent implements OnInit, OnDestroy {
  private readonly streamService = inject(StreamService);
  private readonly signalR = inject(SignalRService);

  summary = signal<StatusSummary | null>(null);
  readonly connected = this.signalR.connected;

  private unsubscribe!: () => void;

  ngOnInit() {
    this.load();
    this.unsubscribe = this.signalR.onSummaryUpdate(s => this.summary.set(s));
  }

  ngOnDestroy() { this.unsubscribe?.(); }

  private load() {
    this.streamService.getStatus().subscribe(s => this.summary.set(s));
  }
}
