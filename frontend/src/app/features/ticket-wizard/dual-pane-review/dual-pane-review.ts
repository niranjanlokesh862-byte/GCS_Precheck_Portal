import { Component, Input, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { TicketService } from '../../../core/services/ticket.service';
import { AuthService } from '../../../core/services/auth.service';
import { TicketDetailDto, TicketRequirementItemDto, TicketAttachmentDto } from '../../../core/models/ticket.models';

@Component({
  selector: 'app-dual-pane-review',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './dual-pane-review.html',
  styleUrls: ['./dual-pane-review.scss']
})
export class DualPaneReviewComponent implements OnInit {
  private ticketService = inject(TicketService);
  public authService = inject(AuthService);

  @Input({ required: true }) ticketId!: string;
  @Input() currentUserId: string = '';

  private get effectiveUserId(): string {
    return this.currentUserId || this.authService.currentUserId();
  }

  ticket = signal<TicketDetailDto | null>(null);
  selectedAttachment = signal<TicketAttachmentDto | null>(null);
  editingItemId = signal<string | null>(null);
  editValue = signal<string>('');
  editReason = signal<string>('');
  errorMessage = signal<string | null>(null);
  successMessage = signal<string | null>(null);

  ngOnInit() {
    this.loadTicket();
  }

  loadTicket() {
    this.ticketService.getTicket(this.ticketId).subscribe({
      next: (data) => {
        this.ticket.set(data);
        if (data.attachments.length > 0 && !this.selectedAttachment()) {
          this.selectedAttachment.set(data.attachments[0]);
        }
      },
      error: (err) => {
        this.errorMessage.set(err.error?.message || 'Failed to load ticket details.');
      }
    });
  }

  selectAttachment(attachment: TicketAttachmentDto) {
    this.selectedAttachment.set(attachment);
  }

  getAttachmentUrl(attachmentId: string): string {
    return this.ticketService.getAttachmentStreamUrl(this.ticketId, attachmentId);
  }

  startEditing(item: TicketRequirementItemDto) {
    this.editingItemId.set(item.id);
    this.editValue.set(item.employeeVerifiedValue || item.extractedRawText || '');
    this.editReason.set(item.correctionReason || '');
  }

  cancelEditing() {
    this.editingItemId.set(null);
  }

  saveVerification(item: TicketRequirementItemDto, isModified: boolean) {
    const val = isModified ? this.editValue() : (item.employeeVerifiedValue || item.extractedRawText || '');
    const reason = isModified ? this.editReason() : null;

    this.ticketService.verifyField(
      this.ticketId,
      item.id,
      val,
      isModified,
      reason,
      this.effectiveUserId
    ).subscribe({
      next: () => {
        this.editingItemId.set(null);
        this.loadTicket();
        this.successMessage.set(`Parameter ${item.parameterCode} verified successfully.`);
        setTimeout(() => this.successMessage.set(null), 3000);
      },
      error: (err) => {
        this.errorMessage.set(err.error?.message || 'Failed to verify parameter.');
      }
    });
  }

  completeReview() {
    this.ticketService.completeReview(this.ticketId, this.effectiveUserId).subscribe({
      next: (updated) => {
        this.ticket.set(updated);
        this.successMessage.set('All requirement parameters have been verified and locked.');
      },
      error: (err) => {
        this.errorMessage.set(err.error?.message || 'Cannot complete review.');
      }
    });
  }

  get verifiedCount(): number {
    return this.ticket()?.requirementItems.filter(r => r.fieldVerificationStatus !== 'PendingReview').length || 0;
  }

  get totalCount(): number {
    return this.ticket()?.requirementItems.length || 0;
  }

  get isReviewComplete(): boolean {
    return this.totalCount > 0 && this.verifiedCount === this.totalCount;
  }

  get isPmLocked(): boolean {
    return this.ticket()?.pmRoutingStatus === 'PendingPmApproval';
  }
}
