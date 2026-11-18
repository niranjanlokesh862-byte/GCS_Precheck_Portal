# GCS Pre-Check Automation Portal — Database Manual Execution Guide

This directory contains standalone, idempotent SQL scripts for provisioning and seeding the Microsoft SQL Server database for the GCS Pre-Check Automation Portal.

---

## Script Manifest

| Script File | Purpose | Execution Order |
| :--- | :--- | :--- |
| `01_schema_v2.4.sql` | Creates all 11 tables, primary keys, foreign keys, unique indices, and check constraints. | Step 1 |
| `02_seed_data.sql` | Inserts initial application users (Admin, Lead Specialist, Operator, PM), approved network allowlists, and 10 active SOP rules. | Step 2 |
| `init_database_full.sql` | **All-in-one script** combining both schema DDL and seed data in a single transactional run. | Standalone |

---

## Manual Execution Instructions

### Option A: Using SQL Server Management Studio (SSMS) or Azure Data Studio
1. Open **SSMS** or **Azure Data Studio**.
2. Connect to your target SQL Server instance: `ASPL-LOKESH\SQLEXPRESS2019` with **Windows Authentication**.
3. Ensure the target database exists or create it:
   ```sql
   CREATE DATABASE [GcsPreCheckDb];
   GO
   USE [GcsPreCheckDb];
   GO
   ```
4. Open [init_database_full.sql](file:///d:/Projects/GCS_Precheck_Portal/database/init_database_full.sql).
5. Execute the script (`F5`).
6. Verify output displays:
   ```
   >>> GCS Pre-Check Automation Portal Database Initialized Successfully (Schema + Seed Data)
   ```

### Option B: Using Command-Line `sqlcmd`
```cmd
sqlcmd -S ASPL-LOKESH\SQLEXPRESS2019 -d GcsPreCheckDb -E -i "d:\Projects\GCS_Precheck_Portal\database\init_database_full.sql"
```

---

## Tables Provisioned

1. **`dbo.ApplicationUsers`**: Enterprise employees with role-based access control (`Admin`, `LeadSpecialist`, `Operator`, `ProjectManager`).
2. **`dbo.CustomerTickets`**: Analyzed customer tickets with classification gates (`eLite`, `ManagedServices`, `Unknown`, `OutOfScope`).
3. **`dbo.TicketAttachments`**: Uploaded screenshots and snapshots with SHA-256 hashes and antivirus scan states.
4. **`dbo.TicketRequirementItems`**: Extracted OCR parameters and employee field-verification statuses with reason audit trail.
5. **`dbo.SopConfigurations`**: Authoritative regulatory SOP version mappings with effective dates.
6. **`dbo.StudioNetworkAllowlists`**: IT-approved internal IP, CIDR subnet, and wildcard FQDN allowlists for SSRF prevention.
7. **`dbo.PreCheckJobQueue`**: Durable multi-process job queue with worker heartbeats and leasing timeouts.
8. **`dbo.PreCheckRuns`**: Execution records capturing timestamp, target environment, worker ID, and overall results.
9. **`dbo.PreCheckMfaChallenges`**: SignalR-bridged human-in-the-loop MFA challenges with attempt limit tracking.
10. **`dbo.ParameterCheckResults`**: Verification matrix comparing expected values against Studio actual discoveries.
11. **`dbo.AuditLogs`**: Immutable audit logs capturing every user and system operational event.
