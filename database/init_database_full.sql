-- ==========================================================================================
-- GCS PRE-CHECK AUTOMATION PORTAL — ALL-IN-ONE INITIALIZATION SCRIPT v2.4
-- Target RDBMS: Microsoft SQL Server 2019 / 2022 / Azure SQL Database
-- Security Classification: Highly Confidential — Internal Operational Tooling
-- Execution Mode: Manual DBA Script (Idempotent - Schema + Seed Data)
-- ==========================================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

PRINT '==========================================================================================';
PRINT 'Starting GCS Pre-Check Automation Portal Database Initialization (v2.4)...';
PRINT '==========================================================================================';
GO

-- PART 1: SCHEMA DDL
-- Ensure schema 'dbo' exists
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'dbo')
BEGIN
    EXEC('CREATE SCHEMA [dbo]');
END
GO

-- 1. TABLE: dbo.ApplicationUsers
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ApplicationUsers]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[ApplicationUsers] (
        [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_ApplicationUsers_Id] DEFAULT (NEWID()),
        [Username] NVARCHAR(100) NOT NULL,
        [Email] NVARCHAR(255) NOT NULL,
        [FullName] NVARCHAR(150) NOT NULL,
        [AuthProvider] NVARCHAR(50) NOT NULL CONSTRAINT [DF_ApplicationUsers_AuthProvider] DEFAULT ('LocalIdentity'),
        [Role] NVARCHAR(50) NOT NULL,
        [IsActive] BIT NOT NULL CONSTRAINT [DF_ApplicationUsers_IsActive] DEFAULT (1),
        [CreatedAtUtc] DATETIME2(3) NOT NULL CONSTRAINT [DF_ApplicationUsers_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_ApplicationUsers] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [UQ_ApplicationUsers_Username] UNIQUE NONCLUSTERED ([Username] ASC),
        CONSTRAINT [UQ_ApplicationUsers_Email] UNIQUE NONCLUSTERED ([Email] ASC),
        CONSTRAINT [CK_ApplicationUsers_Role] CHECK ([Role] IN ('Admin', 'LeadSpecialist', 'Operator', 'ProjectManager'))
    );
END
GO

-- 2. TABLE: dbo.CustomerTickets
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[CustomerTickets]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[CustomerTickets] (
        [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_CustomerTickets_Id] DEFAULT (NEWID()),
        [TicketNumber] NVARCHAR(100) NOT NULL,
        [CustomerName] NVARCHAR(200) NOT NULL,
        [CustomerClassification] NVARCHAR(50) NOT NULL,
        [PmRoutingStatus] NVARCHAR(50) NOT NULL CONSTRAINT [DF_CustomerTickets_PmRoutingStatus] DEFAULT ('NotRequired'),
        [PmApprovedByUserId] UNIQUEIDENTIFIER NULL,
        [PmApprovedAtUtc] DATETIME2(3) NULL,
        [PmApprovalNotes] NVARCHAR(MAX) NULL,
        [OcrOverallConfidence] DECIMAL(5, 4) NULL,
        [VerificationStatus] NVARCHAR(50) NOT NULL CONSTRAINT [DF_CustomerTickets_VerificationStatus] DEFAULT ('PendingVerification'),
        [VerifiedByUserId] UNIQUEIDENTIFIER NULL,
        [VerifiedAtUtc] DATETIME2(3) NULL,
        [CreatedByUserId] UNIQUEIDENTIFIER NOT NULL,
        [CreatedAtUtc] DATETIME2(3) NOT NULL CONSTRAINT [DF_CustomerTickets_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_CustomerTickets] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [UQ_CustomerTickets_TicketNumber] UNIQUE NONCLUSTERED ([TicketNumber] ASC),
        CONSTRAINT [CK_CustomerTickets_Classification] CHECK ([CustomerClassification] IN ('eLite', 'ManagedServices', 'Unknown', 'OutOfScope')),
        CONSTRAINT [CK_CustomerTickets_PmRoutingStatus] CHECK ([PmRoutingStatus] IN ('NotRequired', 'PendingPmApproval', 'ApprovedByPm', 'RejectedByPm')),
        CONSTRAINT [CK_CustomerTickets_VerificationStatus] CHECK ([VerificationStatus] IN ('PendingVerification', 'RequirementsVerified', 'Rejected')),
        CONSTRAINT [FK_CustomerTickets_ApplicationUsers_CreatedBy] FOREIGN KEY ([CreatedByUserId]) REFERENCES [dbo].[ApplicationUsers] ([Id]),
        CONSTRAINT [FK_CustomerTickets_ApplicationUsers_VerifiedBy] FOREIGN KEY ([VerifiedByUserId]) REFERENCES [dbo].[ApplicationUsers] ([Id]),
        CONSTRAINT [FK_CustomerTickets_ApplicationUsers_PmApprovedBy] FOREIGN KEY ([PmApprovedByUserId]) REFERENCES [dbo].[ApplicationUsers] ([Id])
    );
END
GO

-- 3. TABLE: dbo.TicketAttachments
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[TicketAttachments]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[TicketAttachments] (
        [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_TicketAttachments_Id] DEFAULT (NEWID()),
        [TicketId] UNIQUEIDENTIFIER NOT NULL,
        [AttachmentType] NVARCHAR(50) NOT NULL,
        [OriginalFileName] NVARCHAR(255) NOT NULL,
        [StorageRelativePath] NVARCHAR(500) NOT NULL,
        [FileSizeBytes] BIGINT NOT NULL,
        [MimeType] NVARCHAR(100) NOT NULL,
        [Sha256Checksum] CHAR(64) NOT NULL,
        [ImageWidth] INT NULL,
        [ImageHeight] INT NULL,
        [AntivirusScanStatus] NVARCHAR(30) NOT NULL CONSTRAINT [DF_TicketAttachments_AvStatus] DEFAULT ('Clean'),
        [OcrProcessingStatus] NVARCHAR(30) NOT NULL CONSTRAINT [DF_TicketAttachments_OcrStatus] DEFAULT ('Pending'),
        [OcrConfidenceAverage] DECIMAL(5, 4) NULL,
        [UploadedByUserId] UNIQUEIDENTIFIER NOT NULL,
        [UploadedAtUtc] DATETIME2(3) NOT NULL CONSTRAINT [DF_TicketAttachments_UploadedAtUtc] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_TicketAttachments] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [CK_TicketAttachments_Type] CHECK ([AttachmentType] IN ('CustomerTicketScreenshot', 'StudioEvidenceSnapshot')),
        CONSTRAINT [CK_TicketAttachments_AvStatus] CHECK ([AntivirusScanStatus] IN ('Clean', 'Infected', 'ScanFailed')),
        CONSTRAINT [CK_TicketAttachments_OcrStatus] CHECK ([OcrProcessingStatus] IN ('Pending', 'Processed', 'Failed', 'Skipped')),
        CONSTRAINT [FK_TicketAttachments_CustomerTickets] FOREIGN KEY ([TicketId]) REFERENCES [dbo].[CustomerTickets] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_TicketAttachments_ApplicationUsers] FOREIGN KEY ([UploadedByUserId]) REFERENCES [dbo].[ApplicationUsers] ([Id])
    );
END
GO

-- 4. TABLE: dbo.TicketRequirementItems
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[TicketRequirementItems]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[TicketRequirementItems] (
        [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_TicketRequirementItems_Id] DEFAULT (NEWID()),
        [TicketId] UNIQUEIDENTIFIER NOT NULL,
        [ParameterCode] NVARCHAR(50) NOT NULL,
        [ScopeQualifier] NVARCHAR(100) NOT NULL CONSTRAINT [DF_TicketRequirementItems_Scope] DEFAULT ('DEFAULT'),
        [ExtractedRawText] NVARCHAR(500) NULL,
        [OcrConfidenceScore] DECIMAL(5, 4) NULL,
        [EmployeeVerifiedValue] NVARCHAR(500) NULL,
        [FieldVerificationStatus] NVARCHAR(30) NOT NULL CONSTRAINT [DF_TicketRequirementItems_Status] DEFAULT ('PendingReview'),
        [VerifiedByUserId] UNIQUEIDENTIFIER NULL,
        [VerifiedAtUtc] DATETIME2(3) NULL,
        [CorrectionReason] NVARCHAR(500) NULL,
        CONSTRAINT [PK_TicketRequirementItems] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [UQ_TicketRequirementItems_Parameter] UNIQUE NONCLUSTERED ([TicketId] ASC, [ParameterCode] ASC, [ScopeQualifier] ASC),
        CONSTRAINT [CK_TicketRequirementItems_Status] CHECK ([FieldVerificationStatus] IN ('PendingReview', 'Verified', 'Modified', 'Rejected')),
        CONSTRAINT [FK_TicketRequirementItems_CustomerTickets] FOREIGN KEY ([TicketId]) REFERENCES [dbo].[CustomerTickets] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_TicketRequirementItems_ApplicationUsers] FOREIGN KEY ([VerifiedByUserId]) REFERENCES [dbo].[ApplicationUsers] ([Id])
    );
END
GO

-- 5. TABLE: dbo.SopConfigurations
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[SopConfigurations]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[SopConfigurations] (
        [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_SopConfigurations_Id] DEFAULT (NEWID()),
        [ParameterCode] NVARCHAR(50) NOT NULL,
        [SopDocumentId] NVARCHAR(100) NOT NULL,
        [SopTitle] NVARCHAR(200) NOT NULL,
        [SopVersion] NVARCHAR(50) NOT NULL,
        [SopDocumentUrl] NVARCHAR(500) NOT NULL,
        [IsApproved] BIT NOT NULL CONSTRAINT [DF_SopConfigurations_IsApproved] DEFAULT (0),
        [ApprovedByUserId] UNIQUEIDENTIFIER NULL,
        [ApprovedAtUtc] DATETIME2(3) NULL,
        [EffectiveDate] DATE NULL,
        [RetirementDate] DATE NULL,
        [IsActive] BIT NOT NULL CONSTRAINT [DF_SopConfigurations_IsActive] DEFAULT (0),
        CONSTRAINT [PK_SopConfigurations] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [UQ_SopConfigurations_CodeVersion] UNIQUE NONCLUSTERED ([ParameterCode] ASC, [SopVersion] ASC),
        CONSTRAINT [FK_SopConfigurations_ApplicationUsers] FOREIGN KEY ([ApprovedByUserId]) REFERENCES [dbo].[ApplicationUsers] ([Id])
    );

    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'UX_SopConfigurations_ActiveApproved')
    BEGIN
        CREATE UNIQUE NONCLUSTERED INDEX [UX_SopConfigurations_ActiveApproved]
        ON [dbo].[SopConfigurations] ([ParameterCode] ASC)
        WHERE [IsActive] = 1 AND [IsApproved] = 1;
    END
END
GO

-- 6. TABLE: dbo.StudioNetworkAllowlists
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[StudioNetworkAllowlists]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[StudioNetworkAllowlists] (
        [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_StudioNetworkAllowlists_Id] DEFAULT (NEWID()),
        [NetworkPattern] NVARCHAR(255) NOT NULL,
        [EntryType] NVARCHAR(20) NOT NULL,
        [EnvironmentDescription] NVARCHAR(200) NOT NULL,
        [IsProduction] BIT NOT NULL CONSTRAINT [DF_StudioNetworkAllowlists_IsProduction] DEFAULT (1),
        [ApprovedByUserId] UNIQUEIDENTIFIER NOT NULL,
        [ApprovedAtUtc] DATETIME2(3) NOT NULL CONSTRAINT [DF_StudioNetworkAllowlists_ApprovedAtUtc] DEFAULT (SYSUTCDATETIME()),
        [IsActive] BIT NOT NULL CONSTRAINT [DF_StudioNetworkAllowlists_IsActive] DEFAULT (1),
        CONSTRAINT [PK_StudioNetworkAllowlists] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [CK_StudioNetworkAllowlists_Type] CHECK ([EntryType] IN ('ExactIp', 'CidrSubnet', 'FqdnPattern')),
        CONSTRAINT [FK_StudioNetworkAllowlists_ApplicationUsers] FOREIGN KEY ([ApprovedByUserId]) REFERENCES [dbo].[ApplicationUsers] ([Id])
    );
END
GO

-- 7. TABLE: dbo.PreCheckJobQueue
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[PreCheckJobQueue]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[PreCheckJobQueue] (
        [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_PreCheckJobQueue_Id] DEFAULT (NEWID()),
        [TicketId] UNIQUEIDENTIFIER NOT NULL,
        [StudioUrl] NVARCHAR(500) NOT NULL,
        [SessionNonce] UNIQUEIDENTIFIER NOT NULL,
        [TargetEnvironment] NVARCHAR(50) NOT NULL,
        [ExecutionStatus] NVARCHAR(30) NOT NULL CONSTRAINT [DF_PreCheckJobQueue_Status] DEFAULT ('QUEUED'),
        [Priority] INT NOT NULL CONSTRAINT [DF_PreCheckJobQueue_Priority] DEFAULT (10),
        [QueuedAtUtc] DATETIME2(3) NOT NULL CONSTRAINT [DF_PreCheckJobQueue_QueuedAtUtc] DEFAULT (SYSUTCDATETIME()),
        [ScheduledAtUtc] DATETIME2(3) NOT NULL CONSTRAINT [DF_PreCheckJobQueue_ScheduledAtUtc] DEFAULT (SYSUTCDATETIME()),
        [LeasedByWorkerId] NVARCHAR(100) NULL,
        [LeaseExpiresAtUtc] DATETIME2(3) NULL,
        [HeartbeatAtUtc] DATETIME2(3) NULL,
        [CompletedAtUtc] DATETIME2(3) NULL,
        [RetryCount] INT NOT NULL CONSTRAINT [DF_PreCheckJobQueue_RetryCount] DEFAULT (0),
        [ErrorMessage] NVARCHAR(MAX) NULL,
        CONSTRAINT [PK_PreCheckJobQueue] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [CK_PreCheckJobQueue_Status] CHECK ([ExecutionStatus] IN ('QUEUED', 'WAITING_FOR_PM', 'RUNNING', 'WAITING_FOR_MFA', 'COMPLETED', 'FAILED', 'EXPIRED', 'CANCELLED')),
        CONSTRAINT [FK_PreCheckJobQueue_CustomerTickets] FOREIGN KEY ([TicketId]) REFERENCES [dbo].[CustomerTickets] ([Id])
    );

    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_PreCheckJobQueue_Leasing')
    BEGIN
        CREATE NONCLUSTERED INDEX [IX_PreCheckJobQueue_Leasing]
        ON [dbo].[PreCheckJobQueue] ([ExecutionStatus] ASC, [ScheduledAtUtc] ASC)
        INCLUDE ([Priority], [QueuedAtUtc]);
    END
END
GO

-- 8. TABLE: dbo.PreCheckRuns
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[PreCheckRuns]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[PreCheckRuns] (
        [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_PreCheckRuns_Id] DEFAULT (NEWID()),
        [JobId] UNIQUEIDENTIFIER NOT NULL,
        [TicketId] UNIQUEIDENTIFIER NOT NULL,
        [ExecutedByUserId] UNIQUEIDENTIFIER NOT NULL,
        [StudioUrl] NVARCHAR(500) NOT NULL,
        [ResolvedStudioIp] NVARCHAR(50) NOT NULL,
        [MatchedAllowlistId] UNIQUEIDENTIFIER NOT NULL,
        [TargetEnvironment] NVARCHAR(50) NOT NULL,
        [ExecutionStatus] NVARCHAR(30) NOT NULL,
        [OverallResult] NVARCHAR(30) NULL,
        [PassedCount] INT NOT NULL CONSTRAINT [DF_PreCheckRuns_PassedCount] DEFAULT (0),
        [FailedCount] INT NOT NULL CONSTRAINT [DF_PreCheckRuns_FailedCount] DEFAULT (0),
        [NotVerifiedCount] INT NOT NULL CONSTRAINT [DF_PreCheckRuns_NotVerifiedCount] DEFAULT (0),
        [NotApplicableCount] INT NOT NULL CONSTRAINT [DF_PreCheckRuns_NotApplicableCount] DEFAULT (0),
        [NeedsReviewCount] INT NOT NULL CONSTRAINT [DF_PreCheckRuns_NeedsReviewCount] DEFAULT (0),
        [EvidenceStorageDirectory] NVARCHAR(500) NOT NULL,
        [ExecutionDurationMs] INT NULL,
        [ReportPdfRelativePath] NVARCHAR(500) NULL,
        [ErrorMessage] NVARCHAR(MAX) NULL,
        [StartedAtUtc] DATETIME2(3) NOT NULL CONSTRAINT [DF_PreCheckRuns_StartedAtUtc] DEFAULT (SYSUTCDATETIME()),
        [CompletedAtUtc] DATETIME2(3) NULL,
        CONSTRAINT [PK_PreCheckRuns] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [UQ_PreCheckRuns_JobId] UNIQUE NONCLUSTERED ([JobId] ASC),
        CONSTRAINT [CK_PreCheckRuns_Status] CHECK ([ExecutionStatus] IN ('QUEUED', 'WAITING_FOR_PM', 'RUNNING', 'WAITING_FOR_MFA', 'COMPLETED', 'FAILED', 'EXPIRED', 'CANCELLED')),
        CONSTRAINT [CK_PreCheckRuns_OverallResult] CHECK ([OverallResult] IS NULL OR [OverallResult] IN ('PASS', 'FAIL', 'NOT_VERIFIED', 'NOT_APPLICABLE', 'NEEDS_REVIEW')),
        CONSTRAINT [FK_PreCheckRuns_PreCheckJobQueue] FOREIGN KEY ([JobId]) REFERENCES [dbo].[PreCheckJobQueue] ([Id]),
        CONSTRAINT [FK_PreCheckRuns_CustomerTickets] FOREIGN KEY ([TicketId]) REFERENCES [dbo].[CustomerTickets] ([Id]),
        CONSTRAINT [FK_PreCheckRuns_ApplicationUsers] FOREIGN KEY ([ExecutedByUserId]) REFERENCES [dbo].[ApplicationUsers] ([Id]),
        CONSTRAINT [FK_PreCheckRuns_StudioNetworkAllowlists] FOREIGN KEY ([MatchedAllowlistId]) REFERENCES [dbo].[StudioNetworkAllowlists] ([Id])
    );
END
GO

-- 9. TABLE: dbo.PreCheckMfaChallenges
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[PreCheckMfaChallenges]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[PreCheckMfaChallenges] (
        [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_PreCheckMfaChallenges_Id] DEFAULT (NEWID()),
        [JobId] UNIQUEIDENTIFIER NOT NULL,
        [RunId] UNIQUEIDENTIFIER NULL,
        [ChallengeType] NVARCHAR(50) NOT NULL CONSTRAINT [DF_PreCheckMfaChallenges_Type] DEFAULT ('TOTP_6_DIGIT'),
        [SubmissionAttempts] INT NOT NULL CONSTRAINT [DF_PreCheckMfaChallenges_Attempts] DEFAULT (0),
        [IsConsumed] BIT NOT NULL CONSTRAINT [DF_PreCheckMfaChallenges_IsConsumed] DEFAULT (0),
        [ExpiresAtUtc] DATETIME2(3) NOT NULL,
        [CreatedAtUtc] DATETIME2(3) NOT NULL CONSTRAINT [DF_PreCheckMfaChallenges_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_PreCheckMfaChallenges] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_PreCheckMfaChallenges_PreCheckJobQueue] FOREIGN KEY ([JobId]) REFERENCES [dbo].[PreCheckJobQueue] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_PreCheckMfaChallenges_PreCheckRuns] FOREIGN KEY ([RunId]) REFERENCES [dbo].[PreCheckRuns] ([Id])
    );

    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_PreCheckMfaChallenges_Status')
    BEGIN
        CREATE NONCLUSTERED INDEX [IX_PreCheckMfaChallenges_Status]
        ON [dbo].[PreCheckMfaChallenges] ([JobId] ASC, [IsConsumed] ASC, [ExpiresAtUtc] ASC);
    END
END
GO

-- 10. TABLE: dbo.ParameterCheckResults
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ParameterCheckResults]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[ParameterCheckResults] (
        [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_ParameterCheckResults_Id] DEFAULT (NEWID()),
        [RunId] UNIQUEIDENTIFIER NOT NULL,
        [ParameterCode] NVARCHAR(50) NOT NULL,
        [ParameterName] NVARCHAR(100) NOT NULL,
        [TicketExpectedValue] NVARCHAR(500) NOT NULL,
        [StudioActualValue] NVARCHAR(500) NULL,
        [ComparisonStatus] NVARCHAR(30) NOT NULL,
        [IsConditional] BIT NOT NULL CONSTRAINT [DF_ParameterCheckResults_IsConditional] DEFAULT (0),
        [ScopeRuleApplied] NVARCHAR(200) NOT NULL,
        [SopId] UNIQUEIDENTIFIER NULL,
        [EvidenceScreenshotPath] NVARCHAR(500) NULL,
        [DomSnapshotHash] CHAR(64) NULL,
        [EvaluationNotes] NVARCHAR(MAX) NULL,
        [EvaluatedAtUtc] DATETIME2(3) NOT NULL CONSTRAINT [DF_ParameterCheckResults_EvaluatedAtUtc] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_ParameterCheckResults] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [CK_ParameterCheckResults_Status] CHECK ([ComparisonStatus] IN ('PASS', 'FAIL', 'NOT_VERIFIED', 'NOT_APPLICABLE', 'NEEDS_REVIEW')),
        CONSTRAINT [FK_ParameterCheckResults_PreCheckRuns] FOREIGN KEY ([RunId]) REFERENCES [dbo].[PreCheckRuns] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ParameterCheckResults_SopConfigurations] FOREIGN KEY ([SopId]) REFERENCES [dbo].[SopConfigurations] ([Id])
    );

    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_ParameterCheckResults_Run')
    BEGIN
        CREATE NONCLUSTERED INDEX [IX_ParameterCheckResults_Run]
        ON [dbo].[ParameterCheckResults] ([RunId] ASC, [ParameterCode] ASC);
    END
END
GO

-- 11. TABLE: dbo.AuditLogs
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AuditLogs]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[AuditLogs] (
        [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_AuditLogs_Id] DEFAULT (NEWID()),
        [UserId] UNIQUEIDENTIFIER NULL,
        [Action] NVARCHAR(100) NOT NULL,
        [EntityName] NVARCHAR(100) NOT NULL,
        [EntityId] NVARCHAR(100) NOT NULL,
        [AuditPayloadJson] NVARCHAR(MAX) NULL,
        [IpAddress] NVARCHAR(50) NOT NULL,
        [TimestampUtc] DATETIME2(3) NOT NULL CONSTRAINT [DF_AuditLogs_TimestampUtc] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_AuditLogs_ApplicationUsers] FOREIGN KEY ([UserId]) REFERENCES [dbo].[ApplicationUsers] ([Id])
    );

    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_AuditLogs_Entity')
    BEGIN
        CREATE NONCLUSTERED INDEX [IX_AuditLogs_Entity]
        ON [dbo].[AuditLogs] ([EntityName] ASC, [EntityId] ASC, [TimestampUtc] DESC);
    END
END
GO

PRINT '>>> Schema DDL successfully verified/created.';
GO

-- PART 2: SEED DATA
-- 1. SEED APPLICATION USERS
IF NOT EXISTS (SELECT 1 FROM [dbo].[ApplicationUsers] WHERE [Id] = '00000000-0000-0000-0000-000000000001')
BEGIN
    INSERT INTO [dbo].[ApplicationUsers] ([Id], [Username], [Email], [FullName], [AuthProvider], [Role], [IsActive], [CreatedAtUtc])
    VALUES ('00000000-0000-0000-0000-000000000001', 'admin', 'admin@wercsgcs.internal', 'System Administrator', 'LocalIdentity', 'Admin', 1, SYSUTCDATETIME());
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[ApplicationUsers] WHERE [Id] = '00000000-0000-0000-0000-000000000002')
BEGIN
    INSERT INTO [dbo].[ApplicationUsers] ([Id], [Username], [Email], [FullName], [AuthProvider], [Role], [IsActive], [CreatedAtUtc])
    VALUES ('00000000-0000-0000-0000-000000000002', 'lead_specialist', 'lead@wercsgcs.internal', 'Lead Verification Specialist', 'LocalIdentity', 'LeadSpecialist', 1, SYSUTCDATETIME());
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[ApplicationUsers] WHERE [Id] = '00000000-0000-0000-0000-000000000003')
BEGIN
    INSERT INTO [dbo].[ApplicationUsers] ([Id], [Username], [Email], [FullName], [AuthProvider], [Role], [IsActive], [CreatedAtUtc])
    VALUES ('00000000-0000-0000-0000-000000000003', 'operator', 'operator@wercsgcs.internal', 'PreCheck Operations Specialist', 'LocalIdentity', 'Operator', 1, SYSUTCDATETIME());
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[ApplicationUsers] WHERE [Id] = '00000000-0000-0000-0000-000000000004')
BEGIN
    INSERT INTO [dbo].[ApplicationUsers] ([Id], [Username], [Email], [FullName], [AuthProvider], [Role], [IsActive], [CreatedAtUtc])
    VALUES ('00000000-0000-0000-0000-000000000004', 'pm_lead', 'pm@wercsgcs.internal', 'Customer Success Project Manager', 'LocalIdentity', 'ProjectManager', 1, SYSUTCDATETIME());
END
GO

-- 2. SEED IT-APPROVED NETWORK ALLOWLISTS
IF NOT EXISTS (SELECT 1 FROM [dbo].[StudioNetworkAllowlists] WHERE [NetworkPattern] = '192.168.1.155')
BEGIN
    INSERT INTO [dbo].[StudioNetworkAllowlists] ([Id], [NetworkPattern], [EntryType], [EnvironmentDescription], [IsProduction], [ApprovedByUserId], [ApprovedAtUtc], [IsActive])
    VALUES (NEWID(), '192.168.1.155', 'ExactIp', 'Customer On-Premises WERCS GCS Staging Node', 1, '00000000-0000-0000-0000-000000000001', SYSUTCDATETIME(), 1);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[StudioNetworkAllowlists] WHERE [NetworkPattern] = '10.200.0.0/16')
BEGIN
    INSERT INTO [dbo].[StudioNetworkAllowlists] ([Id], [NetworkPattern], [EntryType], [EnvironmentDescription], [IsProduction], [ApprovedByUserId], [ApprovedAtUtc], [IsActive])
    VALUES (NEWID(), '10.200.0.0/16', 'CidrSubnet', 'Enterprise Datacenter Production Pre-Check Subnet', 1, '00000000-0000-0000-0000-000000000001', SYSUTCDATETIME(), 1);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[StudioNetworkAllowlists] WHERE [NetworkPattern] = '*.wercsgcs.internal')
BEGIN
    INSERT INTO [dbo].[StudioNetworkAllowlists] ([Id], [NetworkPattern], [EntryType], [EnvironmentDescription], [IsProduction], [ApprovedByUserId], [ApprovedAtUtc], [IsActive])
    VALUES (NEWID(), '*.wercsgcs.internal', 'FqdnPattern', 'Enterprise Internal Domain Wildcard', 1, '00000000-0000-0000-0000-000000000001', SYSUTCDATETIME(), 1);
END
GO

-- 3. SEED AUTHORITATIVE ACTIVE SOP CONFIGURATIONS
DECLARE @AdminId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';

IF NOT EXISTS (SELECT 1 FROM [dbo].[SopConfigurations] WHERE [ParameterCode] = 'SITE_VERSION' AND [SopVersion] = 'v2.0')
BEGIN
    INSERT INTO [dbo].[SopConfigurations] ([Id], [ParameterCode], [SopDocumentId], [SopTitle], [SopVersion], [SopDocumentUrl], [IsApproved], [ApprovedByUserId], [ApprovedAtUtc], [EffectiveDate], [IsActive])
    VALUES (NEWID(), 'SITE_VERSION', 'SOP-WERCS-PREREQ', 'Site Version and Patch Prerequisites', 'v2.0', 'https://sop.company.internal/docs/SOP-WERCS-PREREQ-v2.0.pdf', 1, @AdminId, SYSUTCDATETIME(), '2025-01-01', 1);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[SopConfigurations] WHERE [ParameterCode] = 'CHEMADVISOR_VERSION' AND [SopVersion] = 'v2.1')
BEGIN
    INSERT INTO [dbo].[SopConfigurations] ([Id], [ParameterCode], [SopDocumentId], [SopTitle], [SopVersion], [SopDocumentUrl], [IsApproved], [ApprovedByUserId], [ApprovedAtUtc], [EffectiveDate], [IsActive])
    VALUES (NEWID(), 'CHEMADVISOR_VERSION', 'SOP-REG-CHEMADV', 'Regulatory ChemAdvisor Database Validation', 'v2.1', 'https://sop.company.internal/docs/SOP-REG-CHEMADV-v2.1.pdf', 1, @AdminId, SYSUTCDATETIME(), '2025-01-01', 1);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[SopConfigurations] WHERE [ParameterCode] = 'TRANSPORT_DB_VERSION' AND [SopVersion] = 'v1.4')
BEGIN
    INSERT INTO [dbo].[SopConfigurations] ([Id], [ParameterCode], [SopDocumentId], [SopTitle], [SopVersion], [SopDocumentUrl], [IsApproved], [ApprovedByUserId], [ApprovedAtUtc], [EffectiveDate], [IsActive])
    VALUES (NEWID(), 'TRANSPORT_DB_VERSION', 'SOP-REG-TRANS', 'Transport Engine Database Compliance', 'v1.4', 'https://sop.company.internal/docs/SOP-REG-TRANS-v1.4.pdf', 1, @AdminId, SYSUTCDATETIME(), '2025-01-01', 1);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[SopConfigurations] WHERE [ParameterCode] = 'CURRENT_CONTENT_VERSION' AND [SopVersion] = 'v3.0')
BEGIN
    INSERT INTO [dbo].[SopConfigurations] ([Id], [ParameterCode], [SopDocumentId], [SopTitle], [SopVersion], [SopDocumentUrl], [IsApproved], [ApprovedByUserId], [ApprovedAtUtc], [EffectiveDate], [IsActive])
    VALUES (NEWID(), 'CURRENT_CONTENT_VERSION', 'SOP-WERCS-UPGRADE', 'Current Content Version Baseline Verification', 'v3.0', 'https://sop.company.internal/docs/SOP-WERCS-UPGRADE-v3.0.pdf', 1, @AdminId, SYSUTCDATETIME(), '2025-01-01', 1);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[SopConfigurations] WHERE [ParameterCode] = 'REQUESTED_CONTENT_VERSION' AND [SopVersion] = 'v3.0')
BEGIN
    INSERT INTO [dbo].[SopConfigurations] ([Id], [ParameterCode], [SopDocumentId], [SopTitle], [SopVersion], [SopDocumentUrl], [IsApproved], [ApprovedByUserId], [ApprovedAtUtc], [EffectiveDate], [IsActive])
    VALUES (NEWID(), 'REQUESTED_CONTENT_VERSION', 'SOP-WERCS-TARGET', 'Target Content Compatibility Prerequisite', 'v3.0', 'https://sop.company.internal/docs/SOP-WERCS-TARGET-v3.0.pdf', 1, @AdminId, SYSUTCDATETIME(), '2025-01-01', 1);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[SopConfigurations] WHERE [ParameterCode] = 'NUMBER_OF_REGIONS' AND [SopVersion] = 'v1.1')
BEGIN
    INSERT INTO [dbo].[SopConfigurations] ([Id], [ParameterCode], [SopDocumentId], [SopTitle], [SopVersion], [SopDocumentUrl], [IsApproved], [ApprovedByUserId], [ApprovedAtUtc], [EffectiveDate], [IsActive])
    VALUES (NEWID(), 'NUMBER_OF_REGIONS', 'SOP-JURIS-GLOBAL', 'Global Configured Regions Alignment', 'v1.1', 'https://sop.company.internal/docs/SOP-JURIS-GLOBAL-v1.1.pdf', 1, @AdminId, SYSUTCDATETIME(), '2025-01-01', 1);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[SopConfigurations] WHERE [ParameterCode] = 'JAPAN_REGION' AND [SopVersion] = 'v2.3')
BEGIN
    INSERT INTO [dbo].[SopConfigurations] ([Id], [ParameterCode], [SopDocumentId], [SopTitle], [SopVersion], [SopDocumentUrl], [IsApproved], [ApprovedByUserId], [ApprovedAtUtc], [EffectiveDate], [IsActive])
    VALUES (NEWID(), 'JAPAN_REGION', 'SOP-JURIS-APAC', 'Japan Regulatory Jurisdiction Absence/Presence Matrix', 'v2.3', 'https://sop.company.internal/docs/SOP-JURIS-APAC-v2.3.pdf', 1, @AdminId, SYSUTCDATETIME(), '2025-01-01', 1);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[SopConfigurations] WHERE [ParameterCode] = 'TRANSPORTATION' AND [SopVersion] = 'v1.5')
BEGIN
    INSERT INTO [dbo].[SopConfigurations] ([Id], [ParameterCode], [SopDocumentId], [SopTitle], [SopVersion], [SopDocumentUrl], [IsApproved], [ApprovedByUserId], [ApprovedAtUtc], [EffectiveDate], [IsActive])
    VALUES (NEWID(), 'TRANSPORTATION', 'SOP-MOD-TRANSPORT', 'Transportation Regulatory Engine Status Check', 'v1.5', 'https://sop.company.internal/docs/SOP-MOD-TRANSPORT-v1.5.pdf', 1, @AdminId, SYSUTCDATETIME(), '2025-01-01', 1);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[SopConfigurations] WHERE [ParameterCode] = 'PCN' AND [SopVersion] = 'v2.0')
BEGIN
    INSERT INTO [dbo].[SopConfigurations] ([Id], [ParameterCode], [SopDocumentId], [SopTitle], [SopVersion], [SopDocumentUrl], [IsApproved], [ApprovedByUserId], [ApprovedAtUtc], [EffectiveDate], [IsActive])
    VALUES (NEWID(), 'PCN', 'SOP-EU-COMPLIANCE', 'EU Poison Centre Notification Engine Validation', 'v2.0', 'https://sop.company.internal/docs/SOP-EU-COMPLIANCE-v2.0.pdf', 1, @AdminId, SYSUTCDATETIME(), '2025-01-01', 1);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[SopConfigurations] WHERE [ParameterCode] = 'CUSTOMER_CUSTOMIZATION' AND [SopVersion] = 'v1.0')
BEGIN
    INSERT INTO [dbo].[SopConfigurations] ([Id], [ParameterCode], [SopDocumentId], [SopTitle], [SopVersion], [SopDocumentUrl], [IsApproved], [ApprovedByUserId], [ApprovedAtUtc], [EffectiveDate], [IsActive])
    VALUES (NEWID(), 'CUSTOMER_CUSTOMIZATION', 'SOP-QA-CUSTOM-AUDIT', 'Customer Core Customization and User Exit Audit', 'v1.0', 'https://sop.company.internal/docs/SOP-QA-CUSTOM-AUDIT-v1.0.pdf', 1, @AdminId, SYSUTCDATETIME(), '2025-01-01', 1);
END
GO

PRINT '==========================================================================================';
PRINT '>>> GCS Pre-Check Automation Portal Database Initialized Successfully (Schema + Seed Data)';
PRINT '==========================================================================================';
GO
