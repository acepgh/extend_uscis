# Extend Schemas

This folder contains Extend API schema outputs for USCIS/EOIR forms.

## Usage

1. Run the console app locally: `dotnet run`
2. Use **Quick Process** or **Generate Schema** to create schema JSONs
3. Copy the `*_Extend_Schema.json` files here
4. Commit and push

## Naming Convention

`{FormNumber}_Extend_Schema.json`

Examples:
- `I-131_Extend_Schema.json`
- `I-485_Extend_Schema.json`
- `G-28_Extend_Schema.json`

## Pipeline Integration

The questionnaire generator skill pulls schemas from this folder. **No schema = no questionnaire can be built.**

