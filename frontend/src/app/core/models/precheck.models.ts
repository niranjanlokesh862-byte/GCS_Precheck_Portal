export type ExecutionStatus =
  | 'QUEUED'
  | 'WAITING_FOR_PM'
  | 'RUNNING'
  | 'WAITING_FOR_MFA'
  | 'COMPLETED'
  | 'FAILED'
  | 'EXPIRED'
  | 'CANCELLED';

export type TargetEnvironment = 'Production' | 'Staging' | 'IsolatedLabMock';

export interface StartPreCheckRequest {
  ticketId: string;
  studioUrl: string;
  username: string;
  password: string;
  environment: TargetEnvironment;
  executedByUserId: string;
}

export interface PreCheckJobResponse {
  message: string;
  jobId: string;
  sessionNonce: string;
  status: ExecutionStatus;
}

export interface PreCheckJobStatus {
  jobId: string;
  ticketId: string;
  studioUrl: string;
  executionStatus: ExecutionStatus;
  leasedByWorkerId?: string;
  leaseExpiresAtUtc?: string;
  completedAtUtc?: string;
  errorMessage?: string;
  runId?: string;
  overallResult?: string;
}

export interface LogEntry {
  message: string;
  level: string;
  timestamp: string;
}

export interface MfaChallengeInfo {
  challengeId: string;
  challengeType: string;
  expiresAtUtc: string;
}
