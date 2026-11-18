-- ==========================================================================================
-- GCS PRE-CHECK AUTOMATION PORTAL — SEED DATA SCRIPT
-- Target RDBMS: Microsoft SQL Server 2019 / 2022 / Azure SQL Database
-- Execution Mode: Standalone Idempotent DML
-- Purpose: Provisions initial employee credentials for portal access.
-- ==========================================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

PRINT '==========================================================================================';
PRINT 'Seeding Default Admin Account for GCS Pre-Check Automation Portal...';
PRINT '==========================================================================================';
GO

-- ------------------------------------------------------------------------------------------
-- 1. Default Administrator Account
-- Username: admin
-- Password: admin123
-- SHA-256 Hex: 240be518fabd2724ddb6f04eeb1da5967448d7e831c08c8fa822809f74c720a9
-- ------------------------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM [dbo].[ApplicationUsers] WHERE [Username] = 'admin')
BEGIN
    INSERT INTO [dbo].[ApplicationUsers] (
        [Id],
        [Username],
        [Email],
        [FullName],
        [PasswordHash],
        [AuthProvider],
        [Role],
        [IsActive],
        [CreatedAtUtc]
    )
    VALUES (
        '00000000-0000-0000-0000-000000000001',
        'admin',
        'admin@wercs.com',
        'System Administrator',
        '240be518fabd2724ddb6f04eeb1da5967448d7e831c08c8fa822809f74c720a9',
        'LocalIdentity',
        'Admin',
        1,
        SYSUTCDATETIME()
    );

    PRINT '>>> Default Admin user [admin] created successfully with initial credentials.';
END
ELSE
BEGIN
    PRINT '>>> User [admin] already exists in [dbo].[ApplicationUsers]. Updating credentials if needed...';
    UPDATE [dbo].[ApplicationUsers]
    SET [PasswordHash] = '240be518fabd2724ddb6f04eeb1da5967448d7e831c08c8fa822809f74c720a9',
        [IsActive] = 1
    WHERE [Username] = 'admin';
END
GO
