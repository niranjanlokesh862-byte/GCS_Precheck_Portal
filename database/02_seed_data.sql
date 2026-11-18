-- ==========================================================================================
-- GCS PRE-CHECK AUTOMATION PORTAL — SEED DATA SCRIPT v2.4
-- Target RDBMS: Microsoft SQL Server 2019 / 2022 / Azure SQL Database
-- Security Classification: Highly Confidential — Internal Operational Tooling
-- Execution Mode: Manual DBA Script (Idempotent)
-- ==========================================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

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

-- 3. SEED AUTHORITATIVE ACTIVE SOP CONFIGURATIONS (FOR ALL 10 PRE-CHECK PARAMETERS)
DECLARE @AdminId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';

-- Parameter 1: Site Version / Patch
IF NOT EXISTS (SELECT 1 FROM [dbo].[SopConfigurations] WHERE [ParameterCode] = 'SITE_VERSION' AND [SopVersion] = 'v2.0')
BEGIN
    INSERT INTO [dbo].[SopConfigurations] ([Id], [ParameterCode], [SopDocumentId], [SopTitle], [SopVersion], [SopDocumentUrl], [IsApproved], [ApprovedByUserId], [ApprovedAtUtc], [EffectiveDate], [IsActive])
    VALUES (NEWID(), 'SITE_VERSION', 'SOP-WERCS-PREREQ', 'Site Version and Patch Prerequisites', 'v2.0', 'https://sop.company.internal/docs/SOP-WERCS-PREREQ-v2.0.pdf', 1, @AdminId, SYSUTCDATETIME(), '2025-01-01', 1);
END

-- Parameter 2: ChemAdvisor Version
IF NOT EXISTS (SELECT 1 FROM [dbo].[SopConfigurations] WHERE [ParameterCode] = 'CHEMADVISOR_VERSION' AND [SopVersion] = 'v2.1')
BEGIN
    INSERT INTO [dbo].[SopConfigurations] ([Id], [ParameterCode], [SopDocumentId], [SopTitle], [SopVersion], [SopDocumentUrl], [IsApproved], [ApprovedByUserId], [ApprovedAtUtc], [EffectiveDate], [IsActive])
    VALUES (NEWID(), 'CHEMADVISOR_VERSION', 'SOP-REG-CHEMADV', 'Regulatory ChemAdvisor Database Validation', 'v2.1', 'https://sop.company.internal/docs/SOP-REG-CHEMADV-v2.1.pdf', 1, @AdminId, SYSUTCDATETIME(), '2025-01-01', 1);
END

-- Parameter 3: Transport Database Version
IF NOT EXISTS (SELECT 1 FROM [dbo].[SopConfigurations] WHERE [ParameterCode] = 'TRANSPORT_DB_VERSION' AND [SopVersion] = 'v1.4')
BEGIN
    INSERT INTO [dbo].[SopConfigurations] ([Id], [ParameterCode], [SopDocumentId], [SopTitle], [SopVersion], [SopDocumentUrl], [IsApproved], [ApprovedByUserId], [ApprovedAtUtc], [EffectiveDate], [IsActive])
    VALUES (NEWID(), 'TRANSPORT_DB_VERSION', 'SOP-REG-TRANS', 'Transport Engine Database Compliance', 'v1.4', 'https://sop.company.internal/docs/SOP-REG-TRANS-v1.4.pdf', 1, @AdminId, SYSUTCDATETIME(), '2025-01-01', 1);
END

-- Parameter 4: Current Content Version
IF NOT EXISTS (SELECT 1 FROM [dbo].[SopConfigurations] WHERE [ParameterCode] = 'CURRENT_CONTENT_VERSION' AND [SopVersion] = 'v3.0')
BEGIN
    INSERT INTO [dbo].[SopConfigurations] ([Id], [ParameterCode], [SopDocumentId], [SopTitle], [SopVersion], [SopDocumentUrl], [IsApproved], [ApprovedByUserId], [ApprovedAtUtc], [EffectiveDate], [IsActive])
    VALUES (NEWID(), 'CURRENT_CONTENT_VERSION', 'SOP-WERCS-UPGRADE', 'Current Content Version Baseline Verification', 'v3.0', 'https://sop.company.internal/docs/SOP-WERCS-UPGRADE-v3.0.pdf', 1, @AdminId, SYSUTCDATETIME(), '2025-01-01', 1);
END

-- Parameter 5: Requested Content Version
IF NOT EXISTS (SELECT 1 FROM [dbo].[SopConfigurations] WHERE [ParameterCode] = 'REQUESTED_CONTENT_VERSION' AND [SopVersion] = 'v3.0')
BEGIN
    INSERT INTO [dbo].[SopConfigurations] ([Id], [ParameterCode], [SopDocumentId], [SopTitle], [SopVersion], [SopDocumentUrl], [IsApproved], [ApprovedByUserId], [ApprovedAtUtc], [EffectiveDate], [IsActive])
    VALUES (NEWID(), 'REQUESTED_CONTENT_VERSION', 'SOP-WERCS-TARGET', 'Target Content Compatibility Prerequisite', 'v3.0', 'https://sop.company.internal/docs/SOP-WERCS-TARGET-v3.0.pdf', 1, @AdminId, SYSUTCDATETIME(), '2025-01-01', 1);
END

-- Parameter 6: Number of Regions
IF NOT EXISTS (SELECT 1 FROM [dbo].[SopConfigurations] WHERE [ParameterCode] = 'NUMBER_OF_REGIONS' AND [SopVersion] = 'v1.1')
BEGIN
    INSERT INTO [dbo].[SopConfigurations] ([Id], [ParameterCode], [SopDocumentId], [SopTitle], [SopVersion], [SopDocumentUrl], [IsApproved], [ApprovedByUserId], [ApprovedAtUtc], [EffectiveDate], [IsActive])
    VALUES (NEWID(), 'NUMBER_OF_REGIONS', 'SOP-JURIS-GLOBAL', 'Global Configured Regions Alignment', 'v1.1', 'https://sop.company.internal/docs/SOP-JURIS-GLOBAL-v1.1.pdf', 1, @AdminId, SYSUTCDATETIME(), '2025-01-01', 1);
END

-- Parameter 7: Japan Region
IF NOT EXISTS (SELECT 1 FROM [dbo].[SopConfigurations] WHERE [ParameterCode] = 'JAPAN_REGION' AND [SopVersion] = 'v2.3')
BEGIN
    INSERT INTO [dbo].[SopConfigurations] ([Id], [ParameterCode], [SopDocumentId], [SopTitle], [SopVersion], [SopDocumentUrl], [IsApproved], [ApprovedByUserId], [ApprovedAtUtc], [EffectiveDate], [IsActive])
    VALUES (NEWID(), 'JAPAN_REGION', 'SOP-JURIS-APAC', 'Japan Regulatory Jurisdiction Absence/Presence Matrix', 'v2.3', 'https://sop.company.internal/docs/SOP-JURIS-APAC-v2.3.pdf', 1, @AdminId, SYSUTCDATETIME(), '2025-01-01', 1);
END

-- Parameter 8: Transportation
IF NOT EXISTS (SELECT 1 FROM [dbo].[SopConfigurations] WHERE [ParameterCode] = 'TRANSPORTATION' AND [SopVersion] = 'v1.5')
BEGIN
    INSERT INTO [dbo].[SopConfigurations] ([Id], [ParameterCode], [SopDocumentId], [SopTitle], [SopVersion], [SopDocumentUrl], [IsApproved], [ApprovedByUserId], [ApprovedAtUtc], [EffectiveDate], [IsActive])
    VALUES (NEWID(), 'TRANSPORTATION', 'SOP-MOD-TRANSPORT', 'Transportation Regulatory Engine Status Check', 'v1.5', 'https://sop.company.internal/docs/SOP-MOD-TRANSPORT-v1.5.pdf', 1, @AdminId, SYSUTCDATETIME(), '2025-01-01', 1);
END

-- Parameter 9: PCN
IF NOT EXISTS (SELECT 1 FROM [dbo].[SopConfigurations] WHERE [ParameterCode] = 'PCN' AND [SopVersion] = 'v2.0')
BEGIN
    INSERT INTO [dbo].[SopConfigurations] ([Id], [ParameterCode], [SopDocumentId], [SopTitle], [SopVersion], [SopDocumentUrl], [IsApproved], [ApprovedByUserId], [ApprovedAtUtc], [EffectiveDate], [IsActive])
    VALUES (NEWID(), 'PCN', 'SOP-EU-COMPLIANCE', 'EU Poison Centre Notification Engine Validation', 'v2.0', 'https://sop.company.internal/docs/SOP-EU-COMPLIANCE-v2.0.pdf', 1, @AdminId, SYSUTCDATETIME(), '2025-01-01', 1);
END

-- Parameter 10: Customer Customization
IF NOT EXISTS (SELECT 1 FROM [dbo].[SopConfigurations] WHERE [ParameterCode] = 'CUSTOMER_CUSTOMIZATION' AND [SopVersion] = 'v1.0')
BEGIN
    INSERT INTO [dbo].[SopConfigurations] ([Id], [ParameterCode], [SopDocumentId], [SopTitle], [SopVersion], [SopDocumentUrl], [IsApproved], [ApprovedByUserId], [ApprovedAtUtc], [EffectiveDate], [IsActive])
    VALUES (NEWID(), 'CUSTOMER_CUSTOMIZATION', 'SOP-QA-CUSTOM-AUDIT', 'Customer Core Customization and User Exit Audit', 'v1.0', 'https://sop.company.internal/docs/SOP-QA-CUSTOM-AUDIT-v1.0.pdf', 1, @AdminId, SYSUTCDATETIME(), '2025-01-01', 1);
END
GO

PRINT '>>> GCS Pre-Check Automation Portal Seed Data v2.4 successfully applied.';
GO
