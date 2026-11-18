import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { TicketDetailDto, TicketRequirementItemDto, PmQueueItemDto, CustomerClassification } from '../models/ticket.models';

@Injectable({
  providedIn: 'root'
})
export class TicketService {
  private http = inject(HttpClient);
  private apiBaseUrl = '/api';

  uploadTicket(
    ticketNumber: string,
    customerName: string,
    suggestedClassification: CustomerClassification,
    uploadedByUserId: string,
    files: File[]
  ): Observable<TicketDetailDto> {
    const formData = new FormData();
    formData.append('ticketNumber', ticketNumber);
    formData.append('customerName', customerName);
    formData.append('suggestedClassification', suggestedClassification);
    formData.append('uploadedByUserId', uploadedByUserId);

    for (const file of files) {
      formData.append('files', file, file.name);
    }

    return this.http.post<TicketDetailDto>(`${this.apiBaseUrl}/tickets/upload`, formData);
  }

  getTicket(id: string): Observable<TicketDetailDto> {
    return this.http.get<TicketDetailDto>(`${this.apiBaseUrl}/tickets/${id}`);
  }

  verifyField(
    ticketId: string,
    fieldId: string,
    verifiedValue: string,
    isModified: boolean,
    correctionReason: string | null,
    verifiedByUserId: string
  ): Observable<TicketRequirementItemDto> {
    return this.http.put<TicketRequirementItemDto>(
      `${this.apiBaseUrl}/tickets/${ticketId}/fields/${fieldId}/verify`,
      {
        verifiedValue,
        isModified,
        correctionReason,
        verifiedByUserId
      }
    );
  }

  completeReview(ticketId: string, employeeUserId: string): Observable<TicketDetailDto> {
    return this.http.post<TicketDetailDto>(
      `${this.apiBaseUrl}/tickets/${ticketId}/complete-review?employeeUserId=${employeeUserId}`,
      {}
    );
  }

  getAttachmentStreamUrl(ticketId: string, attachmentId: string): string {
    return `${this.apiBaseUrl}/tickets/${ticketId}/attachments/${attachmentId}/stream`;
  }

  getPmQueue(): Observable<PmQueueItemDto[]> {
    return this.http.get<PmQueueItemDto[]>(`${this.apiBaseUrl}/pm/queue`);
  }

  submitPmDecision(
    ticketId: string,
    decidedClassification: CustomerClassification,
    isApproved: boolean,
    approvalNotes: string,
    pmUserId: string
  ): Observable<any> {
    return this.http.post(`${this.apiBaseUrl}/pm/tickets/${ticketId}/decision`, {
      decidedClassification,
      isApproved,
      approvalNotes,
      pmUserId
    });
  }
}
