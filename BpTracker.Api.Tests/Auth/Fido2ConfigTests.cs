using System;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace BpTracker.Api.Tests.Auth;

public class Fido2ConfigTests
{
    [Fact]
    public void Fido2Origins_WhenAndroidOriginsProvided_CombinesThemCorrectly()
    {
        // Arrange
        var corsOrigins = "https://bptracker.home.vn.ua,https://another.domain.com";
        var androidOrigins = "android:apk-key-hash:yJpN7WGuMsRNBaLAWWyMYfYXIQzPMm7b75i-9JQRzfs,android:apk-key-hash:anotherhash";

        // Act
        var origins = corsOrigins
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Concat(androidOrigins
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToHashSet();

        // Assert
        origins.Should().Contain("https://bptracker.home.vn.ua");
        origins.Should().Contain("https://another.domain.com");
        origins.Should().Contain("android:apk-key-hash:yJpN7WGuMsRNBaLAWWyMYfYXIQzPMm7b75i-9JQRzfs");
        origins.Should().Contain("android:apk-key-hash:anotherhash");
        origins.Should().HaveCount(4);
    }

    [Fact]
    public void Fido2Origins_WhenAndroidOriginsEmpty_OnlyUsesCorsOrigins()
    {
        // Arrange
        var corsOrigins = "https://bptracker.home.vn.ua";
        var androidOrigins = "";

        // Act
        var origins = corsOrigins
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Concat(androidOrigins
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToHashSet();

        // Assert
        origins.Should().Contain("https://bptracker.home.vn.ua");
        origins.Should().HaveCount(1);
    }
}
