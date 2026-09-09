# Contributing

Thanks for looking. This is a .NET client for Interactive Brokers' Web API Trading surface — 90 of IBKR's 108
Trading endpoints so far, with the rest tracked in the
[milestones](https://github.com/jerbersoft/ibkrdotnet/milestones).

## Getting a build

```bash
dotnet restore IbkrDotNet.slnx
dotnet build   IbkrDotNet.slnx -c Release
dotnet test    --solution IbkrDotNet.slnx -c Release
```

Note `--solution`. `dotnet test IbkrDotNet.slnx` is rejected: a solution has to be passed by that flag.

**No IBKR credentials are needed.** Every test runs offline against fixtures, so a fresh clone goes green with no
gateway, no account and no key.

The documentation site builds locally too:

```bash
dotnet tool restore
dotnet docfx docs/docfx.json --serve
```

Use `docfx docs/docfx.json`, not `docfx build` — only the first form runs the metadata stage, and the generated
API reference under `docs/api/*.yml` is git-ignored, so `build` alone produces a site with no reference section.

## Workflow

1. Open an issue first for anything larger than a typo.
2. Branch from `master` — `feat/`, `fix/`, `docs/`, `chore/` or `ci/` plus a slice name.
3. Commit in conventional-commit form, referencing the issue.
4. Open a pull request and wait for **`Build and test`** and **`Docs — build`** to go green.
5. Merge once they are:

```bash
gh pr merge <number> --squash --delete-branch
```

`master` requires a pull request, both named checks, a branch that is up to date, resolved review conversations
and linear history; force-push and deletion are blocked. **No approving review is required**, deliberately — a
solo maintainer cannot approve their own pull request, and a rule that can only be satisfied by overriding it
trains an override that skips the required checks too. Restore the review requirement the day a second
maintainer can supply one.

## Four gates that fail the build

Each of these exists because the alternative is a defect that hides rather than fails.

**NodaTime, exclusively.** No `DateTime`, `DateTimeOffset` or `TimeSpan` may appear in a public signature —
`Instant`, `LocalDate`, `LocalTime`, `DateTimeZone` and `Duration` instead. IBKR encodes time ten different ways
across the API (epoch seconds, epoch milliseconds, epoch milliseconds *as a string*, `YYYYMMDD-hh:mm:ss`,
`YYMMDDhhmmss`, `yyyyMMdd`, `HHmm`, IANA zone ids, and durations in both seconds and milliseconds), so there is
no single global converter — each property names its own. A BCL date type in the public surface means a unit was
guessed somewhere.

**The public API transcript.** `tests/*/PublicApi/*.approved.txt` holds the full public surface of both packages.
Any change to it fails the test and writes a `.received.txt` beside it. Read the diff; if the change is
intended, copy `.received.txt` over `.approved.txt` and delete the received file. This is also what enforces the
rule above — it is the file that would show a `System.DateTime` appearing.

**The documentation site tests.** `DocsSiteTests` pins two things DocFX will not tell you about: every project
under `src/` must be named in `docs/docfx.json`, in both directions, and every `docs/guides/*.md` must appear in
`docs/guides/toc.yml`. DocFX builds an orphaned page without a word of complaint.

**Documentation warnings are errors in CI.** `Docs — build` runs with `--warningsAsErrors`, because a DocFX
warning is almost always a cross-reference that will not resolve on the published site. A relative link that
works on GitHub can still fail there.

## Design conventions

* **Every response shape comes from a real payload.** Fixtures under `tests/**/Fixtures/Responses/` are IBKR's
  own documented examples, or captures from a live gateway with account numbers redacted to `U1234567`. Do not
  hand-write a fixture from a field list — IBKR's documentation and its live responses disagree often enough
  that the disagreement is usually the interesting part, and several of the README's notes exist because of one.
* **Strongly-typed identifiers.** `ConId`, `AccountId`, `OrderId`, `ExecutionId` — not bare strings or ints.
* **Everything asynchronous takes a `CancellationToken`** and returns `Task<T>`.
* **DTOs are `record` types** with `init` accessors and `IReadOnlyList<T>` collections.
* **Rate limits are the library's problem, not the caller's.** IBKR responds to a breach by putting the calling
  IP address in a ten-minute penalty box, and blocks repeat offenders — so the published limits ship as data and
  are enforced by default, and a `429` is fed back into the pacing rather than merely reported.

## Credentials never enter the repository

`.gitignore` covers `.env`, `*.pem`, `*.key`, `*.pfx`, `*.p12`, `*.jks` and `secrets.json`. The README's setup
instructions have you run `openssl genrsa -out privatekey.pem 3072`, which writes into whatever directory you
are standing in — generate keys outside any repository, and see [SECURITY.md](SECURITY.md) for the rest.

## Reporting a security problem

Not through an issue. See **[SECURITY.md](SECURITY.md)**.
