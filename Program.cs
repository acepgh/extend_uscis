using System.CommandLine;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ExtendSchemaGenerator;

class Program
{
    private static readonly HttpClient _httpClient = new();
    private const string ExtendApiBase = "https://api.extend.app/v1";
    private const string ApiVersion = "2025-04-21";

    static Program()
    {
        // Set browser-like User-Agent for USCIS downloads
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
    }

    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("USCIS Form Schema Generator - Download forms and extract schemas via Extend API");

        // === Process Command (existing functionality) ===
        var processCommand = new Command("process", "Process a PDF file to generate schema");
        
        var pdfOption = new Option<FileInfo>(
            name: "--pdf",
            description: "Path to the PDF file to process")
        { IsRequired = true };

        var outputOption = new Option<FileInfo?>(
            name: "--output",
            description: "Output JSON file path (default: <pdf-name>_Extend_Schema.json)");

        var apiKeyOption = new Option<string?>(
            name: "--api-key",
            description: "Extend API key (or set EXTEND_API_KEY environment variable)");

        processCommand.AddOption(pdfOption);
        processCommand.AddOption(outputOption);
        processCommand.AddOption(apiKeyOption);

        processCommand.SetHandler(async (pdf, output, apiKey) =>
        {
            await ProcessPdf(pdf, output, apiKey);
        }, pdfOption, outputOption, apiKeyOption);

        // === Download Command ===
        var downloadCommand = new Command("download", "Download USCIS/EOIR form PDFs");

        var formOption = new Option<string?>(
            name: "--form",
            description: "Specific form number to download (e.g., I-131, G-28)");

        var allOption = new Option<bool>(
            name: "--all",
            description: "Download all forms in the catalog");

        var outputDirOption = new Option<DirectoryInfo>(
            name: "--output-dir",
            description: "Directory to save downloaded PDFs",
            getDefaultValue: () => new DirectoryInfo(Directory.GetCurrentDirectory()));

        var includeInstructionsOption = new Option<bool>(
            name: "--instructions",
            description: "Also download instruction PDFs (USCIS forms only)",
            getDefaultValue: () => true);

        var sourceOption = new Option<string?>(
            name: "--source",
            description: "Filter by source: USCIS or EOIR");

        downloadCommand.AddOption(formOption);
        downloadCommand.AddOption(allOption);
        downloadCommand.AddOption(outputDirOption);
        downloadCommand.AddOption(includeInstructionsOption);
        downloadCommand.AddOption(sourceOption);

        downloadCommand.SetHandler(async (form, all, outputDir, includeInstructions, source) =>
        {
            await DownloadForms(form, all, outputDir, includeInstructions, source);
        }, formOption, allOption, outputDirOption, includeInstructionsOption, sourceOption);

        // === List Command ===
        var listCommand = new Command("list", "List all forms in the catalog");
        
        var listSourceOption = new Option<string?>(
            name: "--source",
            description: "Filter by source: USCIS or EOIR");

        listCommand.AddOption(listSourceOption);

        listCommand.SetHandler((source) =>
        {
            ListForms(source);
        }, listSourceOption);

        // === Process-Form Command (download + process in one step) ===
        var processFormCommand = new Command("process-form", "Download a form and generate schema in one step");
        
        var formNumberOption = new Option<string>(
            name: "--form",
            description: "Form number to process (e.g., I-131)")
        { IsRequired = true };

        var processOutputDirOption = new Option<DirectoryInfo>(
            name: "--output-dir",
            description: "Directory for output files",
            getDefaultValue: () => new DirectoryInfo(Directory.GetCurrentDirectory()));

        var processApiKeyOption = new Option<string?>(
            name: "--api-key",
            description: "Extend API key (or set EXTEND_API_KEY environment variable)");

        processFormCommand.AddOption(formNumberOption);
        processFormCommand.AddOption(processOutputDirOption);
        processFormCommand.AddOption(processApiKeyOption);

        processFormCommand.SetHandler(async (formNumber, outputDir, apiKey) =>
        {
            await ProcessFormByNumber(formNumber, outputDir, apiKey);
        }, formNumberOption, processOutputDirOption, processApiKeyOption);

        rootCommand.AddCommand(processCommand);
        rootCommand.AddCommand(downloadCommand);
        rootCommand.AddCommand(listCommand);
        rootCommand.AddCommand(processFormCommand);

        return await rootCommand.InvokeAsync(args);
    }

    static void ListForms(string? sourceFilter)
    {
        var forms = FormCatalog.AllForms.AsEnumerable();
        
        if (!string.IsNullOrEmpty(sourceFilter))
        {
            if (Enum.TryParse<FormCatalog.FormSource>(sourceFilter, true, out var source))
            {
                forms = forms.Where(f => f.Source == source);
            }
            else
            {
                Console.Error.WriteLine($"Invalid source: {sourceFilter}. Use USCIS or EOIR.");
                return;
            }
        }

        Console.WriteLine($"{"Form",-12} {"Source",-8} {"Name"}");
        Console.WriteLine(new string('-', 80));

        foreach (var form in forms.OrderBy(f => f.FormNumber))
        {
            Console.WriteLine($"{form.FormNumber,-12} {form.Source,-8} {form.DisplayName}");
        }

        Console.WriteLine();
        Console.WriteLine($"Total: {forms.Count()} forms");
    }

    static async Task DownloadForms(string? formNumber, bool all, DirectoryInfo outputDir, bool includeInstructions, string? sourceFilter)
    {
        if (!all && string.IsNullOrEmpty(formNumber))
        {
            Console.Error.WriteLine("Error: Specify --form <number> or use --all to download all forms.");
            Environment.Exit(1);
        }

        outputDir.Create();

        IEnumerable<FormCatalog.FormInfo> formsToDownload;

        if (all)
        {
            formsToDownload = FormCatalog.AllForms;
            
            if (!string.IsNullOrEmpty(sourceFilter))
            {
                if (Enum.TryParse<FormCatalog.FormSource>(sourceFilter, true, out var source))
                {
                    formsToDownload = formsToDownload.Where(f => f.Source == source);
                }
            }
        }
        else
        {
            var form = FormCatalog.FindForm(formNumber!);
            if (form == null)
            {
                Console.Error.WriteLine($"Error: Form '{formNumber}' not found in catalog.");
                Console.Error.WriteLine("Use 'list' command to see available forms.");
                Environment.Exit(1);
            }
            formsToDownload = new[] { form };
        }

        var formList = formsToDownload.ToList();
        Console.WriteLine($"Downloading {formList.Count} form(s) to {outputDir.FullName}");
        Console.WriteLine();

        var successCount = 0;
        var failCount = 0;

        foreach (var form in formList)
        {
            Console.Write($"  {form.FormNumber,-12} ");
            
            try
            {
                var url = FormCatalog.GetDownloadUrl(form);
                var fileName = $"{form.FormNumber}.pdf";
                var filePath = Path.Combine(outputDir.FullName, fileName);

                var response = await _httpClient.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    var bytes = await response.Content.ReadAsByteArrayAsync();
                    await File.WriteAllBytesAsync(filePath, bytes);
                    Console.WriteLine($"✓ Downloaded ({bytes.Length / 1024} KB)");
                    successCount++;

                    // Try to download instructions
                    if (includeInstructions && form.Source == FormCatalog.FormSource.USCIS)
                    {
                        var instrUrl = FormCatalog.GetInstructionsUrl(form);
                        if (instrUrl != null)
                        {
                            Console.Write($"  {form.FormNumber + "-instr",-12} ");
                            try
                            {
                                var instrResponse = await _httpClient.GetAsync(instrUrl);
                                if (instrResponse.IsSuccessStatusCode)
                                {
                                    var instrBytes = await instrResponse.Content.ReadAsByteArrayAsync();
                                    var instrPath = Path.Combine(outputDir.FullName, $"{form.FormNumber}-instructions.pdf");
                                    await File.WriteAllBytesAsync(instrPath, instrBytes);
                                    Console.WriteLine($"✓ Downloaded ({instrBytes.Length / 1024} KB)");
                                }
                                else
                                {
                                    Console.WriteLine($"⚠ Not available ({instrResponse.StatusCode})");
                                }
                            }
                            catch
                            {
                                Console.WriteLine("⚠ Not available");
                            }
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"✗ Failed ({response.StatusCode})");
                    failCount++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Error: {ex.Message}");
                failCount++;
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Complete: {successCount} succeeded, {failCount} failed");
    }

    static async Task ProcessFormByNumber(string formNumber, DirectoryInfo outputDir, string? apiKey)
    {
        var form = FormCatalog.FindForm(formNumber);
        if (form == null)
        {
            Console.Error.WriteLine($"Error: Form '{formNumber}' not found in catalog.");
            Console.Error.WriteLine("Use 'list' command to see available forms.");
            Environment.Exit(1);
        }

        outputDir.Create();

        Console.WriteLine($"Processing form: {form.FormNumber} - {form.DisplayName}");
        Console.WriteLine($"Output directory: {outputDir.FullName}");
        Console.WriteLine();

        // Step 1: Download
        Console.Write("Downloading PDF... ");
        var pdfPath = Path.Combine(outputDir.FullName, $"{form.FormNumber}.pdf");
        
        try
        {
            var url = FormCatalog.GetDownloadUrl(form);
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var bytes = await response.Content.ReadAsByteArrayAsync();
            await File.WriteAllBytesAsync(pdfPath, bytes);
            Console.WriteLine($"OK ({bytes.Length / 1024} KB)");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed: {ex.Message}");
            Environment.Exit(1);
        }

        // Step 2: Process with Extend
        var schemaPath = Path.Combine(outputDir.FullName, $"{form.FormNumber}_Extend_Schema.json");
        await ProcessPdf(new FileInfo(pdfPath), new FileInfo(schemaPath), apiKey);
    }

    static async Task ProcessPdf(FileInfo pdfFile, FileInfo? outputFile, string? apiKey)
    {
        // Validate PDF exists
        if (!pdfFile.Exists)
        {
            Console.Error.WriteLine($"Error: PDF file not found: {pdfFile.FullName}");
            Environment.Exit(1);
        }

        // Get API key
        var key = apiKey ?? Environment.GetEnvironmentVariable("EXTEND_API_KEY");
        if (string.IsNullOrEmpty(key))
        {
            Console.Error.WriteLine("Error: API key required. Use --api-key or set EXTEND_API_KEY environment variable.");
            Environment.Exit(1);
        }

        // Set default output path
        var outputPath = outputFile?.FullName 
            ?? Path.Combine(
                pdfFile.DirectoryName ?? ".", 
                $"{Path.GetFileNameWithoutExtension(pdfFile.Name)}_Extend_Schema.json");

        Console.WriteLine($"Processing: {pdfFile.Name}");
        Console.WriteLine($"Output: {outputPath}");
        Console.WriteLine();

        try
        {
            // Step 1: Upload file
            Console.Write("Uploading to Extend API... ");
            var fileId = await UploadFile(pdfFile, key);
            Console.WriteLine($"OK (File ID: {fileId})");

            // Step 2: Start edit run
            Console.Write("Starting schema extraction... ");
            var runId = await StartEditRun(fileId, key);
            Console.WriteLine($"OK (Run ID: {runId})");

            // Step 3: Poll for completion
            Console.Write("Processing");
            var schema = await WaitForCompletion(runId, key);
            Console.WriteLine(" Done!");

            // Step 4: Save schema
            Console.Write("Saving schema... ");
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(schema, options);
            await File.WriteAllTextAsync(outputPath, json);
            Console.WriteLine("OK");

            // Summary
            var fieldCount = schema?["properties"]?.AsObject()?.Count ?? 0;
            Console.WriteLine();
            Console.WriteLine($"✅ Schema generated: {fieldCount} fields");
            Console.WriteLine($"   Output: {outputPath}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    static async Task<string> UploadFile(FileInfo pdfFile, string apiKey)
    {
        using var content = new MultipartFormDataContent();
        var fileBytes = await File.ReadAllBytesAsync(pdfFile.FullName);
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", pdfFile.Name);

        var request = new HttpRequestMessage(HttpMethod.Post, $"{ExtendApiBase}/files")
        {
            Content = content
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.Add("x-extend-api-version", ApiVersion);

        var response = await _httpClient.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Upload failed ({response.StatusCode}): {responseBody}");
        }

        var json = JsonNode.Parse(responseBody);
        return json?["id"]?.GetValue<string>() 
            ?? throw new Exception("No file ID in response");
    }

    static async Task<string> StartEditRun(string fileId, string apiKey)
    {
        var requestBody = JsonSerializer.Serialize(new { config = new { } });
        var content = new StringContent(requestBody, Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, $"{ExtendApiBase}/files/{fileId}/edit-runs")
        {
            Content = content
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.Add("x-extend-api-version", ApiVersion);

        var response = await _httpClient.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Edit run failed ({response.StatusCode}): {responseBody}");
        }

        var json = JsonNode.Parse(responseBody);
        return json?["id"]?.GetValue<string>() 
            ?? throw new Exception("No run ID in response");
    }

    static async Task<JsonNode?> WaitForCompletion(string runId, string apiKey)
    {
        var maxAttempts = 60; // 5 minutes max
        var attempt = 0;

        while (attempt < maxAttempts)
        {
            await Task.Delay(5000); // Wait 5 seconds between polls
            Console.Write(".");

            var request = new HttpRequestMessage(HttpMethod.Get, $"{ExtendApiBase}/edit-runs/{runId}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Headers.Add("x-extend-api-version", ApiVersion);

            var response = await _httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Status check failed ({response.StatusCode}): {responseBody}");
            }

            var json = JsonNode.Parse(responseBody);
            var status = json?["status"]?.GetValue<string>();

            if (status == "complete")
            {
                return json?["outputSchema"];
            }
            else if (status != "running" && status != "pending")
            {
                throw new Exception($"Edit run failed with status: {status}");
            }

            attempt++;
        }

        throw new Exception("Timeout waiting for schema extraction");
    }
}
