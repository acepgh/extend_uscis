# USCIS Forms Schema Generator

Interactive console app to download USCIS/EOIR forms and extract field schemas using the [Extend API](https://extend.app).

## Prerequisites

- .NET 8.0 SDK
- Extend API key

## Build & Run

```bash
dotnet build
dotnet run
```

## Features

### 📥 Download USCIS Forms
- Download all USCIS forms (43 forms)
- Download all EOIR forms (9 forms)
- Download all forms (52 total)
- Download specific form with searchable list
- Option to include instruction PDFs

### 📋 List Available Forms
- View all forms in the catalog
- Filter by source (USCIS or EOIR)
- See form descriptions

### 🔧 Generate Schema from PDF
- Process any PDF file with Extend API
- Generates JSON schema with field definitions
- Progress indicator during processing

### ⚡ Quick Process (Download + Schema)
- Select a form from searchable list
- Downloads the form automatically
- Generates schema in one step

### ⚙️ Settings
- Configure Extend API key
- Change working directory

## Form Catalog

**52 forms** from the TUF Questionnaire tracker:

| Source | Count | Examples |
|--------|-------|----------|
| USCIS  | 43    | I-131, I-485, G-28, N-400, AR-11 |
| EOIR   | 9     | EOIR-28, EOIR-42A, EOIR-61 |

## Environment Variables

| Variable | Description |
|----------|-------------|
| `EXTEND_API_KEY` | Extend API key (can also be set in app Settings) |

## Screenshots

```
╭──────────────────────────────────────────╮
│  _   _ ____   ____ ___ ____              │
│ | | | / ___| / ___|_ _/ ___|             │
│ | | | \___ \| |    | |\___ \             │
│ | |_| |___) | |___ | | ___) |            │
│  \___/|____/ \____|___|____/             │
│                                          │
│  Extend Schema Generator                 │
│  Working Directory: /Users/you/forms     │
│  API Key: Configured ✓                   │
│                                          │
│  What would you like to do?              │
│  > 📥 Download USCIS Forms               │
│    📋 List Available Forms               │
│    🔧 Generate Schema from PDF           │
│    ⚡ Quick Process (Download + Schema)  │
│    ⚙️  Settings                          │
│    ❌ Exit                               │
╰──────────────────────────────────────────╯
```

## Notes

- USCIS/EOIR websites may block datacenter IPs - run this locally from your machine
- Schema generation typically takes 30-60 seconds per form
- Generated schemas can be used with SurveyJS questionnaire builder
