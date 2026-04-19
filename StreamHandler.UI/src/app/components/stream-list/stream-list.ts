import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { StreamService } from '../../services/stream.service';
import { SignalRService } from '../../services/signalr.service';
import { Stream } from '../../models/stream.model';
import { AddStreamComponent } from '../add-stream/add-stream';

@Component({
  selector: 'app-stream-list',
  standalone: true,
  imports: [CommonModule, AddStreamComponent],
  templateUrl: './stream-list.html',
  styleUrl: './stream-list.scss'
})
export class StreamListComponent implements OnInit, OnDestroy {
  private readonly streamService = inject(StreamService);
  private readonly signalR = inject(SignalRService);

  streams = signal<Stream[]>([]);
  showAddModal = signal(false);
  checkingIds = signal<Set<string>>(new Set());

  private unsubscribe!: () => void;

  ngOnInit() {
    this.load();
    this.unsubscribe = this.signalR.onStreamUpdate(updated => {
      this.streams.update(list => {
        const idx = list.findIndex(s => s.id === updated.id);
        if (idx >= 0) {
          const next = [...list];
          next[idx] = updated;
          return this.sortStreams(next);
        }
        return list;
      });

      // Remove from checking spinner set when update arrives
      if (updated.status !== 'Checking') {
        this.checkingIds.update(ids => {
          const next = new Set(ids);
          next.delete(updated.id);
          return next;
        });
      }
    });
  }

  ngOnDestroy() { this.unsubscribe?.(); }

  load() {
    this.streamService.getStreams().subscribe(s => this.streams.set(this.sortStreams(s)));
  }

  onStreamAdded(s: Stream) {
    this.streams.update(list => this.sortStreams([...list, s]));
    this.showAddModal.set(false);
  }

  triggerCheck(stream: Stream) {
    this.checkingIds.update(ids => new Set([...ids, stream.id]));
    this.streamService.checkStream(stream.id).subscribe({
      next: updated => {
        this.streams.update(list => {
          const next = list.map(s => s.id === updated.id ? updated : s);
          return this.sortStreams(next);
        });
        this.checkingIds.update(ids => { const n = new Set(ids); n.delete(updated.id); return n; });
      },
      error: () => {
        this.checkingIds.update(ids => { const n = new Set(ids); n.delete(stream.id); return n; });
      }
    });
  }

  deleteStream(stream: Stream) {
    if (!confirm(`Remove "${stream.name}"?`)) return;
    this.streamService.deleteStream(stream.id).subscribe(() => {
      this.streams.update(list => list.filter(s => s.id !== stream.id));
    });
  }

  private sortStreams(list: Stream[]): Stream[] {
    const order = { Live: 0, Checking: 1, Unknown: 2, Offline: 3 };
    return [...list].sort((a, b) => (order[a.status] ?? 4) - (order[b.status] ?? 4));
  }

  isChecking(id: string): boolean {
    return this.checkingIds().has(id);
  }
}
