import { Component, OnInit, OnDestroy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { PreCheckService } from '../../core/services/precheck.service';
import { PreCheckSignalRService } from '../../core/services/precheck-signalr.service';
import { AuthService } from '../../core/services/auth.service';
import { TargetEnvironment, StartPreCheckRequest, ExecutionStatus } from '../../core/models/precheck.models';

@Component({
  selector: 'app-precheck-console',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './precheck-console.html',
  styleUrls: ['./precheck-console.scss']
})
export class PreCheckConsoleComponent implements OnInit, OnDestroy {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private preCheckService = inject(PreCheckService);
  public signalRService = inject(PreCheckSignalRService);
  public authService = inject(AuthService);

  public ticketId = signal<string>('');
  public jobId = signal<string | null>(null);
  public isSubmitting = signal<boolean>(false);
  public launchError = signal<string | null>(null);

  // Form Fields
  public studioUrl = signal<string>('');
  public username = signal<string>('');
  public password = signal<string>('');
  public environment = signal<TargetEnvironment>('Production');

  // MFA modal state
  public otpCode = signal<string>('');
  public mfaCountdownSeconds = signal<number>(180);
  private countdownTimer: any = null;

  public logs = this.signalRService.logs;
  public progress = this.signalRService.progress;
  public mfaChallenge = this.signalRService.mfaChallenge;
  public completed = this.signalRService.completed;
  public signalRError = this.signalRService.error;

  public isJobActive = computed(() => this.jobId() !== null);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('ticketId');
    if (id) {
      this.ticketId.set(id);
    }
  }

  public async startAutomation(): Promise<void> {
    if (!this.studioUrl() || !this.username() || !this.password()) {
      this.launchError.set('Studio URL, Username, and Password are all required.');
      return;
    }

    this.isSubmitting.set(true);
    this.launchError.set(null);

    const request: StartPreCheckRequest = {
      ticketId: this.ticketId(),
      studioUrl: this.studioUrl().trim(),
      username: this.username().trim(),
      password: this.password(),
      environment: this.environment(),
      executedByUserId: this.authService.currentUserId()
    };

    this.preCheckService.startPreCheck(request).subscribe({
      next: async (res) => {
        this.jobId.set(res.jobId);
        this.isSubmitting.set(false);
        // Clear sensitive password from frontend state immediately after sending
        this.password.set('');

        // Connect to SignalR telemetric stream
        await this.signalRService.startConnection(res.jobId);
        this.startMfaWatcher();
      },
      error: (err) => {
        this.isSubmitting.set(false);
        const errorMsg = err.error?.message || err.message || 'Failed to queue pre-check automation.';
        this.launchError.set(errorMsg);
      }
    });
  }

  private startMfaWatcher(): void {
    // Monitor for MFA challenge arrival to start countdown
    const checkInterval = setInterval(() => {
      const challenge = this.mfaChallenge();
      if (challenge && !this.countdownTimer) {
        this.startCountdown(challenge.expiresAtUtc);
      }
      if (this.completed() || this.signalRError()) {
        clearInterval(checkInterval);
        this.stopCountdown();
      }
    }, 500);
  }

  private startCountdown(expiresAtUtcStr: string): void {
    const expiresAt = new Date(expiresAtUtcStr).getTime();
    this.updateCountdown(expiresAt);

    this.countdownTimer = setInterval(() => {
      this.updateCountdown(expiresAt);
    }, 1000);
  }

  private updateCountdown(expiresAt: number): void {
    const diff = Math.max(0, Math.floor((expiresAt - Date.now()) / 1000));
    this.mfaCountdownSeconds.set(diff);
    if (diff <= 0) {
      this.stopCountdown();
    }
  }

  private stopCountdown(): void {
    if (this.countdownTimer) {
      clearInterval(this.countdownTimer);
      this.countdownTimer = null;
    }
  }

  public async submitOtp(): Promise<void> {
    const challenge = this.mfaChallenge();
    const code = this.otpCode().trim();
    if (!challenge || !code) return;

    try {
      await this.signalRService.submitMfaOtp(challenge.challengeId, code);
      this.otpCode.set('');
      this.stopCountdown();
    } catch (err: any) {
      this.launchError.set(`MFA Submission failed: ${err.message}`);
    }
  }

  public resetConsole(): void {
    this.jobId.set(null);
    this.signalRService.disconnect();
    this.launchError.set(null);
  }

  ngOnDestroy(): void {
    this.stopCountdown();
    this.signalRService.disconnect();
  }
}
