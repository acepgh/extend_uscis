# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Interactive .NET 8 console application for downloading USCIS/EOIR immigration forms and extracting field schemas using the Extend API. Built with Spectre.Console for rich terminal UI.

## Commands

### Build and Run
```bash
dotnet build
dotnet run
```

### Clean Build Artifacts
```bash
dotnet clean
```

## Architecture

### Core Components

**Program.cs** (525 lines)
- Main application entry point with interactive menu system
- Five primary workflows: Download Forms, List Forms, Generate Schema, Quick Process, Settings
- Handles all HTTP communication with both form download sites and Extend API
- Static HttpClient with browser user-agent to avoid datacenter IP blocks

**FormCatalog.cs** (131 lines)
- Static catalog of 52 immigration forms (43 USCIS, 9 EOIR)
- `FormInfo` record with form number, display name, source, and optional notes
- URL generation logic for both USCIS and EOIR download patterns
- USCIS pattern: `https://www.uscis.gov/sites/default/files/document/forms/{form}.pdf`
- EOIR pattern: `https://www.justice.gov/eoir/file/{formNumber}/download` (hyphens removed)

### Extend API Integration

Three-step workflow for PDF schema extraction:

1. **Upload PDF** (`UploadFile`)
   - POST to `https://api.extend.ai/files/upload` with multipart form data
   - Returns file object with `file.id` property

2. **Start Edit Run** (`StartEditRun`)
   - POST to `https://api.extend.ai/edit/async` with `{ file: { fileId }, config: {} }`
   - Returns edit run status object with `id` property

3. **Poll for Completion** (`WaitForCompletion`)
   - GET `https://api.extend.ai/edit_runs/{runId}` every 3 seconds
   - Max 120 attempts (6 minutes timeout)
   - Response structure: `{ success: true, editRun: { status, config: { schema }, output, metrics } }`
   - Status values: `PROCESSING`, `PROCESSED`, or `FAILED` (nested at `editRun.status`)
   - Returns JSON schema at `editRun.config.schema` when status becomes `PROCESSED`
   - Schema structure: `properties` object contains field definitions with extend_edit metadata (bbox, field_type, page_index)
   - The `editRun.output` object contains extracted field values (all null when just detecting fields)

### API Configuration

- Base URL: `https://api.extend.ai`
- API Version: `2025-04-21` (sent as `x-extend-api-version` header)
- Authentication: Bearer token from API key configuration
- Schema processing typically takes 30-60 seconds per form
- API Docs: https://docs.extend.ai

### User Settings

Configuration is loaded with the following priority (first found wins):
1. **appsettings.json** (recommended, gitignored for security)
   - `ExtendApi:ApiKey`: Extend API key
   - `WorkingDirectory`: Optional custom working directory
2. **Environment variable**: `EXTEND_API_KEY`
3. **Settings menu**: Interactive API key entry (session-only, not persisted)

## Dependencies

- **Spectre.Console 0.49.1**: Interactive menus, tables, progress bars, status spinners
- **Microsoft.Extensions.Configuration 8.0.0**: Configuration system
- **Microsoft.Extensions.Configuration.Json 8.0.0**: JSON configuration provider
- **.NET 8.0**: Target framework with implicit usings and nullable reference types enabled

## Important Notes

### Network Requirements
- USCIS/EOIR websites may block datacenter IPs
- Run locally from residential connection, not from cloud servers
- HttpClient configured with Chrome user-agent header to mimic browser requests

### Output Files
- Downloaded PDFs: User-specified directory (default: `./forms/`)
- Generated schemas: `{FormNumber}_Extend_Schema.json` with indented formatting
- Optional instruction PDFs: `{FormNumber}-instructions.pdf` (USCIS forms only)

### Form Catalog Maintenance
When adding new forms to `FormCatalog.AllForms`:
- Use consistent form number format (e.g., "I-485", "EOIR-28")
- USCIS forms must match the exact filename on uscis.gov (case-insensitive)
- EOIR forms use file IDs without hyphens in URL pattern
- Update README.md form counts if source distribution changes
