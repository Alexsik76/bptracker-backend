using System.Collections.Generic;

namespace BpTracker.Api.Models;

public class AuthSettings
{
    public HashSet<string> AllowedEmails { get; set; } = [];
}
