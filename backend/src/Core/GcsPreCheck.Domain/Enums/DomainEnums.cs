namespace GcsPreCheck.Domain.Enums;

public enum ExecutionStatus
{
    QUEUED,
    WAITING_FOR_PM,
    RUNNING,
    WAITING_FOR_MFA,
    COMPLETED,
    FAILED,
    EXPIRED,
    CANCELLED
}

public enum OverallResult
{
    PASS,
    FAIL,
    NOT_VERIFIED,
    NOT_APPLICABLE,
    NEEDS_REVIEW
}

public enum ComparisonStatus
{
    PASS,
    FAIL,
    NOT_VERIFIED,
    NOT_APPLICABLE,
    NEEDS_REVIEW
}

public enum CustomerClassification
{
    eLite,
    ManagedServices,
    Unknown,
    OutOfScope
}

public enum PmRoutingStatus
{
    NotRequired,
    PendingPmApproval,
    ApprovedByPm,
    RejectedByPm
}

public enum FieldVerificationStatus
{
    PendingReview,
    Verified,
    Modified,
    Rejected
}

public enum VerificationStatus
{
    PendingVerification,
    RequirementsVerified,
    Rejected
}

public enum AttachmentType
{
    CustomerTicketScreenshot,
    StudioEvidenceSnapshot
}

public enum AntivirusScanStatus
{
    Clean,
    Infected,
    ScanFailed
}

public enum OcrProcessingStatus
{
    Pending,
    Processed,
    Failed,
    Skipped
}

public enum UserRole
{
    Admin,
    LeadSpecialist,
    Operator,
    ProjectManager
}

public enum TargetEnvironment
{
    Production,
    Staging,
    IsolatedLabMock
}

public enum AllowlistEntryType
{
    ExactIp,
    CidrSubnet,
    FqdnPattern
}
