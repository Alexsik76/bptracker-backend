using System.Net.Http.Headers;
using System.Text.Json;
using BpTracker.Api.DTOs;
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

    public async Task<ImageAnalysisResultDto?> RecognizeAsync(byte[] imageBytes)
    {
        if (!_settings.Enabled) return null;

        try
        {
            var client = _httpClientFactory.CreateClient("PhotoApi");
            client.BaseAddress = new Uri(_settings.Url!);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _settings.Token);

            using var content = new MultipartFormDataContent();
            var imageContent = new ByteArrayContent(imageBytes);
            imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
            content.Add(imageContent, "file", "image.jpg");

            var response = await client.PostAsync("/images/recognize", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Photo API recognize failed: {StatusCode} {Body}", response.StatusCode, errorBody);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("sys", out var sysEl) &&
                root.TryGetProperty("dia", out var diaEl) &&
                root.TryGetProperty("pul", out var pulEl))
            {
                var sys = sysEl.GetInt32();
                var dia = diaEl.GetInt32();
                var pul = pulEl.GetInt32();
                var confidence = root.TryGetProperty("confidence", out var confEl) ? confEl.GetDouble() : (double?)null;
                return new ImageAnalysisResultDto(sys, dia, pul, "local_ocr", confidence);
            }
            
            _logger.LogWarning("Photo API recognize response invalid format: {Json}", json);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recognizing photo via Photo API");
            return null;
        }
    }

    public async Task UploadAsync(byte[] imageBytes, Measurement measurement, (int Sys, int Dia, int Pulse)? aiResult, string? sourceEngine, string? ocrMeta = null)
    {
        if (!_settings.Enabled) return;

        try
        {
            var client = _httpClientFactory.CreateClient("PhotoApi");
            client.BaseAddress = new Uri(_settings.Url!);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _settings.Token);

            var correctedByUser = aiResult.HasValue && (
                measurement.Sys != aiResult.Value.Sys ||
                measurement.Dia != aiResult.Value.Dia ||
                measurement.Pulse != aiResult.Value.Pulse
            );

            using var content = new MultipartFormDataContent();

            var imageContent = new ByteArrayContent(imageBytes);
            imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
            content.Add(imageContent, "file", $"measurement_{measurement.Id}.jpg");

            content.Add(new StringContent(measurement.Sys.ToString()), "sys");
            content.Add(new StringContent(measurement.Dia.ToString()), "dia");
            content.Add(new StringContent(measurement.Pulse.ToString()), "pul");
            content.Add(new StringContent(measurement.RecordedAt.ToString("O")), "timestamp");
            content.Add(new StringContent(sourceEngine ?? "user_confirmed"), "source");
            content.Add(new StringContent(correctedByUser ? "true" : "false"), "corrected_by_user");
            if (_settings.DeviceModel is not null)
                content.Add(new StringContent(_settings.DeviceModel), "device_model");
            if (aiResult.HasValue)
            {
                content.Add(new StringContent(aiResult.Value.Sys.ToString()), "ai_suggested_sys");
                content.Add(new StringContent(aiResult.Value.Dia.ToString()), "ai_suggested_dia");
                content.Add(new StringContent(aiResult.Value.Pulse.ToString()), "ai_suggested_pul");
            }
            if (ocrMeta is not null)
                content.Add(new StringContent(ocrMeta), "ocr_meta");

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
