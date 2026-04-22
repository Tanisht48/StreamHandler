import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AddStreamRequest, StatusSummary, Stream, StreamHistory } from '../models/stream.model';
import { environment } from '../../environments/environment';

@Injectable({ providedIn: 'root' })
export class StreamService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiUrl;

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

  getStreamHistory(id: string, limit = 50): Observable<StreamHistory> {
    return this.http.get<StreamHistory>(`${this.base}/streams/${id}/history?limit=${limit}`);
  }
}
