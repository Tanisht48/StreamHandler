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
}
