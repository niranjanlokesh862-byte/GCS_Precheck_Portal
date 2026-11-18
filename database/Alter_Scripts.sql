-- ==========================================================================================
-- GCS PRE-CHECK AUTOMATION PORTAL — UPCOMING SCHEMA ALTER SCRIPTS
-- Target RDBMS: Microsoft SQL Server 2019 / 2022 / Azure SQL Database
-- Execution Mode: Sequential, Idempotent Migrations & Alterations
-- All future DDL modifications (new columns, indexes, altered constraints) must be added here.
-- ==========================================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

PRINT '==========================================================================================';
PRINT 'Applying GCS Pre-Check Automation Portal Incremental Alter Scripts...';
PRINT '==========================================================================================';
GO

-- ------------------------------------------------------------------------------------------
-- MIGRATION TEMPLATE EXAMPLE (Always check existence before applying ALTER):
--
-- IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.CustomerTickets') AND name = 'ExampleNewColumn')
-- BEGIN
--     PRINT 'Adding dbo.CustomerTickets.ExampleNewColumn...';
--     ALTER TABLE dbo.CustomerTickets ADD [ExampleNewColumn] NVARCHAR(100) NULL;
-- END
-- GO
-- ------------------------------------------------------------------------------------------

-- ------------------------------------------------------------------------------------------
-- MIGRATION 2026-11-19-01: Add PasswordHash to ApplicationUsers (for Employee Portal Login)
-- ------------------------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'[dbo].[ApplicationUsers]') 
    AND name = 'PasswordHash'
)
BEGIN
    PRINT 'Adding dbo.ApplicationUsers.PasswordHash column...';
    ALTER TABLE [dbo].[ApplicationUsers] ADD [PasswordHash] NVARCHAR(255) NULL;
END
GO

PRINT '>>> All incremental alter scripts verified successfully.';
GO
