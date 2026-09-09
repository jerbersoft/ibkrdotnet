# Development

```sh
dotnet build IbkrDotNet.slnx -c Release
dotnet test  --solution IbkrDotNet.slnx -c Release
dotnet pack  IbkrDotNet.slnx -c Release
```

Warnings are errors, and `GenerateDocumentationFile` is on for both shipping projects, so a public member without
a documentation comment does not compile.

## The public API transcript

Both packages carry a transcript of their public surface under `tests/*/PublicApi/`, and a test fails when the
assembly stops matching it.

Both are published, so every public signature is somebody's compile error once it changes — and nothing else in
the suite would notice a widened parameter, a property that quietly became nullable, or a type that stopped being
sealed. The transcript makes that visible in review, where it can be a decision rather than an accident.

When a change is intended, the failure writes the new surface beside the approved one as `.received.txt`. Read
the diff, then replace the approved file with it.

## Fixtures

Response fixtures under `tests/IbkrDotNet.Trading.Tests/Fixtures/Responses/` are the example payloads from IBKR's
own reference pages, so deserialization is checked against what the API documents.

Fixtures ending **`.live.json`** were captured from a running gateway instead. They exist where the two disagree,
so a discrepancy is pinned by a test rather than rediscovered.

## The spec

```sh
tools/fetch-spec.sh
```

Downloads IBKR's reference documentation as Markdown into a gitignored `artifacts/spec/`. IBKR serves a clean
Markdown rendering of any docs page by appending `.md` to its URL, which makes it a reliable source when adding or
verifying an endpoint model. It is deliberately not vendored.

## The console tour

Read-only, against a locally running gateway: session status, accounts, balances, a quote, daily bars and an order
*preview*. It never places a live order.

```sh
dotnet run --project samples/IbkrDotNet.Samples.Console -- \
    --Ibkr:BaseAddress=https://localhost:5050 --TrustGatewayCertificate=true
```

## The live sweep

```sh
dotnet run --project samples/IbkrDotNet.Samples.Verify -- \
    --Ibkr:BaseAddress=https://localhost:5050 --TrustGatewayCertificate=true
```

Walks every implemented endpoint against a running gateway and prints one line each. It exists because unit tests
cannot see the two kinds of bug that matter most here: a transport problem only an intermediary produces, and a
response whose real shape contradicts the documented example the fixtures were built from.

**`--Orders=true`** adds the order write path: submits a limit order priced a quarter below the market so it rests
rather than fills, modifies it, and cancels it in a `finally` block.

**`--Fills=true`** lets an order execute — buys one share at market, reads the execution back, and sells it again
in a `finally`, checking the position returns to whatever the account started with rather than assuming it started
flat. This is the only way to reach the four execution-time encodings, and it earns its keep: the first run found
that a market order's `limit_price` arrives as `""`, which was failing the whole response. Unlike the rest of the
sweep, these checks assert on the data — IBKR sends each execution time twice, and the two decodings disagreeing
is a converter bug rather than a fact about the account.

Both flags are refused on anything but a paper account, and there is deliberately no flag to override that.

**Notification checks read and never write**, and there is no flag to make them write. Every other write path in
the sweep undoes itself; these change subscriptions and delivery settings on the username, and IBKR documents no
way to read a value before overwriting it or to re-register a device once deleted. The seven writes appear in the
report as skips saying what each would have changed, so the group is visible in full rather than half-absent.

**Watchlist checks do write** without a flag, because a watchlist cannot move money. One is created under a fixed
identifier, read back, and deleted in a `finally`; the identifier is checked against the existing lists first, so
a watchlist you created is never displaced.

**PortfolioAnalyst checks** are the slowest thing to repeat: each of the four endpoints allows one request per
fifteen minutes. The client will not block that long, so a second run inside the window reports them as skips
saying how much of it is left. Two of the four re-prove claims the library documents rather than trusting them —
that `lastSuccessfulUpdate` is UTC, and that `nd` counts calendar days rather than data points — so a comment that
drifts away from the API is caught by a run instead of by a caller.

No credential is read or printed. The gateway holds the login, and the account identifier is discovered at runtime
and masked on the way out, so the output can go straight into a bug report.

## The documentation site

```sh
dotnet tool restore
dotnet docfx docs/docfx.json --serve
```

DocFX is pinned in `.config/dotnet-tools.json` with `rollForward: false`, because the site's layout and the
generated YAML schema are both version-dependent and an unpinned tool would redesign the published site on
somebody else's release schedule.

`docfx docs/docfx.json` — not `docfx build` — because that form runs the metadata stage too. It has to:
`docs/api/*.yml` is gitignored, so on a fresh checkout the reference YAML does not exist and `build` alone would
publish a site with an empty API section.

CI builds it with `--warningsAsErrors`. A DocFX warning is almost always a link that will not resolve — a README
section renamed, a guide moved — which on a published site is a dead link nobody reports.

Two things DocFX does not check are pinned by `DocsSiteTests`: that `docs/docfx.json` lists exactly the projects
under `src/`, and that every page under `docs/guides/` is in the sidebar. DocFX builds an orphan page without a
word.

## Adding an endpoint group

1. `tools/fetch-spec.sh`, then read the group's pages under `artifacts/spec/`.
2. Model the responses as `record` types, one file per group, with a converter per date or time property.
3. Add the example payloads as fixtures and a deserialization test per endpoint.
4. Add the client interface and implementation; register both in the DI package and on `IIbkrTradingClient`.
5. Add the group's rate limits to `IbkrRateLimits.PerEndpoint` if IBKR publishes any.
6. Add it to the live sweep.
7. Update the public API transcript.

Step 6 is the one that finds things. The documentation and the API disagree often enough that a group verified
only against fixtures should say so.

## Contributing

This page is the build. The process around it — branch naming, the four gates that fail the build, the merge
policy on `master` — is in
[CONTRIBUTING.md](https://github.com/jerbersoft/ibkrdotnet/blob/master/CONTRIBUTING.md), and security reporting
is in [SECURITY.md](https://github.com/jerbersoft/ibkrdotnet/blob/master/SECURITY.md).
