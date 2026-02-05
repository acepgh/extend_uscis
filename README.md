# Extend USCIS Schema Generator

.NET console app to download USCIS/EOIR forms and extract field schemas using the [Extend API](https://extend.app).

## Prerequisites

- .NET 8.0 SDK
- Extend API key

## Build

```bash
dotnet build
```

## Commands

### List Available Forms

```bash
# List all forms in catalog
dotnet run -- list

# List only USCIS forms
dotnet run -- list --source USCIS

# List only EOIR forms
dotnet run -- list --source EOIR
```

### Download Forms

```bash
# Download a specific form
dotnet run -- download --form I-131

# Download all forms
dotnet run -- download --all

# Download all USCIS forms to specific directory
dotnet run -- download --all --source USCIS --output-dir ./forms

# Download without instructions
dotnet run -- download --all --instructions false
```

### Process PDF (Generate Schema)

```bash
# Process an existing PDF file
export EXTEND_API_KEY=your_api_key_here
dotnet run -- process --pdf ./forms/I-131.pdf

# Specify output path
dotnet run -- process --pdf ./forms/I-131.pdf --output ./schemas/I-131_schema.json
```

### Download + Process in One Step

```bash
# Download form and generate schema
export EXTEND_API_KEY=your_api_key_here
dotnet run -- process-form --form I-131 --output-dir ./output
```

## Form Catalog

The app includes a catalog of **52 forms** from the TUF Questionnaire tracker:

| Source | Count | Examples |
|--------|-------|----------|
| USCIS  | 43    | I-131, I-485, G-28, N-400 |
| EOIR   | 9     | EOIR-28, EOIR-42A, EOIR-61 |

### USCIS Forms
- I-90, I-102, I-129F, I-130, I-130A, I-131, I-131A, I-192, I-212, I-246
- I-290B, I-360, I-485, I-539, I-589, I-601, I-601A, I-639, I-730, I-751
- I-765, I-765WS, I-821, I-821D, I-824, I-864, I-881, I-912, I-914, I-914A
- I-918, I-918A, I-918B
- N-336, N-400, N-565, N-600, N-648
- G-28, G-325A, AR-11

### EOIR Forms (Department of Justice)
- EOIR-26, EOIR-27, EOIR-28, EOIR-33, EOIR-42A, EOIR-42B, EOIR-59, EOIR-60, EOIR-61

## Example Workflow

```bash
# 1. Set your API key
export EXTEND_API_KEY=ext_abc123...

# 2. Download all USCIS forms
dotnet run -- download --all --source USCIS --output-dir ./forms

# 3. Process each form to generate schemas
for pdf in ./forms/*.pdf; do
    dotnet run -- process --pdf "$pdf" --output "./schemas/$(basename "$pdf" .pdf)_schema.json"
done

# Or process a specific form end-to-end
dotnet run -- process-form --form I-485 --output-dir ./output
```

## Output

### Schema JSON

The generated schema follows Extend's output format:

```json
{
  "type": "object",
  "properties": {
    "applicant_family_name": {
      "type": "string",
      "description": "Family Name (Last Name)"
    },
    "applicant_given_name": {
      "type": "string", 
      "description": "Given Name (First Name)"
    }
    // ... more fields
  }
}
```

### Integration with USCIS Questionnaire Pipeline

After generating schemas:

1. Upload `*_Extend_Schema.json` to Google Drive (`Forms/Questionnaires/{form}/`)
2. Run the SurveyJS builder to create questionnaires
3. Deploy to CAIF

## Environment Variables

| Variable | Description |
|----------|-------------|
| `EXTEND_API_KEY` | Extend API key (required for schema generation) |

## Notes

- USCIS and EOIR websites may block datacenter IPs - run this locally
- Instructions PDFs may not exist for all forms
- EOIR form URLs use a different pattern than USCIS
