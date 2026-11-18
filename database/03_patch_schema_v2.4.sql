-- ==========================================================================================
-- GCS PRE-CHECK AUTOMATION PORTAL — SCHEMA RECONCILIATION PATCH v2.4
-- Target RDBMS: Microsoft SQL Server 2019 / 2022 / Azure SQL Database
-- Purpose: Aligns existing table columns with ApplicationDbContext EF Core Entity Models
-- Execution: Run in SSMS or sqlcmd against GcsPreCheckDb
-- ==========================================================================================

USE [GcsPreCheckDb];
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

PRINT '>>> Applying Schema Reconciliation Patch v2.4 to [GcsPreCheckDb]...';

-- 1. FIX CustomerTickets: PmApprovalNotes & VerifiedByUserId
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.CustomerTickets') AND name = 'PmApprovalNotes')
BEGIN
    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.CustomerTickets') AND name = 'PmRejectionReason')
    BEGIN
        PRINT 'Renaming dbo.CustomerTickets.PmRejectionReason -> PmApprovalNotes...';
        EXEC sp_rename 'dbo.CustomerTickets.PmRejectionReason', 'PmApprovalNotes', 'COLUMN';
    END
    ELSE
    BEGIN
        PRINT 'Adding dbo.CustomerTickets.PmApprovalNotes...';
        ALTER TABLE dbo.CustomerTickets ADD [PmApprovalNotes] NVARCHAR(MAX) NULL;
    END
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.CustomerTickets') AND name = 'VerifiedByUserId')
BEGIN
    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.CustomerTickets') AND name = 'ReviewedByUserId')
    BEGIN
        PRINT 'Renaming dbo.CustomerTickets.ReviewedByUserId -> VerifiedByUserId...';
        IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CustomerTickets_ApplicationUsers_ReviewedBy')
        BEGIN
            ALTER TABLE dbo.CustomerTickets DROP CONSTRAINT [FK_CustomerTickets_ApplicationUsers_ReviewedBy];
        END

        EXEC sp_rename 'dbo.CustomerTickets.ReviewedByUserId', 'VerifiedByUserId', 'COLUMN';

        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CustomerTickets_ApplicationUsers_VerifiedBy')
        BEGIN
            ALTER TABLE dbo.CustomerTickets ADD CONSTRAINT [FK_CustomerTickets_ApplicationUsers_VerifiedBy]
                FOREIGN KEY ([VerifiedByUserId]) REFERENCES dbo.ApplicationUsers([Id]);
        END
    END
    ELSE
    BEGIN
        PRINT 'Adding dbo.CustomerTickets.VerifiedByUserId...';
        ALTER TABLE dbo.CustomerTickets ADD [VerifiedByUserId] UNIQUEIDENTIFIER NULL;
        ALTER TABLE dbo.CustomerTickets ADD CONSTRAINT [FK_CustomerTickets_ApplicationUsers_VerifiedBy]
            FOREIGN KEY ([VerifiedByUserId]) REFERENCES dbo.ApplicationUsers([Id]);
    END
END
GO

-- 2. FIX PreCheckRuns: Missing summary count columns
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.PreCheckRuns') AND name = 'PassedCount')
BEGIN
    PRINT 'Adding dbo.PreCheckRuns.PassedCount...';
    ALTER TABLE dbo.PreCheckRuns ADD [PassedCount] INT NOT NULL CONSTRAINT [DF_PreCheckRuns_PassedCount] DEFAULT (0);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.PreCheckRuns') AND name = 'FailedCount')
BEGIN
    PRINT 'Adding dbo.PreCheckRuns.FailedCount...';
    ALTER TABLE dbo.PreCheckRuns ADD [FailedCount] INT NOT NULL CONSTRAINT [DF_PreCheckRuns_FailedCount] DEFAULT (0);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.PreCheckRuns') AND name = 'NotVerifiedCount')
BEGIN
    PRINT 'Adding dbo.PreCheckRuns.NotVerifiedCount...';
    ALTER TABLE dbo.PreCheckRuns ADD [NotVerifiedCount] INT NOT NULL CONSTRAINT [DF_PreCheckRuns_NotVerifiedCount] DEFAULT (0);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.PreCheckRuns') AND name = 'NotApplicableCount')
BEGIN
    PRINT 'Adding dbo.PreCheckRuns.NotApplicableCount...';
    ALTER TABLE dbo.PreCheckRuns ADD [NotApplicableCount] INT NOT NULL CONSTRAINT [DF_PreCheckRuns_NotApplicableCount] DEFAULT (0);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.PreCheckRuns') AND name = 'NeedsReviewCount')
BEGIN
    PRINT 'Adding dbo.PreCheckRuns.NeedsReviewCount...';
    ALTER TABLE dbo.PreCheckRuns ADD [NeedsReviewCount] INT NOT NULL CONSTRAINT [DF_PreCheckRuns_NeedsReviewCount] DEFAULT (0);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.PreCheckRuns') AND name = 'ExecutionDurationMs')
BEGIN
    PRINT 'Adding dbo.PreCheckRuns.ExecutionDurationMs...';
    ALTER TABLE dbo.PreCheckRuns ADD [ExecutionDurationMs] INT NULL;
END
GO

-- 3. FIX AuditLogs: AuditPayloadJson
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AuditLogs') AND name = 'AuditPayloadJson')
BEGIN
    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AuditLogs') AND name = 'Details')
    BEGIN
        PRINT 'Renaming dbo.AuditLogs.Details -> AuditPayloadJson...';
        EXEC sp_rename 'dbo.AuditLogs.Details', 'AuditPayloadJson', 'COLUMN';
    END
    ELSE
    BEGIN
        PRINT 'Adding dbo.AuditLogs.AuditPayloadJson...';
        ALTER TABLE dbo.AuditLogs ADD [AuditPayloadJson] NVARCHAR(MAX) NULL;
    END
END
GO

PRINT '>>> Schema Reconciliation Patch v2.4 applied successfully!';
GO
