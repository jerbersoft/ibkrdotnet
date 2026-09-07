# IbkrDotNet

A .NET client for the [Interactive Brokers Web API](https://www.interactivebrokers.com/campus/ibkr-api-page/web-api-trading/) **Trading** surface.

| Package | Description |
| --- | --- |
| `IbkrDotNet.Trading` | The client: endpoint groups, models, authentication and transport. |
| `IbkrDotNet.Extensions.DependencyInjection` | `AddIbkrTrading(...)` registration for `Microsoft.Extensions.DependencyInjection`. |

Targets `net10.0`. Every date and time value in the public API is a [NodaTime](https://nodatime.org) type — there is no `DateTime`, `DateTimeOffset` or `TimeSpan` anywhere in it.

> Status: in development. See the [milestones](https://github.com/jerbersoft/ibkrdotnet/milestones) for what is implemented.

## Documentation

Full usage documentation lands with issue [#14](https://github.com/jerbersoft/ibkrdotnet/issues/14).

## Working on this repository

```bash
dotnet build IbkrDotNet.slnx -c Release
dotnet test -c Release
```

`tools/fetch-spec.sh` downloads IBKR's reference documentation as Markdown into a gitignored
`artifacts/spec/` directory. IBKR serves a clean Markdown rendering of any docs page by appending
`.md` to its URL, which makes it a reliable source when adding or verifying endpoint models.

## License

MIT. See [LICENSE](LICENSE).
