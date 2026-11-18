import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { TicketService } from '../../core/services/ticket.service';
import { TicketSummaryDto } from '../../core/models/ticket.models';

@Component({
  selector: 'app-ticket-history',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './ticket-history.html',
  styleUrls: ['./ticket-history.scss']
})
export class TicketHistoryComponent implements OnInit {
  private ticketService = inject(TicketService);

  public tickets = signal<TicketSummaryDto[]>([]);
  public searchTerm = signal<string>('');
  public selectedClassification = signal<string>('ALL');
  public isLoading = signal<boolean>(true);
  public errorMessage = signal<string | null>(null);

  public filteredTickets = computed(() => {
    const term = this.searchTerm().toLowerCase().trim();
    const classification = this.selectedClassification();

    return this.tickets().filter(t => {
      const matchSearch = !term || 
        t.ticketNumber.toLowerCase().includes(term) || 
        t.customerName.toLowerCase().includes(term);

      const matchClass = classification === 'ALL' || t.customerClassification === classification;

      return matchSearch && matchClass;
    });
  });

  ngOnInit(): void {
    this.loadTickets();
  }

  public loadTickets(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.ticketService.getAllTickets().subscribe({
      next: (data) => {
        this.tickets.set(data);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.errorMessage.set(err.error?.message || 'Failed to load ticket history.');
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
