using System.Net;
using System.Text;
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
        public string ResponseBody { get; set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content != null)
                LastContent = await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(ResponseStatusCode)
            {
                Content = new StringContent(ResponseBody, Encoding.UTF8, "application/json")
            };
        }
    }

    private class TimeoutMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new TaskCanceledException("Simulated timeout");
    }

    // ── UploadAsync tests ────────────────────────────────────────────────────

    [Fact]
    public async Task UploadAsync_WhenEnabled_SendsCorrectMetadata()
    {
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

        var recordedAt = new DateTimeOffset(2026, 5, 2, 10, 0, 0, TimeSpan.Zero);
        var measurement = new Measurement
        {
            Id = Guid.NewGuid(),
            Sys = 120,
            Dia = 80,
            Pulse = 70,
            RecordedAt = recordedAt
        };
        var aiResult = (125, 85, 75);

        await service.UploadAsync([1, 2, 3], measurement, aiResult, "local");

        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.Headers.Authorization!.Parameter.Should().Be("secret");
        handler.LastContent.Should().Contain("name=corrected_by_user\r\n\r\ntrue");
        handler.LastContent.Should().Contain("name=device_model\r\n\r\nTest-Device");
        handler.LastContent.Should().Contain("name=source\r\n\r\nlocal");
        handler.LastContent.Should().Contain("name=ai_suggested_sys\r\n\r\n125");
        handler.LastContent.Should().Contain("name=ai_suggested_dia\r\n\r\n85");
        handler.LastContent.Should().Contain("name=ai_suggested_pul\r\n\r\n75");
        handler.LastContent.Should().Contain(recordedAt.ToString("O"));
    }

    [Fact]
    public async Task UploadAsync_WhenNotCorrected_SetsFlagToFalse()
    {
        var handler = new TestMessageHandler();
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://api") };
        var factory = new TestHttpClientFactory(client);
        var settings = new PhotoApiSettings { Enabled = true, Url = "http://api", Token = "s" };
        var service = new PhotoApiService(factory, Options.Create(settings), NullLogger<PhotoApiService>.Instance);

        var measurement = new Measurement { Sys = 120, Dia = 80, Pulse = 70 };
        var aiResult = (120, 80, 70);

        await service.UploadAsync([1], measurement, aiResult, "local");

        handler.LastContent.Should().Contain("name=corrected_by_user\r\n\r\nfalse");
    }

    [Fact]
    public async Task UploadAsync_WhenDisabled_DoesNotSendRequest()
    {
        var handler = new TestMessageHandler();
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://api") };
        var factory = new TestHttpClientFactory(client);
        var settings = new PhotoApiSettings { Enabled = false };
        var service = new PhotoApiService(factory, Options.Create(settings), NullLogger<PhotoApiService>.Instance);

        await service.UploadAsync([1], new Measurement(), null, null);

        handler.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task UploadAsync_WhenApiReturnsError_DoesNotThrow()
    {
        var handler = new TestMessageHandler { ResponseStatusCode = HttpStatusCode.InternalServerError };
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://api") };
        var factory = new TestHttpClientFactory(client);
        var settings = new PhotoApiSettings { Enabled = true, Url = "http://api", Token = "s" };
        var service = new PhotoApiService(factory, Options.Create(settings), NullLogger<PhotoApiService>.Instance);

        var act = () => service.UploadAsync([1], new Measurement(), null, null);

        await act.Should().NotThrowAsync();
    }

    // ── RecognizeAsync tests ─────────────────────────────────────────────────

    [Fact]
    public async Task RecognizeAsync_ReturnsResult_WhenApiResponds200()
    {
        var handler = new TestMessageHandler
        {
            ResponseBody = """{"sys":125,"dia":82,"pul":68,"confidence":0.92,"elapsed_ms":50}"""
        };
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://api") };
        var factory = new TestHttpClientFactory(client);
        var settings = new PhotoApiSettings { Enabled = true, Url = "http://api", Token = "s" };
        var service = new PhotoApiService(factory, Options.Create(settings), NullLogger<PhotoApiService>.Instance);

        var result = await service.RecognizeAsync([1, 2, 3]);

        result.Should().NotBeNull();
        result!.Sys.Should().Be(125);
        result.Dia.Should().Be(82);
        result.Pulse.Should().Be(68);
        result.Source.Should().Be("local_ocr");
        result.Confidence.Should().BeApproximately(0.92, 0.001);
    }

    [Fact]
    public async Task RecognizeAsync_ReturnsNull_WhenApiReturns422()
    {
        var handler = new TestMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.UnprocessableEntity,
            ResponseBody = """{"detail":"got 2 rows, need 3"}"""
        };
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://api") };
        var factory = new TestHttpClientFactory(client);
        var settings = new PhotoApiSettings { Enabled = true, Url = "http://api", Token = "s" };
        var service = new PhotoApiService(factory, Options.Create(settings), NullLogger<PhotoApiService>.Instance);

        var result = await service.RecognizeAsync([1, 2, 3]);

        result.Should().BeNull();
    }

    [Fact]
    public async Task RecognizeAsync_ReturnsNull_WhenApiTimesOut()
    {
        var handler = new TimeoutMessageHandler();
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://api") };
        var factory = new TestHttpClientFactory(client);
        var settings = new PhotoApiSettings { Enabled = true, Url = "http://api", Token = "s" };
        var service = new PhotoApiService(factory, Options.Create(settings), NullLogger<PhotoApiService>.Instance);

        var result = await service.RecognizeAsync([1, 2, 3]);

        result.Should().BeNull();
    }

    [Fact]
    public async Task RecognizeAsync_ReturnsNull_WhenEnabledIsFalse()
    {
        var handler = new TestMessageHandler();
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://api") };
        var factory = new TestHttpClientFactory(client);
        var settings = new PhotoApiSettings { Enabled = false };
        var service = new PhotoApiService(factory, Options.Create(settings), NullLogger<PhotoApiService>.Instance);

        var result = await service.RecognizeAsync([1, 2, 3]);

        result.Should().BeNull();
        handler.LastRequest.Should().BeNull();
    }
}
