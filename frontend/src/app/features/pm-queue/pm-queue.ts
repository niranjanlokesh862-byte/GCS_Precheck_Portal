import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TicketService } from '../../core/services/ticket.service';
import { AuthService } from '../../core/services/auth.service';
import { PmQueueItemDto, CustomerClassification } from '../../core/models/ticket.models';

@Component({
  selector: 'app-pm-queue',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './pm-queue.html',
  styleUrls: ['./pm-queue.scss']
})
export class PmQueueComponent implements OnInit {
  private ticketService = inject(TicketService);
  public authService = inject(AuthService);

  queueItems = signal<PmQueueItemDto[]>([]);
  selectedTicket = signal<PmQueueItemDto | null>(null);
  decidedClassification = signal<CustomerClassification>('ManagedServices');
  notes = signal<string>('');

  errorMessage = signal<string | null>(null);
  successMessage = signal<string | null>(null);

  ngOnInit() {
    this.loadQueue();
  }

  loadQueue() {
    this.ticketService.getPmQueue().subscribe({
      next: (data) => this.queueItems.set(data),
      error: (err) => this.errorMessage.set(err.error?.message || 'Failed to fetch PM queue.')
    });
  }

  selectTicket(item: PmQueueItemDto) {
    this.selectedTicket.set(item);
    this.decidedClassification.set(item.classification === 'Unknown' ? 'ManagedServices' : item.classification);
    this.notes.set('');
  }

  submitDecision(isApproved: boolean) {
    const item = this.selectedTicket();
    if (!item) return;

    if (!this.notes().trim()) {
      this.errorMessage.set('Approval or rejection notes are mandatory for auditing.');
      return;
    }

    this.ticketService.submitPmDecision(
      item.ticketId,
      this.decidedClassification(),
      isApproved,
      this.notes().trim(),
      this.authService.currentUserId()
    ).subscribe({
      next: () => {
        this.successMessage.set(`Decision submitted for ticket ${item.ticketNumber}.`);
        this.selectedTicket.set(null);
        this.loadQueue();
        setTimeout(() => this.successMessage.set(null), 3000);
      },
      error: (err) => {
        this.errorMessage.set(err.error?.message || 'Failed to submit PM decision.');
      }
    });
  }
}
