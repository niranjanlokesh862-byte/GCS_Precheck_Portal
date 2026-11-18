export type CustomerClassification = 'eLite' | 'ManagedServices' | 'Unknown' | 'OutOfScope';
export type PmRoutingStatus = 'NotRequired' | 'PendingPmApproval' | 'ApprovedByPm' | 'RejectedByPm';
export type FieldVerificationStatus = 'PendingReview' | 'Verified' | 'Modified' | 'Rejected';
export type VerificationStatus = 'PendingVerification' | 'RequirementsVerified' | 'Rejected';

export interface TicketRequirementItemDto {
  id: string;
  parameterCode: string;
  scopeQualifier: string;
  extractedRawText: string | null;
  ocrConfidenceScore: number | null;
  employeeVerifiedValue: string | null;
  fieldVerificationStatus: FieldVerificationStatus;
  verifiedByUserId: string | null;
  verifiedByUserName: string | null;
  verifiedAtUtc: string | null;
  correctionReason: string | null;
}

export interface TicketAttachmentDto {
  id: string;
  originalFileName: string;
  storageRelativePath: string;
  fileSizeBytes: number;
  mimeType: string;
  sha256Checksum: string;
  antivirusScanStatus: string;
  ocrProcessingStatus: string;
  uploadedAtUtc: string;
}

export interface TicketDetailDto {
  id: string;
  ticketNumber: string;
  customerName: string;
  customerClassification: CustomerClassification;
  pmRoutingStatus: PmRoutingStatus;
  pmApprovedByUserName: string | null;
  pmApprovedAtUtc: string | null;
  pmApprovalNotes: string | null;
  ocrOverallConfidence: number | null;
  verificationStatus: VerificationStatus;
  verifiedByUserName: string | null;
  verifiedAtUtc: string | null;
  createdAtUtc: string;
  attachments: TicketAttachmentDto[];
  requirementItems: TicketRequirementItemDto[];
}

export interface PmQueueItemDto {
  ticketId: string;
  ticketNumber: string;
  customerName: string;
  classification: CustomerClassification;
  pmStatus: PmRoutingStatus;
  screenshotCount: number;
  ocrOverallConfidence: number | null;
  uploadedAtUtc: string;
}

export interface TicketSummaryDto {
  id: string;
  ticketNumber: string;
  customerName: string;
  customerClassification: CustomerClassification;
  pmRoutingStatus: PmRoutingStatus;
  verificationStatus: VerificationStatus;
  totalRequirements: number;
  verifiedRequirements: number;
  attachmentCount: number;
  createdAtUtc: string;
}

export interface DashboardMetricsDto {
  totalTickets: number;
  verifiedTickets: number;
  pendingPmApprovals: number;
  totalRuns: number;
  passedRuns: number;
  failedRuns: number;
}
