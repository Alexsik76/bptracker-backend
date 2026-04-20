using System.Net.Http.Json;
using System.Text.Json;
using BpTracker.Api.DTOs;
using BpTracker.Api.Models;
using Microsoft.Extensions.Options;

namespace BpTracker.Api.Services;

public class GeminiService : IGeminiService
{
    private readonly HttpClient _http;
    private readonly GeminiSettings _settings;
    private readonly ILogger<GeminiService> _logger;

    private const string Prompt =
        "Analyze this blood pressure monitor image. " +
        "Extract: 1. Systolic (top number) 2. Diastolic (middle number) 3. Pulse (bottom number). " +
        "Return ONLY a valid JSON object with keys: 'systolic', 'diastolic', 'pulse'. No markdown, no comments.";

    public GeminiService(HttpClient http, IOptions<GeminiSettings> options, ILogger<GeminiService> logger)
    {
        _http = http;
        _settings = options.Value;
        _logger = logger;
    }

    public async Task<ImageAnalysisResultDto> AnalyzeImageAsync(byte[] imageBytes, string mimeType)
    {
        var base64 = Convert.ToBase64String(imageBytes);

        var payload = new
        {
            contents = new[]
            {
                new
                {
                    parts = new object[]
                    {
                        new { text = Prompt },
                        new { inline_data = new { mime_type = mimeType, data = base64 } }
                    }
                }
            },
            generationConfig = new
            {
                response_mime_type = "application/json",
                temperature = 0.1
            }
        };

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_settings.Model}:generateContent?key={_settings.ApiKey}";
        var response = await _http.PostAsJsonAsync(url, payload);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            _logger.LogError("Gemini API {Status}: {Body}", (int)response.StatusCode, errorBody);

            var geminiMessage = $"HTTP {(int)response.StatusCode}";
            try
            {
                using var errDoc = JsonDocument.Parse(errorBody);
                geminiMessage = errDoc.RootElement
                    .GetProperty("error").GetProperty("message").GetString() ?? geminiMessage;
            }
            catch { }

            throw new HttpRequestException(geminiMessage, null, response.StatusCode);
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        var text = doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString() ?? throw new InvalidOperationException("Gemini повернув порожню відповідь");

        // Clean markdown if present
        text = text.Trim();
        if (text.StartsWith("```json")) text = text.Substring(7);
        else if (text.StartsWith("```")) text = text.Substring(3);
        if (text.EndsWith("```")) text = text.Substring(0, text.Length - 3);
        text = text.Trim();

        using var result = JsonDocument.Parse(text);
        var root = result.RootElement;

        var sys = GetInt(root, "systolic");
        var dia = GetInt(root, "diastolic");
        var pulse = GetInt(root, "pulse");

        if (sys is < 40 or > 300) throw new InvalidOperationException($"Систолічний тиск {sys} поза допустимим діапазоном (40–300)");
        if (dia is < 20 or > 200) throw new InvalidOperationException($"Діастолічний тиск {dia} поза допустимим діапазоном (20–200)");
        if (pulse is < 30 or > 250) throw new InvalidOperationException($"Пульс {pulse} поза допустимим діапазоном (30–250)");

        return new ImageAnalysisResultDto(sys, dia, pulse);
    }

    private static int GetInt(JsonElement root, string key)
    {
        if (!root.TryGetProperty(key, out var el))
            throw new InvalidOperationException($"Поле '{key}' відсутнє у відповіді AI");

        if (el.ValueKind == JsonValueKind.Number)
            return (int)el.GetDouble();

        if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), out var val))
            return val;

        throw new InvalidOperationException($"Не вдалося прочитати число з поля '{key}' (тип: {el.ValueKind})");
    }
}
