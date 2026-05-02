using System.Net;
using BpTracker.Api.Models;
using BpTracker.Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BpTracker.Api.Tests.Services;

public class PhotoApiServiceTests
{
    private class TestHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private class TestMessageHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastContent { get; private set; }
        public HttpStatusCode ResponseStatusCode { get; set; } = HttpStatusCode.OK;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content != null)
            {
                LastContent = await request.Content.ReadAsStringAsync();
            }
            return new HttpResponseMessage(ResponseStatusCode);
        }
    }

    [Fact]
    public async Task UploadAsync_WhenEnabled_SendsCorrectMetadata()
    {
        // Arrange
        var handler = new TestMessageHandler();
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://api") };
        var factory = new TestHttpClientFactory(client);
        var settings = new PhotoApiSettings
        {
            Enabled = true,
            Url = "http://api",
            Token = "secret",
            DeviceModel = "Test-Device"
        };
        var service = new PhotoApiService(factory, Options.Create(settings), NullLogger<PhotoApiService>.Instance);

        var recordedAt = new DateTime(2026, 5, 2, 10, 0, 0, DateTimeKind.Utc);
        var measurement = new Measurement
        {
            Id = Guid.NewGuid(),
            Sys = 120,
            Dia = 80,
            Pulse = 70,
            RecordedAt = recordedAt
        };
        var gemini = (125, 85, 75);

        // Act
        await service.UploadAsync([1, 2, 3], measurement, gemini);

        // Assert
        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.Headers.Authorization!.Parameter.Should().Be("secret");

        handler.LastContent.Should().Contain("\"corrected_by_user\":true");
        handler.LastContent.Should().Contain("\"device_model\":\"Test-Device\"");
        handler.LastContent.Should().Contain("\"gemini_suggested\":{\"sys\":125,\"dia\":85,\"pul\":75}");
        handler.LastContent.Should().Contain(recordedAt.ToString("O"));
    }

    [Fact]
    public async Task UploadAsync_WhenNotCorrected_SetsFlagToFalse()
    {
        // Arrange
        var handler = new TestMessageHandler();
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://api") };
        var factory = new TestHttpClientFactory(client);
        var settings = new PhotoApiSettings { Enabled = true, Url = "http://api", Token = "s" };
        var service = new PhotoApiService(factory, Options.Create(settings), NullLogger<PhotoApiService>.Instance);

        var measurement = new Measurement { Sys = 120, Dia = 80, Pulse = 70 };
        var gemini = (120, 80, 70);

        // Act
        await service.UploadAsync([1], measurement, gemini);

        // Assert
        handler.LastContent.Should().Contain("\"corrected_by_user\":false");
    }

    [Fact]
    public async Task UploadAsync_WhenDisabled_DoesNotSendRequest()
    {
        // Arrange
        var handler = new TestMessageHandler();
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://api") };
        var factory = new TestHttpClientFactory(client);
        var settings = new PhotoApiSettings { Enabled = false };
        var service = new PhotoApiService(factory, Options.Create(settings), NullLogger<PhotoApiService>.Instance);

        // Act
        await service.UploadAsync([1], new Measurement(), null);

        // Assert
        handler.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task UploadAsync_WhenApiReturnsError_DoesNotThrow()
    {
        // Arrange
        var handler = new TestMessageHandler { ResponseStatusCode = HttpStatusCode.InternalServerError };
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://api") };
        var factory = new TestHttpClientFactory(client);
        var settings = new PhotoApiSettings { Enabled = true, Url = "http://api", Token = "s" };
        var service = new PhotoApiService(factory, Options.Create(settings), NullLogger<PhotoApiService>.Instance);

        // Act
        var act = () => service.UploadAsync([1], new Measurement(), null);

        // Assert
        await act.Should().NotThrowAsync();
    }
}
