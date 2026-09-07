using System.Text.Json.Serialization;
using IbkrDotNet.Trading.Serialization.Converters;
using NodaTime;

namespace IbkrDotNet.Trading.Authentication.OAuth1a;

/// <summary>A verified live session token and the moment it stops being usable.</summary>
/// <param name="Token">The base64-encoded token.</param>
/// <param name="ExpiresAt">When the token expires.</param>
public sealed record LiveSessionToken(string Token, Instant ExpiresAt);

/// <summary>The response from <c>POST /v1/api/oauth/live_session_token</c>.</summary>
public sealed record LiveSessionTokenResponse
{
    /// <summary>
    /// IBKR's public Diffie-Hellman value, hex-encoded.
    /// </summary>
    /// <remarks>
    /// IBKR's endpoint reference names this field <c>diffie_hellman_challenge</c> while its
    /// integration guide calls it <c>diffie_hellman_response</c>; both are read here so the client
    /// works whichever the server sends.
    /// </remarks>
    [JsonPropertyName("diffie_hellman_challenge")]
    public string? DiffieHellmanChallenge { get; init; }

    /// <summary>An alternate spelling of <see cref="DiffieHellmanChallenge"/>.</summary>
    [JsonPropertyName("diffie_hellman_response")]
    public string? DiffieHellmanResponse { get; init; }

    /// <summary>
    /// The signature used to confirm the locally derived live session token matches IBKR's.
    /// </summary>
    [JsonPropertyName("live_session_token_signature")]
    public string? LiveSessionTokenSignature { get; init; }

    /// <summary>
    /// The <c>live_session_token_expiration</c> timestamp, as IBKR sent it.
    /// </summary>
    /// <remarks>
    /// IBKR's documentation disagrees with itself about what this value means: the field name and
    /// the integration guide call it the token's expiry, while the endpoint reference describes it
    /// as "time of live session token computation by IB" with tokens "valid for 24 hours from this
    /// time". Use <see cref="ResolveExpiry"/> rather than reading it directly.
    /// </remarks>
    [JsonPropertyName("live_session_token_expiration")]
    [JsonConverter(typeof(InstantEpochMillisecondsConverter))]
    public Instant? Expiration { get; init; }

    /// <summary>
    /// When the token stops being usable, resolving IBKR's ambiguity by looking at the value itself.
    /// </summary>
    /// <param name="now">The current time.</param>
    /// <remarks>
    /// A timestamp comfortably in the future can only be an expiry, so it is used as one. A
    /// timestamp at or near the present can only be the moment of computation, so the documented
    /// 24-hour validity is added to it. Deciding from the value avoids having to pick one reading
    /// of the documentation and be wrong in the expensive direction -- treating a computation time
    /// as an expiry would repeat the handshake on every request, while treating an expiry as a
    /// computation time would keep using a dead token for a day.
    /// </remarks>
    public Instant ResolveExpiry(Instant now) => Expiration is { } value && value > now + LookaheadThreshold
        ? value
        : (Expiration ?? now) + Duration.FromHours(24);

    /// <summary>
    /// How far ahead a timestamp must be to read as an expiry rather than a computation time. Wide
    /// enough that clock skew between the client and IBKR cannot flip the interpretation.
    /// </summary>
    private static readonly Duration LookaheadThreshold = Duration.FromMinutes(5);

    /// <summary>The server's public Diffie-Hellman value, whichever field carried it.</summary>
    public string? ServerPublicValue => DiffieHellmanChallenge ?? DiffieHellmanResponse;
}
