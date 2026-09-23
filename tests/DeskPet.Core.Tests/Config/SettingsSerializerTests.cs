using DeskPet.Core.Config;

namespace DeskPet.Core.Tests.Config;

public sealed class SettingsSerializerTests
{
    [Fact]
    public void RoundTripsAllValues()
    {
        var original = new Settings
        {
            Goal = "Ship \"v1\"\nsoon",
            Model = "claude-x",
            PrivacyAcknowledged = true,
            ForegroundPollMs = 300,
            ApiTimeoutSeconds = 5,
            AllowList = ["Zoom"],
            DistractionKeywords = ["Chess"],
        };

        Assert.True(SettingsSerializer.TryDeserialize(SettingsSerializer.Serialize(original), out var copy));
        Assert.Equal(original.Goal, copy.Goal);
        Assert.Equal(original.Model, copy.Model);
        Assert.True(copy.PrivacyAcknowledged);
        Assert.Equal(300, copy.ForegroundPollMs);
        Assert.Equal(5, copy.ApiTimeoutSeconds);
        Assert.Equal(["Zoom"], copy.AllowList);
        Assert.Equal(["Chess"], copy.DistractionKeywords);
        Assert.Equal(original.IgnoreProcesses, copy.IgnoreProcesses);
    }

    [Fact]
    public void SerializesIndentedCamelCase()
    {
        var json = SettingsSerializer.Serialize(new Settings());

        Assert.Contains("\n", json);
        Assert.Contains("\"maxApiCallsPerMinute\": 6", json);
        Assert.DoesNotContain("\"MaxApiCallsPerMinute\"", json);
    }

    [Fact]
    public void NeverWritesAnyKeyLikeProperty() =>
        Assert.DoesNotContain("key\"", SettingsSerializer.Serialize(new Settings()), StringComparison.OrdinalIgnoreCase);

    [Fact]
    public void MissingPropertiesKeepDefaultsAndUnknownAreIgnored()
    {
        Assert.True(SettingsSerializer.TryDeserialize("""{"goal":"Read","apiKey":"sk-secret","futureThing":{"a":1}}""", out var settings));

        Assert.Equal("Read", settings.Goal);
        Assert.Equal(new Settings().Model, settings.Model);
        Assert.Equal(new Settings().IgnoreTitleKeywords, settings.IgnoreTitleKeywords);
    }

    [Fact]
    public void ToleratesCommentsTrailingCommasAndPascalCase()
    {
        const string json = """
            {
              // hand edited
              "MaxApiCallsPerMinute": 3,
            }
            """;

        Assert.True(SettingsSerializer.TryDeserialize(json, out var settings));
        Assert.Equal(3, settings.MaxApiCallsPerMinute);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"goal\":")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("42")]
    [InlineData("{\"maxApiCallsPerMinute\":\"lots\"}")]
    public void InvalidInputReturnsDefaults(string? json)
    {
        Assert.False(SettingsSerializer.TryDeserialize(json, out var settings));
        Assert.Equal(SettingsSerializer.Serialize(new Settings()), SettingsSerializer.Serialize(settings));
    }

    [Fact]
    public void ClampsNonsensicalNumbers()
    {
        const string json = """
            {"foregroundPollMs":-5,"dwellSeconds":-1,"enumerateSeconds":0,"haulAfterSeconds":-2,"pullAfterSeconds":-3,
             "windowCooldownSeconds":-4,"minActionGapSeconds":-5,"maxActionsPerTenMinutes":-6,
             "maxApiCallsPerMinute":-7,"apiTimeoutSeconds":-8}
            """;

        Assert.True(SettingsSerializer.TryDeserialize(json, out var s));
        Assert.Equal(50, s.ForegroundPollMs);
        Assert.Equal(0, s.DwellSeconds);
        Assert.Equal(1, s.EnumerateSeconds);
        Assert.Equal(0, s.HaulAfterSeconds);
        Assert.Equal(0, s.PullAfterSeconds);
        Assert.Equal(0, s.WindowCooldownSeconds);
        Assert.Equal(0, s.MinActionGapSeconds);
        Assert.Equal(0, s.MaxActionsPerTenMinutes);
        Assert.Equal(0, s.MaxApiCallsPerMinute);
        Assert.Equal(1, s.ApiTimeoutSeconds);
    }

    [Fact]
    public void ClampsHugeTimeout()
    {
        Assert.True(SettingsSerializer.TryDeserialize("""{"apiTimeoutSeconds":1e300}""", out var s));
        Assert.Equal(120, s.ApiTimeoutSeconds);
    }

    [Fact]
    public void NullsBecomeDefaultsAndBlankListItemsAreDropped()
    {
        const string json = """{"goal":null,"model":" ","ignoreProcesses":null,"allowList":["Zoom",null,"  "]}""";

        Assert.True(SettingsSerializer.TryDeserialize(json, out var s));
        Assert.Equal(string.Empty, s.Goal);
        Assert.Equal(new Settings().Model, s.Model);
        Assert.Equal(new Settings().IgnoreProcesses, s.IgnoreProcesses);
        Assert.Equal(["Zoom"], s.AllowList);
    }

    [Fact]
    public void EmptyPrivacyListIsRespected()
    {
        Assert.True(SettingsSerializer.TryDeserialize("""{"ignoreProcesses":[]}""", out var s));
        Assert.Empty(s.IgnoreProcesses);
    }
}
