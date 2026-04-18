using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using BpTracker.Api.Data;
using BpTracker.Api.Models;

class Program {
    static void Main() {
        var services = new ServiceCollection();
        // Just checking if we can mock or test EF compile
        // Actually it will only fail at runtime when executing SQL.
    }
}
