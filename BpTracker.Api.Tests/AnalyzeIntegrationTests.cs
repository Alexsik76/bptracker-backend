using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BpTracker.Api.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace BpTracker.Api.Tests;

public class AnalyzeIntegrationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory = factory;

    [Fact]
    public async Task Analyze_WhenNoCookie_Returns401()
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.PostAsync("/api/v1/measurements/analyze", new MultipartFormDataContent());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Analyze_ReturnsLocalResult_WhenPhotoApiSucceeds()
    {
        _factory.PhotoApiService.Behavior = PhotoApiBehavior.Success;
        try
        {
            var (_, token) = await TestUser.CreateAsync(_factory);
            var client = _factory.CreateClient().AuthAs(token);

            var response = await client.PostAsync("/api/v1/measurements/analyze", CreateImageContent());

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            body.GetProperty("source").GetString().Should().Be("local_ocr");
            body.GetProperty("sys").GetInt32().Should().Be(125);
        }
        finally
        {
            _factory.PhotoApiService.Behavior = PhotoApiBehavior.Failed;
        }
    }

    [Fact]
    public async Task Analyze_FallsBackToGemini_WhenLocalReturnsNull()
    {
        // PhotoApiService defaults to Failed (returns null); GeminiService defaults to success
        var (_, token) = await TestUser.CreateAsync(_factory);
        var client = _factory.CreateClient().AuthAs(token);

        var response = await client.PostAsync("/api/v1/measurements/analyze", CreateImageContent());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("source").GetString().Should().Be("gemini");
    }

    [Fact]
    public async Task Analyze_Returns422_WhenBothFail()
    {
        _factory.GeminiService.ExceptionToThrow = new InvalidOperationException("OCR failed");
        try
        {
            var (_, token) = await TestUser.CreateAsync(_factory);
            var client = _factory.CreateClient().AuthAs(token);

            var response = await client.PostAsync("/api/v1/measurements/analyze", CreateImageContent());

            response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            body.GetProperty("error").GetString().Should().NotBeNullOrEmpty();
        }
        finally
        {
            _factory.GeminiService.ExceptionToThrow = null;
        }
    }

    private static MultipartFormDataContent CreateImageContent()
    {
        var content = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent([0xFF, 0xD8, 0xFF]);
        imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        content.Add(imageContent, "image", "test.jpg");
        return content;
    }
}
