import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { TicketService } from '../../core/services/ticket.service';
import { AuthService } from '../../core/services/auth.service';
import { DashboardMetricsDto, TicketSummaryDto } from '../../core/models/ticket.models';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './dashboard.html',
  styleUrls: ['./dashboard.scss']
})
export class DashboardComponent implements OnInit {
  private ticketService = inject(TicketService);
  public authService = inject(AuthService);

  public metrics = signal<DashboardMetricsDto>({
    totalTickets: 0,
    verifiedTickets: 0,
    pendingPmApprovals: 0,
    totalRuns: 0,
    passedRuns: 0,
    failedRuns: 0
  });

  public recentTickets = signal<TicketSummaryDto[]>([]);
  public isLoading = signal<boolean>(true);
  public errorMessage = signal<string | null>(null);

  ngOnInit(): void {
    this.loadDashboardData();
  }

  public loadDashboardData(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.ticketService.getDashboardMetrics().subscribe({
      next: (m) => this.metrics.set(m),
      error: () => {
        // Fallback for offline/local mode
        this.metrics.set({
          totalTickets: 0,
          verifiedTickets: 0,
          pendingPmApprovals: 0,
          totalRuns: 0,
          passedRuns: 0,
          failedRuns: 0
        });
      }
    });

    this.ticketService.getAllTickets().subscribe({
      next: (tickets) => {
        this.recentTickets.set(tickets);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.errorMessage.set(err.error?.message || 'Failed to load tickets. Configure SQL Server connection to view records.');
        this.isLoading.set(false);
      }
    });
  }

  public getStatusClass(status: string): string {
    switch (status) {
      case 'RequirementsVerified':
      case 'ApprovedByPm':
      case 'PASS':
        return 'badge-pass';
      case 'Rejected':
      case 'RejectedByPm':
      case 'FAIL':
        return 'badge-fail';
      case 'PendingPmApproval':
      case 'PendingVerification':
      case 'NEEDS_REVIEW':
        return 'badge-review';
      default:
        return 'badge-default';
    }
  }
}
