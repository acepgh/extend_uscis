# Extend Schema Generator

.NET console app to extract form field schemas from PDFs using the [Extend API](https://extend.app).

## Prerequisites

- .NET 8.0 SDK
- Extend API key

## Build

```bash
dotnet build
```

## Usage

```bash
# Using environment variable for API key
export EXTEND_API_KEY=your_api_key_here
dotnet run -- --pdf /path/to/form.pdf

# Or pass API key directly
dotnet run -- --pdf /path/to/form.pdf --api-key your_api_key_here

# Specify output file
dotnet run -- --pdf /path/to/form.pdf --output /path/to/output.json
```

## Options

| Option | Description |
|--------|-------------|
| `--pdf` | **(Required)** Path to the PDF file to process |
| `--output` | Output JSON file path (default: `<pdf-name>_Extend_Schema.json`) |
| `--api-key` | Extend API key (or set `EXTEND_API_KEY` env var) |

## Example

```bash
dotnet run -- --pdf ~/Documents/I-131A.pdf
```

Output:
```
Processing: I-131A.pdf
Output: /Users/you/Documents/I-131A_Extend_Schema.json

Uploading to Extend API... OK (File ID: file_abc123)
Starting schema extraction... OK (Run ID: run_xyz789)
Processing............ Done!
Saving schema... OK

✅ Schema generated: 156 fields
   Output: /Users/you/Documents/I-131A_Extend_Schema.json
```

## Output Format

The schema JSON follows Extend's output format with field names, types, and descriptions that can be used for SurveyJS questionnaire generation.

## Integration with USCIS Questionnaire Pipeline

After generating the schema:

1. Upload the `*_Extend_Schema.json` to Google Drive (Forms/Questionnaires/{form}/)
2. Run the questionnaire builder:
   ```bash
   python3 /root/.openclaw/skills/uscis-questionnaire/scripts/build_surveyjs.py \
       --schema I-131A_Extend_Schema.json \
       --form I-131A \
       --output I-131A-questionnaire.json
   ```
