import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { StreamService } from '../../services/stream.service';
import { SignalRService } from '../../services/signalr.service';
import { Stream } from '../../models/stream.model';
import { AddStreamComponent } from '../add-stream/add-stream';

@Component({
  selector: 'app-stream-list',
  standalone: true,
  imports: [CommonModule, FormsModule, AddStreamComponent],
  templateUrl: './stream-list.html',
  styleUrl: './stream-list.scss'
})
export class StreamListComponent implements OnInit, OnDestroy {
  private readonly streamService = inject(StreamService);
  private readonly signalR = inject(SignalRService);

  streams = signal<Stream[]>([]);
  showAddModal = signal(false);
  checkingIds = signal<Set<string>>(new Set());
  tagFilter = signal('');

  filteredStreams = computed(() => {
    const tag = this.tagFilter().trim().toLowerCase();
    const all = this.streams();
    if (!tag) return all;
    return all.filter(s => s.tags?.toLowerCase().split(',').map(t => t.trim()).includes(tag));
  });

  allTags = computed(() => {
    const tags = new Set<string>();
    for (const s of this.streams()) {
      s.tags?.split(',').map(t => t.trim()).filter(Boolean).forEach(t => tags.add(t));
    }
    return [...tags].sort();
  });

  private unsubscribe!: () => void;

  ngOnInit() {
    this.load();
    this.unsubscribe = this.signalR.onStreamUpdate(updated => {
      this.streams.update(list => {
        const idx = list.findIndex(s => s.id === updated.id);
        if (idx >= 0) {
          const next = [...list];
          next[idx] = { ...list[idx], ...updated };
          return this.sortStreams(next);
        }
        return list;
      });

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
        this.streams.update(list => this.sortStreams(list.map(s => s.id === updated.id ? updated : s)));
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

  formatBitrate(bps?: number): string {
    if (!bps) return '—';
    if (bps >= 1_000_000) return `${(bps / 1_000_000).toFixed(1)} Mbps`;
    return `${Math.round(bps / 1000)} kbps`;
  }

  isChecking(id: string): boolean {
    return this.checkingIds().has(id);
  }

  private sortStreams(list: Stream[]): Stream[] {
    const order = { Live: 0, Checking: 1, Unknown: 2, Offline: 3 };
    return [...list].sort((a, b) => (order[a.status] ?? 4) - (order[b.status] ?? 4));
  }
}
