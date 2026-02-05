using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ExtendSchemaGenerator;
using Spectre.Console;

class Program
{
    private static readonly HttpClient _httpClient = new();
    private const string ExtendApiBase = "https://api.extend.app/v1";
    private const string ApiVersion = "2025-04-21";
    
    private static string? _apiKey;
    private static string _workingDirectory = Directory.GetCurrentDirectory();

    static Program()
    {
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
    }

    static async Task Main(string[] args)
    {
        // Load API key from environment
        _apiKey = Environment.GetEnvironmentVariable("EXTEND_API_KEY");

        while (true)
        {
            Console.Clear();
            DisplayHeader();
            
            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[green]What would you like to do?[/]")
                    .PageSize(10)
                    .AddChoices(new[]
                    {
                        "📥 Download USCIS Forms",
                        "📋 List Available Forms",
                        "🔧 Generate Schema from PDF",
                        "⚡ Quick Process (Download + Schema)",
                        "⚙️  Settings",
                        "❌ Exit"
                    }));

            switch (choice)
            {
                case "📥 Download USCIS Forms":
                    await DownloadFormsMenu();
                    break;
                case "📋 List Available Forms":
                    ListFormsMenu();
                    break;
                case "🔧 Generate Schema from PDF":
                    await GenerateSchemaMenu();
                    break;
                case "⚡ Quick Process (Download + Schema)":
                    await QuickProcessMenu();
                    break;
                case "⚙️  Settings":
                    SettingsMenu();
                    break;
                case "❌ Exit":
                    AnsiConsole.MarkupLine("[grey]Goodbye![/]");
                    return;
            }
        }
    }

    static void DisplayHeader()
    {
        AnsiConsole.Write(
            new FigletText("USCIS Forms")
                .LeftJustified()
                .Color(Color.Blue));
        
        AnsiConsole.MarkupLine("[grey]Extend Schema Generator[/]");
        AnsiConsole.MarkupLine($"[grey]Working Directory:[/] [cyan]{_workingDirectory}[/]");
        AnsiConsole.MarkupLine($"[grey]API Key:[/] {(_apiKey != null ? "[green]Configured ✓[/]" : "[red]Not Set ✗[/]")}");
        AnsiConsole.WriteLine();
    }

    static async Task DownloadFormsMenu()
    {
        Console.Clear();
        AnsiConsole.MarkupLine("[bold blue]📥 Download USCIS Forms[/]\n");

        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Select download option:")
                .AddChoices(new[]
                {
                    "Download ALL USCIS Forms",
                    "Download ALL EOIR Forms", 
                    "Download ALL Forms (USCIS + EOIR)",
                    "Download Specific Form",
                    "← Back to Main Menu"
                }));

        if (choice == "← Back to Main Menu") return;

        // Get output directory
        var outputDir = AnsiConsole.Prompt(
            new TextPrompt<string>("Output directory:")
                .DefaultValue(Path.Combine(_workingDirectory, "forms"))
                .ShowDefaultValue());

        Directory.CreateDirectory(outputDir);

        var includeInstructions = AnsiConsole.Confirm("Download instruction PDFs too?", true);

        IEnumerable<FormCatalog.FormInfo> formsToDownload;

        if (choice == "Download Specific Form")
        {
            var formNumber = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Select form to download:")
                    .PageSize(15)
                    .EnableSearch()
                    .AddChoices(FormCatalog.AllForms.OrderBy(f => f.FormNumber).Select(f => $"{f.FormNumber} - {f.DisplayName}")));

            var selectedFormNumber = formNumber.Split(" - ")[0];
            formsToDownload = new[] { FormCatalog.FindForm(selectedFormNumber)! };
        }
        else
        {
            formsToDownload = choice switch
            {
                "Download ALL USCIS Forms" => FormCatalog.AllForms.Where(f => f.Source == FormCatalog.FormSource.USCIS),
                "Download ALL EOIR Forms" => FormCatalog.AllForms.Where(f => f.Source == FormCatalog.FormSource.EOIR),
                _ => FormCatalog.AllForms
            };
        }

        var formList = formsToDownload.ToList();
        
        AnsiConsole.WriteLine();
        await AnsiConsole.Progress()
            .AutoClear(false)
            .Columns(new ProgressColumn[]
            {
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn(),
            })
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask($"[green]Downloading {formList.Count} forms[/]", maxValue: formList.Count);

                foreach (var form in formList)
                {
                    task.Description = $"[green]Downloading {form.FormNumber}[/]";
                    
                    try
                    {
                        var url = FormCatalog.GetDownloadUrl(form);
                        var response = await _httpClient.GetAsync(url);
                        
                        if (response.IsSuccessStatusCode)
                        {
                            var bytes = await response.Content.ReadAsByteArrayAsync();
                            var filePath = Path.Combine(outputDir, $"{form.FormNumber}.pdf");
                            await File.WriteAllBytesAsync(filePath, bytes);

                            if (includeInstructions && form.Source == FormCatalog.FormSource.USCIS)
                            {
                                var instrUrl = FormCatalog.GetInstructionsUrl(form);
                                if (instrUrl != null)
                                {
                                    try
                                    {
                                        var instrResponse = await _httpClient.GetAsync(instrUrl);
                                        if (instrResponse.IsSuccessStatusCode)
                                        {
                                            var instrBytes = await instrResponse.Content.ReadAsByteArrayAsync();
                                            var instrPath = Path.Combine(outputDir, $"{form.FormNumber}-instructions.pdf");
                                            await File.WriteAllBytesAsync(instrPath, instrBytes);
                                        }
                                    }
                                    catch { }
                                }
                            }
                        }
                    }
                    catch { }

                    task.Increment(1);
                }

                task.Description = "[green]Download complete![/]";
            });

        AnsiConsole.MarkupLine($"\n[green]✓ Forms saved to:[/] {outputDir}");
        AnsiConsole.MarkupLine("\n[grey]Press any key to continue...[/]");
        Console.ReadKey(true);
    }

    static void ListFormsMenu()
    {
        Console.Clear();
        AnsiConsole.MarkupLine("[bold blue]📋 Available Forms[/]\n");

        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Filter by source:")
                .AddChoices(new[] { "All Forms", "USCIS Only", "EOIR Only", "← Back" }));

        if (choice == "← Back") return;

        var forms = choice switch
        {
            "USCIS Only" => FormCatalog.AllForms.Where(f => f.Source == FormCatalog.FormSource.USCIS),
            "EOIR Only" => FormCatalog.AllForms.Where(f => f.Source == FormCatalog.FormSource.EOIR),
            _ => FormCatalog.AllForms.AsEnumerable()
        };

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("[bold]Form[/]")
            .AddColumn("[bold]Source[/]")
            .AddColumn("[bold]Description[/]");

        foreach (var form in forms.OrderBy(f => f.FormNumber))
        {
            var sourceColor = form.Source == FormCatalog.FormSource.USCIS ? "blue" : "yellow";
            table.AddRow(
                $"[white]{form.FormNumber}[/]",
                $"[{sourceColor}]{form.Source}[/]",
                form.DisplayName);
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"\n[grey]Total: {forms.Count()} forms[/]");
        AnsiConsole.MarkupLine("\n[grey]Press any key to continue...[/]");
        Console.ReadKey(true);
    }

    static async Task GenerateSchemaMenu()
    {
        Console.Clear();
        AnsiConsole.MarkupLine("[bold blue]🔧 Generate Schema from PDF[/]\n");

        if (string.IsNullOrEmpty(_apiKey))
        {
            AnsiConsole.MarkupLine("[red]Error: Extend API key not configured![/]");
            AnsiConsole.MarkupLine("[grey]Go to Settings to configure your API key.[/]");
            AnsiConsole.MarkupLine("\n[grey]Press any key to continue...[/]");
            Console.ReadKey(true);
            return;
        }

        var pdfPath = AnsiConsole.Prompt(
            new TextPrompt<string>("Enter path to PDF file:")
                .Validate(path =>
                {
                    if (!File.Exists(path))
                        return ValidationResult.Error("[red]File not found[/]");
                    if (!path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                        return ValidationResult.Error("[red]File must be a PDF[/]");
                    return ValidationResult.Success();
                }));

        var defaultOutput = Path.Combine(
            Path.GetDirectoryName(pdfPath) ?? ".",
            $"{Path.GetFileNameWithoutExtension(pdfPath)}_Extend_Schema.json");

        var outputPath = AnsiConsole.Prompt(
            new TextPrompt<string>("Output JSON path:")
                .DefaultValue(defaultOutput)
                .ShowDefaultValue());

        await ProcessPdfWithProgress(pdfPath, outputPath);

        AnsiConsole.MarkupLine("\n[grey]Press any key to continue...[/]");
        Console.ReadKey(true);
    }

    static async Task QuickProcessMenu()
    {
        Console.Clear();
        AnsiConsole.MarkupLine("[bold blue]⚡ Quick Process (Download + Schema)[/]\n");

        if (string.IsNullOrEmpty(_apiKey))
        {
            AnsiConsole.MarkupLine("[red]Error: Extend API key not configured![/]");
            AnsiConsole.MarkupLine("[grey]Go to Settings to configure your API key.[/]");
            AnsiConsole.MarkupLine("\n[grey]Press any key to continue...[/]");
            Console.ReadKey(true);
            return;
        }

        var formNumber = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Select form to process:")
                .PageSize(15)
                .EnableSearch()
                .AddChoices(FormCatalog.AllForms.OrderBy(f => f.FormNumber).Select(f => $"{f.FormNumber} - {f.DisplayName}")));

        var selectedFormNumber = formNumber.Split(" - ")[0];
        var form = FormCatalog.FindForm(selectedFormNumber)!;

        var outputDir = AnsiConsole.Prompt(
            new TextPrompt<string>("Output directory:")
                .DefaultValue(Path.Combine(_workingDirectory, "output", form.FormNumber))
                .ShowDefaultValue());

        Directory.CreateDirectory(outputDir);

        AnsiConsole.WriteLine();

        // Step 1: Download
        var pdfPath = Path.Combine(outputDir, $"{form.FormNumber}.pdf");
        
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync($"Downloading {form.FormNumber}...", async ctx =>
            {
                var url = FormCatalog.GetDownloadUrl(form);
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                var bytes = await response.Content.ReadAsByteArrayAsync();
                await File.WriteAllBytesAsync(pdfPath, bytes);
            });

        AnsiConsole.MarkupLine($"[green]✓[/] Downloaded: {pdfPath}");

        // Step 2: Process with Extend
        var schemaPath = Path.Combine(outputDir, $"{form.FormNumber}_Extend_Schema.json");
        await ProcessPdfWithProgress(pdfPath, schemaPath);

        AnsiConsole.MarkupLine("\n[grey]Press any key to continue...[/]");
        Console.ReadKey(true);
    }

    static void SettingsMenu()
    {
        Console.Clear();
        AnsiConsole.MarkupLine("[bold blue]⚙️ Settings[/]\n");

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("[bold]Setting[/]")
            .AddColumn("[bold]Value[/]");

        table.AddRow("Working Directory", _workingDirectory);
        table.AddRow("API Key", _apiKey != null ? $"[green]{_apiKey[..10]}...{_apiKey[^4..]}[/]" : "[red]Not Set[/]");

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("What would you like to change?")
                .AddChoices(new[]
                {
                    "Set API Key",
                    "Change Working Directory",
                    "← Back to Main Menu"
                }));

        switch (choice)
        {
            case "Set API Key":
                var newKey = AnsiConsole.Prompt(
                    new TextPrompt<string>("Enter Extend API Key:")
                        .Secret());
                _apiKey = newKey;
                AnsiConsole.MarkupLine("[green]✓ API Key updated![/]");
                AnsiConsole.MarkupLine("[yellow]Note: This is only saved for this session. Set EXTEND_API_KEY environment variable for persistence.[/]");
                AnsiConsole.MarkupLine("\n[grey]Press any key to continue...[/]");
                Console.ReadKey(true);
                break;

            case "Change Working Directory":
                var newDir = AnsiConsole.Prompt(
                    new TextPrompt<string>("Enter working directory:")
                        .DefaultValue(_workingDirectory)
                        .Validate(dir =>
                        {
                            try
                            {
                                Directory.CreateDirectory(dir);
                                return ValidationResult.Success();
                            }
                            catch
                            {
                                return ValidationResult.Error("[red]Invalid directory path[/]");
                            }
                        }));
                _workingDirectory = newDir;
                AnsiConsole.MarkupLine("[green]✓ Working directory updated![/]");
                AnsiConsole.MarkupLine("\n[grey]Press any key to continue...[/]");
                Console.ReadKey(true);
                break;
        }
    }

    static async Task ProcessPdfWithProgress(string pdfPath, string outputPath)
    {
        try
        {
            string? fileId = null;
            string? runId = null;
            JsonNode? schema = null;

            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Processing...", async ctx =>
                {
                    // Upload
                    ctx.Status("Uploading to Extend API...");
                    fileId = await UploadFile(pdfPath, _apiKey!);

                    // Start run
                    ctx.Status("Starting schema extraction...");
                    runId = await StartEditRun(fileId, _apiKey!);

                    // Poll
                    ctx.Status("Extracting schema (this may take 30-60 seconds)...");
                    schema = await WaitForCompletion(runId, _apiKey!);

                    // Save
                    ctx.Status("Saving schema...");
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    var json = JsonSerializer.Serialize(schema, options);
                    await File.WriteAllTextAsync(outputPath, json);
                });

            var fieldCount = schema?["properties"]?.AsObject()?.Count ?? 0;
            
            AnsiConsole.MarkupLine($"[green]✓[/] Schema generated: [cyan]{fieldCount} fields[/]");
            AnsiConsole.MarkupLine($"[green]✓[/] Saved to: [cyan]{outputPath}[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗ Error: {ex.Message}[/]");
        }
    }

    static async Task<string> UploadFile(string pdfPath, string apiKey)
    {
        using var content = new MultipartFormDataContent();
        var fileBytes = await File.ReadAllBytesAsync(pdfPath);
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", Path.GetFileName(pdfPath));

        var request = new HttpRequestMessage(HttpMethod.Post, $"{ExtendApiBase}/files")
        {
            Content = content
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.Add("x-extend-api-version", ApiVersion);

        var response = await _httpClient.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception($"Upload failed ({response.StatusCode}): {responseBody}");

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
            throw new Exception($"Edit run failed ({response.StatusCode}): {responseBody}");

        var json = JsonNode.Parse(responseBody);
        return json?["id"]?.GetValue<string>() 
            ?? throw new Exception("No run ID in response");
    }

    static async Task<JsonNode?> WaitForCompletion(string runId, string apiKey)
    {
        var maxAttempts = 120;
        var attempt = 0;

        while (attempt < maxAttempts)
        {
            await Task.Delay(3000);

            var request = new HttpRequestMessage(HttpMethod.Get, $"{ExtendApiBase}/edit-runs/{runId}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Headers.Add("x-extend-api-version", ApiVersion);

            var response = await _httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Status check failed ({response.StatusCode}): {responseBody}");

            var json = JsonNode.Parse(responseBody);
            var status = json?["status"]?.GetValue<string>();

            if (status == "complete")
                return json?["outputSchema"];
            
            if (status != "running" && status != "pending")
                throw new Exception($"Edit run failed with status: {status}");

            attempt++;
        }

        throw new Exception("Timeout waiting for schema extraction");
    }
}
