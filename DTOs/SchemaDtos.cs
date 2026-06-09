using System.Text.Json;

namespace BpTracker.Api.DTOs;

public record CreateSchemaRequest(
    string Doctor,
    DateOnly? PrescribedOn,
    JsonElement Schedule,
    bool SetActive);

public record UpdateSchemaRequest(
    string Doctor,
    DateOnly? PrescribedOn,
    JsonElement Schedule);
