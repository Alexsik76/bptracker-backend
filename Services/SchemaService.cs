using BpTracker.Api.Models;
using BpTracker.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace BpTracker.Api.Services;

public interface ISchemaService
{
    Task<TreatmentSchema?> GetActiveAsync();
}

public class SchemaService(AppDbContext context) : ISchemaService
{
    public async Task<TreatmentSchema?> GetActiveAsync()
    {
        return await context.TreatmentSchemas
            .FirstOrDefaultAsync(s => s.IsActive);
    }
}
