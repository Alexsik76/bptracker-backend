using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BpTracker.Api.DTOs;
using BpTracker.Api.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace BpTracker.Api.Tests.Measurements;

public class WithPhotoTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory = factory;

    [Fact]
    public async Task PostWithPhoto_ValidRequest_ReturnsCreated()
    {
        var (_, token) = await TestUser.CreateAsync(_factory);
        var client = _factory.CreateClient().AuthAs(token);

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("120"), "sys");
        content.Add(new StringContent("80"), "dia");
        content.Add(new StringContent("70"), "pulse");
        content.Add(new StringContent("125"), "geminiSys");
        content.Add(new StringContent("85"), "geminiDia");
        content.Add(new StringContent("75"), "geminiPulse");

        var fileContent = new ByteArrayContent([1, 2, 3, 4, 5]);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        content.Add(fileContent, "image", "test.jpg");

        var response = await client.PostAsync("/api/v1/measurements/with-photo", content);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<MeasurementDto>();
        body.Should().NotBeNull();
        body!.Sys.Should().Be(120);
        body.Dia.Should().Be(80);
        body.Pulse.Should().Be(70);
    }

    [Fact]
    public async Task PostWithPhoto_NoAuth_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsync("/api/v1/measurements/with-photo", new MultipartFormDataContent());
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("sys", "400")] // sys too high
    [InlineData("sys", "30")]  // sys too low
    [InlineData("dia", "210")] // dia too high
    [InlineData("dia", "10")]  // dia too low
    public async Task PostWithPhoto_InvalidValues_Returns400(string field, string value)
    {
        var (_, token) = await TestUser.CreateAsync(_factory);
        var client = _factory.CreateClient().AuthAs(token);

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(field == "sys" ? value : "120"), "sys");
        content.Add(new StringContent(field == "dia" ? value : "80"), "dia");
        content.Add(new StringContent("70"), "pulse");
        content.Add(new ByteArrayContent([1]), "image", "test.jpg");

        var response = await client.PostAsync("/api/v1/measurements/with-photo", content);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostWithPhoto_MissingImage_Returns400()
    {
        var (_, token) = await TestUser.CreateAsync(_factory);
        var client = _factory.CreateClient().AuthAs(token);

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("120"), "sys");
        content.Add(new StringContent("80"), "dia");
        content.Add(new StringContent("70"), "pulse");

        var response = await client.PostAsync("/api/v1/measurements/with-photo", content);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var err = await response.Content.ReadFromJsonAsync<JsonElement>();
        err.GetProperty("error").GetString().Should().Contain("Image file is missing");
    }
}
