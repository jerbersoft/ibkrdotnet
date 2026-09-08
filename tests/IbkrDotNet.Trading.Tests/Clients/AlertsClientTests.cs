using IbkrDotNet.Trading.Clients;
using IbkrDotNet.Trading.Http;
using IbkrDotNet.Trading.Primitives;
using IbkrDotNet.Trading.Tests.TestSupport;
using NodaTime;
using Xunit;

namespace IbkrDotNet.Trading.Tests.Clients;

public class AlertsClientTests
{
    private const string Fixtures = "trading-alerts/";

    private static readonly AccountId Account = new("U1234567");

    [Fact]
    public async Task Reads_the_documented_listing()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture($"{Fixtures}get-alerts.documented.json");
        var client = new AlertsClient(harness.ApiClient);

        var alerts = await client.GetAllAsync(Account, TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/iserver/account/U1234567/alerts", harness.LastRequest.Path);

        var alert = Assert.Single(alerts);
        Assert.Equal(new OrderId(9876543210), alert.Id);
        Assert.Equal(Account, alert.Account);
        Assert.Equal("AAPL", alert.Name);
        Assert.True(alert.IsActive);
        Assert.False(alert.IsTriggered);
        Assert.False(alert.IsRepeatable);

        // "20240308-17:08:38", documented as UTC and read as UTC.
        Assert.Equal(Instant.FromUtc(2024, 3, 8, 17, 8, 38), alert.CreatedAt);
    }

    [Fact]
    public async Task Reads_an_account_with_no_alerts_as_an_empty_list()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture($"{Fixtures}get-alerts.empty.json");
        var client = new AlertsClient(harness.ApiClient);

        Assert.Empty(await client.GetAllAsync(Account, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Reads_the_documented_alert_details()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture($"{Fixtures}get-alert-details.documented.json");
        var client = new AlertsClient(harness.ApiClient);

        var alert = await client.GetDetailsAsync(
            new OrderId(833967258), TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/iserver/account/alert/833967258", harness.LastRequest.Path);

        Assert.Equal("IBM", alert.Name);
        Assert.Equal("GTC", alert.TimeInForce);
        Assert.Equal("Submitted", alert.OrderStatus);
        Assert.Equal("john.smith@example.com", alert.Email);
        Assert.Equal("alert message", alert.Message);
        Assert.Equal(1, alert.ConditionCount);
        Assert.True(alert.TriggersOutsideRegularTradingHours);
        Assert.Null(alert.ExpiresAt);

        // Every one of these is a JSON number on the wire, and each means yes or no.
        Assert.True(alert.IsActive);
        Assert.True(alert.SendsEmail);
        Assert.True(alert.ShowsPopup);
        Assert.True(alert.IsTriggered);
        Assert.False(alert.IsRepeatable);
        Assert.False(alert.SendsMobileNotifications);
        Assert.False(alert.IsReadOnly);

        var condition = Assert.Single(alert.Conditions);
        Assert.Equal(1, condition.ConditionType);
        Assert.Equal("8314@NYSE", condition.Conidex);
        Assert.Equal("IBM", condition.ContractDescription);
        Assert.Equal(">=", condition.Operator);
        Assert.Equal("500.00", condition.Value);
        Assert.True(condition.LogicBind);
        Assert.Null(condition.TimeZone);
    }

    [Fact]
    public async Task Supplies_the_query_type_the_details_endpoint_requires()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture($"{Fixtures}get-alert-details.documented.json");
        var client = new AlertsClient(harness.ApiClient);

        await client.GetDetailsAsync(new OrderId(833967258), TestContext.Current.CancellationToken);

        // IBKR's path signature shows no query string, but 'type' is required and has exactly one
        // allowed value; omitting it answers 400 "orderId and type are required". The caller is
        // never asked for it, so it has to be sent from here.
        Assert.Equal("?type=Q", harness.LastRequest.Query);
    }

    [Fact]
    public async Task Reads_the_documented_mobile_trading_assistant_alert()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture($"{Fixtures}get-mta.documented.json");
        var client = new AlertsClient(harness.ApiClient);

        var alert = await client.GetMobileTradingAssistantAsync(TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/iserver/account/mta", harness.LastRequest.Path);
        Assert.Equal("MTA (AutoAlert)", alert.Name);
        Assert.Equal("USD", alert.MtaCurrency);
        Assert.StartsWith("9:STATE=1,MIN=-260000", alert.MtaDefaults);

        // Past int.MaxValue, which is why the tool id is a long rather than an int.
        Assert.Equal(55834574848L, alert.ToolId);

        var condition = Assert.Single(alert.Conditions);
        Assert.Equal(5, condition.ConditionType);
        Assert.Equal("*@*", condition.Conidex);
        Assert.Equal("*", condition.Value);
    }

    [Fact]
    public async Task Carries_through_the_prose_ibkr_puts_in_the_alert_time_zone()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture($"{Fixtures}get-mta.documented.json");
        var client = new AlertsClient(harness.ApiClient);

        var alert = await client.GetMobileTradingAssistantAsync(TestContext.Current.CancellationToken);

        // The reason AlertDetails.TimeZone is a string while AlertCondition.TimeZone is a
        // DateTimeZone: this is IBKR's own published example response, and "all timezones can be
        // here" is not a zone. Resolving it eagerly would fail the whole read.
        Assert.Equal("all timezones can be here", alert.TimeZone);
    }

    [Fact]
    public async Task Resolves_a_condition_time_zone_against_tzdb()
    {
        using var harness = new ClientHarness();
        harness.RespondWithJson(
            """{"conditions":[{"condition_type":2,"condition_time_zone":"US/Eastern"}]}""");
        var client = new AlertsClient(harness.ApiClient);

        var alert = await client.GetMobileTradingAssistantAsync(TestContext.Current.CancellationToken);

        var zone = Assert.Single(alert.Conditions).TimeZone;
        Assert.NotNull(zone);

        // TWS names zones the way the event contract schedules do, with backward-compatibility
        // links rather than canonical ids, so the check is that it behaves like the canonical zone.
        var noon = new LocalDateTime(2026, 1, 15, 12, 0);
        Assert.Equal(
            noon.InZoneLeniently(DateTimeZoneProviders.Tzdb["America/New_York"]).ToInstant(),
            noon.InZoneLeniently(zone).ToInstant());
    }

    [Fact]
    public async Task Sends_the_activation_flag_as_the_number_ibkr_documents()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture($"{Fixtures}alert-action.documented.json");
        var client = new AlertsClient(harness.ApiClient);

        var result = await client.SetActiveAsync(
            Account, new OrderId(9876543210), active: true, TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Post, harness.LastRequest.Method);
        Assert.Equal("/v1/api/iserver/account/U1234567/alert/activate", harness.LastRequest.Path);

        // alertActive is documented as an enum over 1 and 0, not as a boolean.
        Assert.Equal("""{"alertId":9876543210,"alertActive":1}""", harness.LastRequest.Body);

        Assert.True(result.Success);
        Assert.Equal(new OrderId(833967258), result.Id);
        Assert.Equal("Request was submitted", result.Text);
    }

    [Fact]
    public async Task Sends_zero_when_deactivating()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture($"{Fixtures}alert-action.documented.json");
        var client = new AlertsClient(harness.ApiClient);

        await client.SetActiveAsync(
            Account, new OrderId(9876543210), active: false, TestContext.Current.CancellationToken);

        Assert.Equal("""{"alertId":9876543210,"alertActive":0}""", harness.LastRequest.Body);
    }

    [Fact]
    public async Task Deletes_by_account_and_alert()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture($"{Fixtures}alert-action.documented.json");
        var client = new AlertsClient(harness.ApiClient);

        var result = await client.DeleteAsync(
            Account, new OrderId(833967258), TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Delete, harness.LastRequest.Method);
        Assert.Equal("/v1/api/iserver/account/U1234567/alert/833967258", harness.LastRequest.Path);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task Reports_a_failed_write_rather_than_throwing()
    {
        using var harness = new ClientHarness();
        harness.RespondWithJson(
            """{"success":false,"text":"Request failed","failure_list":"833967258"}""");
        var client = new AlertsClient(harness.ApiClient);

        // A refused write is still 200, and IBKR names the alerts it could not act on. Turning that
        // into an exception would throw away the failure list.
        var result = await client.DeleteAsync(
            Account, new OrderId(833967258), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("833967258", result.FailureList);
    }

    // ---- Captured from a live gateway ------------------------------------------------------------
    //
    // The fixtures below end '.live.json'. In both of them IBKR contradicts its own reference: the
    // MTA alert reports an order status outside the documented set and sends real JSON booleans
    // where the reference promises numbers, and an unknown alert identifier is reported as a
    // success. The account identifier and order id in the MTA capture are replaced with
    // placeholders; every other value is verbatim.

    [Fact]
    public async Task Reads_an_order_status_outside_the_documented_set()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture($"{Fixtures}get-mta.live.json");
        var client = new AlertsClient(harness.ApiClient);

        var alert = await client.GetMobileTradingAssistantAsync(TestContext.Current.CancellationToken);

        // IBKR documents order_status as "Always returns 'Presubmitted'" over an allowed set of
        // Presubmitted and Submitted. A live gateway answers Inactive, which is why the property is
        // a string and not an enumeration.
        Assert.Equal("Inactive", alert.OrderStatus);
    }

    [Fact]
    public async Task Reads_a_live_alert_that_mixes_booleans_into_the_numeric_flags()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture($"{Fixtures}get-mta.live.json");
        var client = new AlertsClient(harness.ApiClient);

        var alert = await client.GetMobileTradingAssistantAsync(TestContext.Current.CancellationToken);

        // alert_triggered arrives as false where the reference types it as a long, while
        // alert_active beside it is still the number 1.
        Assert.False(alert.IsTriggered);
        Assert.True(alert.IsActive);

        // Absent rather than false: IBKR sends JSON null, and a flag it never set should not read as
        // a decision to turn the feature off.
        Assert.Null(alert.PlaysAudio);
        Assert.Null(alert.Name);
        Assert.Null(alert.Email);
        Assert.Null(alert.TimeZone);
        Assert.Null(alert.ExpiresAt);

        Assert.Equal(0, alert.ConditionCount);
        Assert.Empty(alert.Conditions);
    }

    [Fact]
    public async Task Turns_the_two_hundred_that_means_not_found_into_a_failure()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture($"{Fixtures}get-alert-details.not-found.live.json");
        var client = new AlertsClient(harness.ApiClient);

        // The whole point of the check: this response is 200 OK, so nothing about the transport
        // says the read failed, and every field of the alert would deserialize to null.
        var exception = await Assert.ThrowsAsync<IbkrApiException>(() =>
            client.GetDetailsAsync(new OrderId(1), TestContext.Current.CancellationToken));

        Assert.Contains("Alert with order ID=1 not found.", exception.Message, StringComparison.Ordinal);
        Assert.Equal(System.Net.HttpStatusCode.OK, exception.StatusCode);
    }

    [Fact]
    public async Task Rejects_a_null_api_client()
    {
        await Task.CompletedTask;
        Assert.Throws<ArgumentNullException>(() => new AlertsClient(null!));
    }
}
