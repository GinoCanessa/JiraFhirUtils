using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using jira_fhir_cli.LlmProvider.Configuration;
using jira_fhir_cli.LlmProvider.Models;
using jira_fhir_cli.LlmProvider.Utils;

namespace jira_fhir_cli.LlmProvider.Providers;

public class OpenAICompatibleProvider : ILlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly LlmConfiguration _config;
    private readonly JsonSerializerOptions _jsonOptions;
    
    public OpenAICompatibleProvider(LlmConfiguration config, HttpClient? httpClient = null)
    {
        _config = config;
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(config.RequestTimeoutSeconds);
        
        if (!string.IsNullOrEmpty(config.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", config.ApiKey);
        }

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = false
        };
    }
    
    public string ProviderName => "OpenAI Compatible";
    public bool SupportsStreaming => true;
    
    public async Task<LlmResponse> GenerateAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        return await RetryHelper.ExecuteWithRetryAsync(
            () => generateInternalAsync(request, cancellationToken),
            _config.MaxRetries,
            _config.RetryDelaySeconds,
            cancellationToken);
    }
    
    private async Task<LlmResponse> generateInternalAsync(LlmRequest request, CancellationToken cancellationToken)
    {
        try
        {
            List<object> messages = [];
            
            if (!string.IsNullOrEmpty(request.SystemPrompt))
            {
                messages.Add(new { role = "system", content = request.SystemPrompt });
            }
            
            messages.Add(new { role = "user", content = request.Prompt });

            var requestBody = new
            {
                model = !string.IsNullOrEmpty(request.Model) ? request.Model : _config.Model,
                messages = messages,
                temperature = request.Temperature,
                max_tokens = request.MaxTokens
            };

            string json = JsonSerializer.Serialize(requestBody, _jsonOptions);
            StringContent content = new StringContent(json, Encoding.UTF8, "application/json");

            string endpoint = _config.ApiEndpoint.TrimEnd('/') + "/chat/completions";
            HttpResponseMessage response = await _httpClient.PostAsync(endpoint, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                string errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                return new LlmResponse
                {
                    Content = "",
                    Success = false,
                    ErrorMessage = $"HTTP {response.StatusCode}: {errorContent}"
                };
            }

            string responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            JsonElement responseData = JsonSerializer.Deserialize<JsonElement>(responseJson);

            if (responseData.TryGetProperty("choices", out JsonElement choices) && 
                choices.GetArrayLength() > 0)
            {
                JsonElement firstChoice = choices[0];
                if (firstChoice.TryGetProperty("message", out JsonElement message) &&
                    message.TryGetProperty("content", out JsonElement contentElement))
                {
                    string responseContent = contentElement.GetString() ?? "";
                    
                    int? tokensUsed = null;
                    if (responseData.TryGetProperty("usage", out JsonElement usage) &&
                        usage.TryGetProperty("total_tokens", out JsonElement tokensElement))
                    {
                        tokensUsed = tokensElement.GetInt32();
                    }

                    string? model = null;
                    if (responseData.TryGetProperty("model", out JsonElement modelElement))
                    {
                        model = modelElement.GetString();
                    }

                    return new LlmResponse
                    {
                        Content = responseContent,
                        Model = model,
                        TokensUsed = tokensUsed,
                        Success = true
                    };
                }
            }

            return new LlmResponse
            {
                Content = "",
                Success = false,
                ErrorMessage = "Invalid response format: missing content in response"
            };
        }
        catch (HttpRequestException ex)
        {
            return new LlmResponse
            {
                Content = "",
                Success = false,
                ErrorMessage = $"HTTP request failed: {ex.Message}"
            };
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken == cancellationToken)
        {
            return new LlmResponse
            {
                Content = "",
                Success = false,
                ErrorMessage = "Request was cancelled"
            };
        }
        catch (TaskCanceledException)
        {
            return new LlmResponse
            {
                Content = "",
                Success = false,
                ErrorMessage = $"Request timed out after {_config.RequestTimeoutSeconds} seconds"
            };
        }
        catch (JsonException ex)
        {
            return new LlmResponse
            {
                Content = "",
                Success = false,
                ErrorMessage = $"JSON parsing failed: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            return new LlmResponse
            {
                Content = "",
                Success = false,
                ErrorMessage = $"Unexpected error: {ex.Message}"
            };
        }
    }
    
    public async Task<bool> ValidateConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            LlmRequest testRequest = new LlmRequest
            {
                Prompt = "Test",
                MaxTokens = 10,
                Temperature = 0.1
            };
            
            LlmResponse response = await GenerateAsync(testRequest, cancellationToken);
            return response.Success;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LLM connection validation failed: {ex.Message}");
            return false;
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}