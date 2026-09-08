using System.Text.Json;
using IbkrDotNet.Trading.Models.Session;
using IbkrDotNet.Trading.Serialization;
using IbkrDotNet.Trading.Time;
using NodaTime;
using Xunit;

namespace IbkrDotNet.Trading.Tests.Serialization;

public class SsoExpiryConverterTests
{
    // 2026-09-08T11:46:33Z, the value a live Client Portal Gateway sent for EXPIRES.
    private const long AbsoluteMilliseconds = 1_788_867_993_036L;

    // IBKR's own documented example for the same field: just under seven minutes remaining.
    private const long RelativeMilliseconds = 415_890L;

    private static SsoValidationResponse Read(long expires) =>
        JsonSerializer.Deserialize<SsoValidationResponse>(
            $$"""{"RESULT":true,"EXPIRES":{{expires}}}""",
            IbkrJson.Options)!;

    [Fact]
    public void Reads_the_documented_relative_shape_as_a_remaining_duration()
    {
        var expiry = Read(RelativeMilliseconds).Expires!.Value;

        Assert.Equal(Duration.FromMilliseconds(RelativeMilliseconds), expiry.Remaining);
        Assert.Null(expiry.At);
    }

    [Fact]
    public void Reads_the_absolute_shape_a_live_gateway_sends_as_an_instant()
    {
        // The bug this guards: read as a duration, this is a session lasting fifty-six years, and
        // nothing about it fails or looks wrong until someone schedules a re-authentication on it.
        var expiry = Read(AbsoluteMilliseconds).Expires!.Value;

        Assert.Equal(Instant.FromUnixTimeMilliseconds(AbsoluteMilliseconds), expiry.At);
        Assert.Null(expiry.Remaining);
    }

    [Fact]
    public void Resolves_both_shapes_to_the_same_answer_against_a_clock()
    {
        var now = Instant.FromUnixTimeMilliseconds(AbsoluteMilliseconds) - Duration.FromMilliseconds(RelativeMilliseconds);

        var absolute = SsoExpiry.FromMilliseconds(AbsoluteMilliseconds);
        var relative = SsoExpiry.FromMilliseconds(RelativeMilliseconds);

        Assert.Equal(absolute.ExpiresAt(now), relative.ExpiresAt(now));
        Assert.Equal(absolute.ExpiresIn(now), relative.ExpiresIn(now));
    }

    [Fact]
    public void Writes_back_the_shape_it_read()
    {
        foreach (var milliseconds in new[] { RelativeMilliseconds, AbsoluteMilliseconds })
        {
            var json = JsonSerializer.Serialize(
                new SsoValidationResponse { Expires = SsoExpiry.FromMilliseconds(milliseconds) },
                IbkrJson.Options);

            Assert.Contains($"\"EXPIRES\":{milliseconds}", json, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Reads_an_absent_value_as_null()
    {
        Assert.Null(
            JsonSerializer.Deserialize<SsoValidationResponse>(
                """{"RESULT":true,"EXPIRES":null}""", IbkrJson.Options)!.Expires);

        Assert.Null(
            JsonSerializer.Deserialize<SsoValidationResponse>(
                """{"RESULT":true}""", IbkrJson.Options)!.Expires);
    }
}
