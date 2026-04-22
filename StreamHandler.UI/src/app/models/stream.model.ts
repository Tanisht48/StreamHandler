export type StreamFormat = 'Unknown' | 'HLS' | 'RTSP' | 'YouTube' | 'HTTP';
export type StreamStatus = 'Unknown' | 'Live' | 'Offline' | 'Checking';

export interface Stream {
  id: string;
  name: string;
  url: string;
  format: StreamFormat;
  status: StreamStatus;
  lastCheckedAt: string;
  lastSeenLiveAt?: string;
  tags: string;
  codec?: string;
  resolution?: string;
  bitrateBps?: number;
}

export interface StatusSummary {
  totalStreams: number;
  live: number;
  offline: number;
  checking: number;
  unknown: number;
  lastHealthCheckRanAt?: string;
  serverTimeUtc: string;
}

export interface AddStreamRequest {
  url: string;
  name?: string;
  tags?: string;
}

export interface StreamEvent {
  id: string;
  oldStatus: StreamStatus;
  newStatus: StreamStatus;
  occurredAt: string;
}

export interface StreamHistory {
  uptimePercent: number;
  events: StreamEvent[];
}

export interface StreamAlert {
  id: string;
  name: string;
  message: string;
  occurredAt: string;
}
