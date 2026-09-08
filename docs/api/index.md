# API reference

Every public member of both packages, generated from the XML documentation comments in the source. The same
comments ship inside the packages, so what is here is what IntelliSense shows at the call site.

`GenerateDocumentationFile` and `TreatWarningsAsErrors` are both on for every project under `src/`, so a public
member without a documentation comment does not compile in this repository.

## Namespaces

| Namespace | Package | Contents |
|---|---|---|
| <xref:IbkrDotNet.Trading> | `IbkrDotNet.Trading` | <xref:IbkrDotNet.Trading.IIbkrTradingClient>, the facade over all twelve endpoint clients |
| <xref:IbkrDotNet.Trading.Clients> | `IbkrDotNet.Trading` | The twelve endpoint groups, an interface and an implementation each |
| <xref:IbkrDotNet.Trading.Models.Orders> | `IbkrDotNet.Trading` | Tickets, the four-way <xref:IbkrDotNet.Trading.Models.Orders.OrderSubmissionResult>, executions |
| `IbkrDotNet.Trading.Models.*` | `IbkrDotNet.Trading` | The response models, one namespace per endpoint group |
| <xref:IbkrDotNet.Trading.Configuration> | `IbkrDotNet.Trading` | <xref:IbkrDotNet.Trading.Configuration.IbkrTradingOptions>, <xref:IbkrDotNet.Trading.Configuration.IbkrEnvironment> |
| <xref:IbkrDotNet.Trading.Authentication> | `IbkrDotNet.Trading` | <xref:IbkrDotNet.Trading.Authentication.IIbkrAuthenticator> and the gateway implementation |
| `IbkrDotNet.Trading.Authentication.OAuth1a` / `.OAuth2` | `IbkrDotNet.Trading` | The two handshakes and their options |
| <xref:IbkrDotNet.Trading.Http> | `IbkrDotNet.Trading` | <xref:IbkrDotNet.Trading.Http.IIbkrApiClient>, <xref:IbkrDotNet.Trading.Http.IbkrRequest>, the exception family |
| <xref:IbkrDotNet.Trading.Http.RateLimiting> | `IbkrDotNet.Trading` | <xref:IbkrDotNet.Trading.Http.RateLimiting.IbkrRateLimits>, the registry and the handler |
| <xref:IbkrDotNet.Trading.Session> | `IbkrDotNet.Trading` | <xref:IbkrDotNet.Trading.Session.IIbkrSessionManager> |
| <xref:IbkrDotNet.Trading.Time> | `IbkrDotNet.Trading` | <xref:IbkrDotNet.Trading.Time.BarSize>, <xref:IbkrDotNet.Trading.Time.HistoryPeriod>, <xref:IbkrDotNet.Trading.Time.TradingScheduleDate> |
| <xref:IbkrDotNet.Trading.Primitives> | `IbkrDotNet.Trading` | The typed identifiers — `ConId`, `AccountId`, `OrderId` |
| <xref:IbkrDotNet.Trading.Serialization> | `IbkrDotNet.Trading` | The shared `JsonSerializerOptions` and the converters |
| <xref:IbkrDotNet.Extensions.DependencyInjection> | `IbkrDotNet.Extensions.DependencyInjection` | `AddIbkrTrading`, the authentication selectors, the keep-alive |

## Two things no single member's page can say

**The converters are documented because they are reachable, not because they are an entry point.** IBKR encodes
time ten different ways, so each property declares the converter for its own documented format rather than a
global one being applied. Read a converter when you want to know exactly which wire spelling a value round-trips
as — each says, and says what an unrecognised value does. [Dates and times](../guides/dates-and-times.md) is the
overview.

**A model's remarks are often the most useful thing on its page.** Where IBKR's documented shape and its real one
disagree, the difference is written on the member it affects, with what a live gateway actually sent. The larger
patterns are collected in the [reference](../../README.md#things-about-ibkr-that-will-otherwise-surprise-you);
the per-field detail is here.
