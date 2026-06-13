using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace KTV
{
    public class LlmIntent
    {
        public string Action { get; set; } = "Unknown";
        public string? Title { get; set; }
        public string? Artist { get; set; }
        public int? Value { get; set; }
    }

    public class LlmAgentService
    {
        private readonly HttpClient _httpClient;
        private readonly string _endpoint;
        private readonly string _modelName;
        private readonly bool _isEnabled;

        public LlmAgentService(IConfiguration? configuration, HttpClient? httpClient = null)
        {
            _httpClient = httpClient ?? new HttpClient();
            
            _endpoint = configuration?["LlmAgent:Endpoint"] ?? "http://localhost:11434/api/generate";
            _modelName = configuration?["LlmAgent:ModelName"] ?? "gemma2b";
            
            // Default to true if not specified
            string? enableToggle = configuration?["Features:EnableLlmAgent"];
            _isEnabled = enableToggle == null || bool.Parse(enableToggle);
            
            int timeoutSeconds = 10;
            if (configuration != null && int.TryParse(configuration["LlmAgent:TimeoutSeconds"], out int t))
            {
                timeoutSeconds = t;
            }
            _httpClient.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
        }

        public bool IsEnabled => _isEnabled;

        public async Task<LlmIntent> AnalyzeIntentAsync(string userInput)
        {
            if (!_isEnabled)
            {
                Log.Warning("LLM Agent is disabled via features toggle.");
                return new LlmIntent { Action = "Unknown" };
            }

            string systemPrompt = @"You are an AI assistant for a KTV system. Analyze the user's natural language input and convert it into a JSON command.
You must output ONLY a valid JSON object matching the following structure:
{
  ""Action"": ""Search"" | ""PitchChange"" | ""VocalToggle"" | ""Play"" | ""Pause"" | ""Next"" | ""Unknown"",
  ""Title"": ""song title"" (if Search),
  ""Artist"": ""artist name"" (if Search),
  ""Value"": integer (if PitchChange, e.g., 1, 2, -1, -2)
}
Do not write any other explanation or markdown code blocks. Just return the JSON object.";

            var requestBody = new
            {
                model = _modelName,
                prompt = userInput,
                system = systemPrompt,
                stream = false,
                format = "json"
            };

            string jsonPayload = JsonSerializer.Serialize(requestBody);
            
            try
            {
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                Log.Information("Sending request to LLM Agent at {Endpoint}. Payload: {Payload}", _endpoint, userInput);
                
                var response = await _httpClient.PostAsync(_endpoint, content);
                response.EnsureSuccessStatusCode();

                string responseString = await response.Content.ReadAsStringAsync();
                
                using (var doc = JsonDocument.Parse(responseString))
                {
                    if (doc.RootElement.TryGetProperty("response", out var rawResponseElement))
                    {
                        string innerJson = rawResponseElement.GetString() ?? "";
                        Log.Information("Received LLM Agent raw response: {Response}", innerJson);

                        var intent = JsonSerializer.Deserialize<LlmIntent>(innerJson, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                        
                        return intent ?? new LlmIntent { Action = "Unknown" };
                    }
                }

                throw new JsonException("Failed to find 'response' property in LLM response.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "LLM Agent call failed for input: {Input}", userInput);
                throw;
            }
        }
    }
}
