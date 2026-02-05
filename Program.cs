using System.CommandLine;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

class Program
{
    private static readonly HttpClient _httpClient = new();
    private const string ExtendApiBase = "https://api.extend.app/v1";
    private const string ApiVersion = "2025-04-21";

    static async Task<int> Main(string[] args)
    {
        var pdfOption = new Option<FileInfo>(
            name: "--pdf",
            description: "Path to the PDF file to process")
        { IsRequired = true };

        var outputOption = new Option<FileInfo>(
            name: "--output",
            description: "Output JSON file path (default: <pdf-name>_schema.json)");

        var apiKeyOption = new Option<string>(
            name: "--api-key",
            description: "Extend API key (or set EXTEND_API_KEY environment variable)");

        var rootCommand = new RootCommand("Generate schema from PDF using Extend API")
        {
            pdfOption,
            outputOption,
            apiKeyOption
        };

        rootCommand.SetHandler(async (pdf, output, apiKey) =>
        {
            await ProcessPdf(pdf, output, apiKey);
        }, pdfOption, outputOption, apiKeyOption);

        return await rootCommand.InvokeAsync(args);
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
