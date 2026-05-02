using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BpTracker.Api.Models;
using Microsoft.Extensions.Options;

namespace BpTracker.Api.Services;

public class PhotoApiService : IPhotoApiService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly PhotoApiSettings _settings;
    private readonly ILogger<PhotoApiService> _logger;

    public PhotoApiService(
        IHttpClientFactory httpClientFactory,
        IOptions<PhotoApiSettings> options,
        ILogger<PhotoApiService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = options.Value;
        _logger = logger;
    }

    public async Task UploadAsync(byte[] imageBytes, Measurement measurement, (int Sys, int Dia, int Pulse)? geminiResult)
    {
        if (!_settings.Enabled) return;

        try
        {
            var client = _httpClientFactory.CreateClient("PhotoApi");
            client.BaseAddress = new Uri(_settings.Url!);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _settings.Token);

            var correctedByUser = geminiResult.HasValue && (
                measurement.Sys != geminiResult.Value.Sys ||
                measurement.Dia != geminiResult.Value.Dia ||
                measurement.Pulse != geminiResult.Value.Pulse
            );

            var metadata = new
            {
                sys = measurement.Sys,
                dia = measurement.Dia,
                pul = measurement.Pulse,
                timestamp = measurement.RecordedAt.ToString("O"), // ISO 8601 with TZ
                device_model = _settings.DeviceModel,
                source = "user_confirmed",
                corrected_by_user = correctedByUser,
                gemini_suggested = geminiResult.HasValue ? new
                {
                    sys = geminiResult.Value.Sys,
                    dia = geminiResult.Value.Dia,
                    pul = geminiResult.Value.Pulse
                } : null,
                notes = (string?)null,
                quality_flags = (object?)null
            };

            using var content = new MultipartFormDataContent();

            var imageContent = new ByteArrayContent(imageBytes);
            imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
            content.Add(imageContent, "file", $"measurement_{measurement.Id}.jpg");

            var metadataJson = JsonSerializer.Serialize(metadata);
            content.Add(new StringContent(metadataJson, Encoding.UTF8, "application/json"), "metadata");

            var response = await client.PostAsync("/images/upload", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Photo API upload failed: {StatusCode} {Body}", response.StatusCode, errorBody);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading photo to Photo API");
        }
    }
}
