# GCS Pre-Check Automation Portal

Enterprise-grade automation and regulatory auditing portal built for WERCS GCS operations to analyze customer tickets, extract requirements via OCR, and verify customer WERCS GCS Studio configurations before any update or deployment process.

---

## Architecture Overview

This project is built using **Clean Architecture** principles:

```
GCS_Precheck_Portal/
├── backend/                               # .NET 9 Clean Architecture Solution
│   ├── src/
│   │   ├── Core/
│   │   │   ├── GcsPreCheck.Domain/        # Pure domain entities, value objects, rules
│   │   │   └── GcsPreCheck.Application/   # Use cases, DTOs, rules engine, interfaces
│   │   ├── Infrastructure/
│   │   │   ├── GcsPreCheck.Persistence/   # EF Core 9, SQL Server mappings, DbContext
│   │   │   └── GcsPreCheck.Infrastructure/# SSRF guard, Named Pipe IPC, AV scanner, OCR
│   │   └── Presentation/
│   │       ├── GcsPreCheck.WebApi/        # REST controllers & SignalR Hub
│   │       └── GcsPreCheck.Worker/        # Headless Playwright automation worker
│   ├── tests/
│   │   ├── GcsPreCheck.Domain.UnitTests/
│   │   ├── GcsPreCheck.Infrastructure.UnitTests/
│   │   └── GcsPreCheck.MockStudio/        # Local WebForms Studio simulator
│   ├── GcsPreCheck.sln
│   ├── Directory.Build.props
│   └── nuget.config
├── frontend/                              # Angular 22 Standalone Application
│   ├── src/app/
│   │   ├── core/                          # Services, SignalR client, Auth context
│   │   ├── features/
│   │   │   ├── ticket-wizard/             # Ticket upload & dual-pane review
│   │   │   ├── precheck-console/          # Live execution monitor & MFA OTP prompt
│   │   │   └── pm-queue/                  # Project Manager governance queue
├── database/                              # Standalone idempotent SQL scripts
│   ├── 01_schema_v2.4.sql
│   ├── 02_seed_data.sql
│   ├── 03_patch_schema_v2.4.sql
│   └── init_database_full.sql
└── .gitignore
```

---

## 10 Verified Pre-Check Parameters

1. **Site Version / Patch**: Version comparison against target baseline.
2. **ChemAdvisor Version**: Regulatory database compatibility check.
3. **Transport Database Version**: Transport matrix validation.
4. **Current Content Version**: Installed content baseline.
5. **Requested Content Version**: Target update version eligibility.
6. **Number of Regions**: Region count alignment.
7. **Japan Region**: Jurisdiction status (Active/Inactive).
8. **Transportation**: Transportation module activation check.
9. **PCN**: Poison Centre Notification module check.
10. **Customer Customization**: Verification of bespoke scripts/overrides.

---

## Security Safeguards

- **Zero Passwords in SQL Server**: Passwords exist strictly in server RAM (`EphemeralCredentialVault`) with a 90-second TTL.
- **IPC Delivery via Named Pipes**: Passwords travel to worker processes via Win32 Named Pipes protected by Windows DACLs/SIDs.
- **SSRF Prevention**: `StudioNetworkValidator` validates all destination hosts against approved IT allowlists and blocks loopback, cloud metadata (`169.254.169.254`), and private subnets.
- **Read-Only Route Interceptor**: Playwright aborts all mutating HTTP verbs (`PUT`, `DELETE`, `PATCH`, and mutating `POST` actions) to guarantee the Studio environment cannot be altered.
- **Human-in-the-Loop MFA**: SignalR bridges Studio MFA challenges to the specialist's browser with a real-time countdown timer.
- **Fail-Closed Antivirus**: Uploaded evidence is isolated in quarantine and scanned before acceptance.

---

## Getting Started (Local Development)

### 1. Database Setup
Execute the all-in-one idempotent SQL script on your local SQL Server instance:
```cmd
sqlcmd -S (localdb)\mssqllocaldb -d GcsPreCheckDb -E -i "database\init_database_full.sql"
```

### 2. Backend Services
Navigate to `backend/` and run the Web API:
```bash
cd backend/src/Presentation/GcsPreCheck.WebApi
dotnet run
```
In a separate terminal, launch the Worker service:
```bash
cd backend/src/Presentation/GcsPreCheck.Worker
dotnet run
```

### 3. Frontend Application
Navigate to `frontend/`, install dependencies, and start the development server:
```bash
cd frontend
npm install
npm start
```
Open your browser at `http://localhost:4200/`.
