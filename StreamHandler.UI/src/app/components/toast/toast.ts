import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SignalRService } from '../../services/signalr.service';
import { StreamAlert } from '../../models/stream.model';

interface Toast {
  id: number;
  message: string;
  type: 'offline' | 'online';
}

@Component({
  selector: 'app-toast',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './toast.html',
  styleUrl: './toast.scss'
})
export class ToastComponent implements OnInit, OnDestroy {
  private readonly signalR = inject(SignalRService);
  private counter = 0;
  private unsubscribe!: () => void;

  toasts = signal<Toast[]>([]);

  ngOnInit() {
    this.unsubscribe = this.signalR.onAlert((alert: StreamAlert) => {
      const type = alert.message.includes('offline') ? 'offline' : 'online';
      const id = ++this.counter;
      this.toasts.update(t => [...t, { id, message: alert.message, type }]);
      setTimeout(() => this.dismiss(id), 5000);
    });
  }

  ngOnDestroy() { this.unsubscribe?.(); }

  dismiss(id: number) {
    this.toasts.update(t => t.filter(x => x.id !== id));
  }
}
