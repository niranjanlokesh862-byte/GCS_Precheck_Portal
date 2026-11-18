# GCS Pre-Check Automation Portal — Database Management Guide

This directory contains standalone, idempotent SQL scripts for managing the Microsoft SQL Server database for the GCS Pre-Check Automation Portal.

---

## Script Manifest

| Script File | Purpose | Execution Timing |
| :--- | :--- | :--- |
| **`Full_Scripts.sql`** | **Master Schema DDL**: Creates all 11 core tables, primary keys, foreign keys, unique indices, check constraints, and defaults with `IF NOT EXISTS` guards. Contains zero seed data. | Initial Setup / Base Provisioning |
| **`Alter_Scripts.sql`** | **Schema Migrations**: Tracks all upcoming incremental database alterations (new columns, indexes, modified constraints) using safe `IF NOT EXISTS` checks. | Incremental Updates |
| **`Seed_Data.sql`** | **Initial Seed Data**: Standalone idempotent DML script creating the initial system employee account (`admin` / `admin123`). | Post-Schema Provisioning |

---

## Database Provisioning Instructions

### Option A: Using SQL Server Management Studio (SSMS) or Azure Data Studio
1. Connect to your SQL Server instance (e.g. `(localdb)\mssqllocaldb` or `localhost`).
2. Ensure the target database exists or create it:
   ```sql
   CREATE DATABASE [GcsPreCheckDb];
   GO
   USE [GcsPreCheckDb];
   GO
   ```
3. Open and execute **`Full_Scripts.sql`** (`F5`).
4. If there are any subsequent alterations, execute **`Alter_Scripts.sql`** (`F5`).
5. Open and execute **`Seed_Data.sql`** (`F5`) to provision the default administrator account (`admin` / `admin123`).

### Option B: Using Command-Line `sqlcmd`
```cmd
sqlcmd -S localhost -d GcsPreCheckDb -E -i "database\Full_Scripts.sql"
sqlcmd -S localhost -d GcsPreCheckDb -E -i "database\Alter_Scripts.sql"
sqlcmd -S localhost -d GcsPreCheckDb -E -i "database\Seed_Data.sql"
```

---

## Default Portal Credentials (Seeded by `Seed_Data.sql`)

- **Username**: `admin`
- **Password**: `admin123`
- **Role**: `Admin`
- **Email**: `admin@wercsgcs.internal`

*Note: Studio target passwords are NEVER stored in this database. Ephemeral Studio credentials exist only in volatile RAM for max 90 seconds during automated pre-check runs.*

---

## 11 Relational Tables Provisioned

1. **`dbo.ApplicationUsers`**: Enterprise employees with role-based access control (`Admin`, `LeadSpecialist`, `Operator`, `ProjectManager`) and password verification.
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
