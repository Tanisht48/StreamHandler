import { Component, EventEmitter, Output, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { StreamService } from '../../services/stream.service';
import { Stream } from '../../models/stream.model';

@Component({
  selector: 'app-add-stream',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './add-stream.html',
  styleUrl: './add-stream.scss'
})
export class AddStreamComponent {
  @Output() streamAdded = new EventEmitter<Stream>();
  @Output() cancelled = new EventEmitter<void>();

  private readonly streamService = inject(StreamService);

  url = '';
  name = '';
  saving = signal(false);
  error = signal('');

  submit() {
    if (!this.url.trim()) { this.error.set('URL is required'); return; }
    this.saving.set(true);
    this.error.set('');

    this.streamService.addStream({ url: this.url.trim(), name: this.name.trim() || undefined })
      .subscribe({
        next: stream => {
          this.saving.set(false);
          this.streamAdded.emit(stream);
        },
        error: err => {
          this.saving.set(false);
          this.error.set(err?.error?.title ?? 'Failed to add stream');
        }
      });
  }
}
