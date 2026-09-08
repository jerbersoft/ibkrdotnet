# Dates and times

No `DateTime`, `DateTimeOffset` or `TimeSpan` appears anywhere in the public API of either package. Every date and
time value is a [NodaTime](https://nodatime.org) type. This is the load-bearing design decision in the library,
and IBKR is the reason for it.

## Ten encodings, sometimes two for the same value

| Wire format | Example | Where | Type |
| --- | --- | --- | --- |
| epoch **seconds** | `1754948718` | `ledger.timestamp`, `last_trade_time` | `Instant` |
| epoch **milliseconds** | `1702317649000` | `trade_time_r`, watchlist `modified` | `Instant` |
| epoch ms **as a string** | `"1714061006000"` | `lastExecutionTime_r` | `Instant` |
| `YYYYMMDD-hh:mm:ss` | `"20231211-18:00:49"` | `trade_time`, `startTime` | `Instant` |
| `YYMMDDhhmmss` | `"240425160326"` | `lastExecutionTime`, `order_time` | `Instant` |
| `yyyyMMdd` | `"20240315"` | `expiry`, `maturityDate` | `LocalDate` |
| `HHmm` | `"0035"`, `"2330"` | `openingTime`, `closingTime` | `LocalTime` |
| `h:mm tt` | `"4:15 PM"` | event contract schedules | `LocalTime` |
| `yyyy-MM-dd HH:mm:ss` | `"2026-09-08 17:33:08"` | PortfolioAnalyst `lastSuccessfulUpdate` | `Instant` |
| IANA zone id | `"America/New_York"` | schedule `timezone` | `DateTimeZone` |

There is no single global converter that works on that. Each property declares the converter for its own
documented format, so `ledger.RetrievedAt` (seconds) and `trade.TradeTime` (milliseconds) both arrive as a correct
`Instant` without the caller knowing which was which.

Where IBKR sends the same moment twice — `trade_time` beside `trade_time_r`, `lastExecutionTime` beside
`lastExecutionTime_r` — a real fill against a live gateway confirms the text halves are UTC and that both
decodings land on the same instant. That is asserted by the live sweep rather than assumed.

## Why an enum of formats would not have been enough

The event contract schedule sends `4:15 PM`. Its converter is separate from the `HHmm` one deliberately: merged,
a schedule that started sending `0415` would be read as a quarter past four in the *morning* and nothing would
complain. Kept apart, it fails loudly.

Their zone is `US/Central` — a TZDB backward-compatibility link, not the canonical `America/Chicago` the rest of
the API sends. NodaTime's TZDB provider resolves it; a test pins that it still does.

## The types you will meet

**`Instant`** — an unambiguous moment. Anything that is a point in time.

**`LocalDate`** — a calendar date with no zone. Expiries, maturities.

**`LocalTime`** — a wall-clock time with no date and no zone. Venue opening and closing times, which are in the
venue's own zone, reported separately as an IANA identifier.

**`DateTimeZone`** — resolved through the injected `IDateTimeZoneProvider`, TZDB by default.

**`Duration`** — bar lengths, timeouts, rate-limit windows, session lifetimes.

**`BarSize`** and **`HistoryPeriod`** are this library's own, because IBKR's `"5min"` and `"6m"` are neither a
`Duration` nor a `Period`:

```csharp
BarSize.FiveMinutes           // "5min"
BarSize.Minutes(15)           // "15min"
BarSize.Parse("1h")

HistoryPeriod.OneMonth        // "1m"
HistoryPeriod.Days(5)         // "5d"
```

**`TradingScheduleDate`** exists because IBKR's `tradingScheduleDate` can mean "any Saturday" rather than a date.
A `LocalDate` cannot represent that and would have had to either throw or invent a day.

## Where a date arrives as text on purpose

Two places, both deliberate.

**PortfolioAnalyst series `dates`.** The encoding follows the sibling `freq`: `"20260908"` while that is `D`,
`"202609"` while it is `M` — and both appear in one response, since a one-month request answers with daily NAV and
monthly interval returns. Parsing eagerly would mean failing on the monthly series or inventing a day of the month
IBKR did not send. The array is `IReadOnlyList<string>`, and `freq` tells you how to read it.

**PortfolioAnalyst transaction dates.** The documented `date` is Java's default rendering,
`Wed Nov 05 00:00:00 EST 2025`, whose zone is an abbreviation rather than an identifier and so is ambiguous across
the world's zones. Every transaction also carries an *undocumented* `rawDate` of `20251105`. `Transaction.Date`
binds the undocumented field and keeps the documented one as `DateText`.

## Testing time

`IClock` is injected everywhere "now" is needed — token expiry, tickle scheduling, OAuth nonces, rate-limit
windows — and registered with `TryAdd`, so a test substitutes `FakeClock` and moves time without waiting for it.

```csharp
services.AddSingleton<IClock>(new FakeClock(Instant.FromUtc(2026, 1, 1, 0, 0)));
```

Rate limiting goes further and abstracts "what time is it" and "wait this long" as a single interface, because
splitting them across two abstractions lets them drift apart under test — which is exactly the situation a rate
limiter must not be verified in.
