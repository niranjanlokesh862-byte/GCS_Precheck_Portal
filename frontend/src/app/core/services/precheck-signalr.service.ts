import { Injectable, signal } from '@angular/core';
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { LogEntry, MfaChallengeInfo } from '../models/precheck.models';

@Injectable({
  providedIn: 'root'
})
export class PreCheckSignalRService {
  private hubConnection: HubConnection | null = null;

  public readonly logs = signal<LogEntry[]>([]);
  public readonly progress = signal<{ percent: number; step: string }>({ percent: 0, step: 'Idle' });
  public readonly mfaChallenge = signal<MfaChallengeInfo | null>(null);
  public readonly completed = signal<{ runId: string; overallResult: string } | null>(null);
  public readonly error = signal<string | null>(null);
  public readonly isConnected = signal<boolean>(false);

  public async startConnection(jobId: string): Promise<void> {
    await this.disconnect();

    this.logs.set([]);
    this.progress.set({ percent: 0, step: 'Connecting to automation monitor...' });
    this.mfaChallenge.set(null);
    this.completed.set(null);
    this.error.set(null);

    this.hubConnection = new HubConnectionBuilder()
      .withUrl('/hubs/precheck')
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Information)
      .build();

    this.hubConnection.on('OnLogEvent', (message: string, level: string, timestamp: string) => {
      this.logs.update(current => [...current, { message, level, timestamp }]);
    });

    this.hubConnection.on('OnProgressUpdate', (percent: number, currentStep: string) => {
      this.progress.set({ percent, step: currentStep });
    });

    this.hubConnection.on('OnMfaRequired', (challengeId: string, challengeType: string, expiresAtUtc: string) => {
      this.mfaChallenge.set({ challengeId, challengeType, expiresAtUtc });
      this.logs.update(current => [
        ...current,
        {
          message: `ACTION REQUIRED: MFA challenge (${challengeType}) encountered. Please enter OTP.`,
          level: 'WARN',
          timestamp: new Date().toISOString()
        }
      ]);
    });

    this.hubConnection.on('OnExecutionCompleted', (runId: string, overallResult: string) => {
      this.completed.set({ runId, overallResult });
      this.progress.set({ percent: 100, step: `Completed: ${overallResult}` });
      this.logs.update(current => [
        ...current,
        {
          message: `Automation execution completed successfully. Overall Result: ${overallResult}`,
          level: 'SUCCESS',
          timestamp: new Date().toISOString()
        }
      ]);
    });

    this.hubConnection.on('OnExecutionFailed', (errorMessage: string) => {
      this.error.set(errorMessage);
      this.progress.set({ percent: 100, step: `Failed: ${errorMessage}` });
      this.logs.update(current => [
        ...current,
        {
          message: `Automation execution failed: ${errorMessage}`,
          level: 'ERROR',
          timestamp: new Date().toISOString()
        }
      ]);
    });

    try {
      await this.hubConnection.start();
      this.isConnected.set(true);
      await this.hubConnection.invoke('JoinJobGroup', jobId);
      this.logs.update(current => [
        ...current,
        {
          message: `Subscribed to live telemetric stream for job ${jobId}.`,
          level: 'INFO',
          timestamp: new Date().toISOString()
        }
      ]);
    } catch (err: any) {
      this.isConnected.set(false);
      this.error.set(`SignalR connection failed: ${err.message}`);
      console.error('SignalR Hub Connection Error: ', err);
    }
  }

  public async submitMfaOtp(challengeId: string, otpCode: string): Promise<void> {
    if (!this.hubConnection || this.hubConnection.state !== 'Connected') {
      throw new Error('SignalR Hub is not connected.');
    }

    await this.hubConnection.invoke('SubmitMfaOtp', challengeId, otpCode);
    this.mfaChallenge.set(null);
    this.logs.update(current => [
      ...current,
      {
        message: 'MFA OTP submitted. Resuming Studio automation...',
        level: 'INFO',
        timestamp: new Date().toISOString()
      }
    ]);
  }

  public async disconnect(): Promise<void> {
    if (this.hubConnection) {
      try {
        await this.hubConnection.stop();
      } catch { }
      this.hubConnection = null;
      this.isConnected.set(false);
    }
  }
}
