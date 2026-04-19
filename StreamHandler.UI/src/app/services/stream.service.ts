import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AddStreamRequest, StatusSummary, Stream } from '../models/stream.model';

@Injectable({ providedIn: 'root' })
export class StreamService {
  private readonly http = inject(HttpClient);
  private readonly base = 'http://localhost:5000';

  getStreams(): Observable<Stream[]> {
    return this.http.get<Stream[]>(`${this.base}/streams`);
  }

  getStream(id: string): Observable<Stream> {
    return this.http.get<Stream>(`${this.base}/streams/${id}`);
  }

  addStream(req: AddStreamRequest): Observable<Stream> {
    return this.http.post<Stream>(`${this.base}/streams`, req);
  }

  deleteStream(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/streams/${id}`);
  }

  checkStream(id: string): Observable<Stream> {
    return this.http.post<Stream>(`${this.base}/streams/${id}/check`, {});
  }

  getStatus(): Observable<StatusSummary> {
    return this.http.get<StatusSummary>(`${this.base}/status`);
  }
}
