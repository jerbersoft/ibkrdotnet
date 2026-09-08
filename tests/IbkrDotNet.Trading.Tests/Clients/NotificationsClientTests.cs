using IbkrDotNet.Trading.Clients;
using IbkrDotNet.Trading.Models.Notifications;
using IbkrDotNet.Trading.Primitives;
using IbkrDotNet.Trading.Tests.TestSupport;
using NodaTime;
using Xunit;

namespace IbkrDotNet.Trading.Tests.Clients;

public class NotificationsClientTests
{
    [Fact]
    public async Task Unwraps_the_unread_count_from_its_single_field_envelope()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-notifications/get-unread-fyis.documented.json");
        var client = new NotificationsClient(harness.ApiClient);

        var count = await client.GetUnreadCountAsync(TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/fyi/unreadnumber", harness.LastRequest.Path);
        Assert.Equal(4, count);
    }

    [Fact]
    public async Task Reports_a_missing_unread_count_as_null_rather_than_zero()
    {
        // "no notifications" and "IBKR did not say" are different answers, and only one of them
        // means the inbox is clear.
        using var harness = new ClientHarness();
        harness.RespondWithJson("{}");
        var client = new NotificationsClient(harness.ApiClient);

        Assert.Null(await client.GetUnreadCountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Reads_a_notification_whose_date_is_a_quoted_epoch_with_a_fractional_part()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-notifications/get-all-fyis.documented.json");
        var client = new NotificationsClient(harness.ApiClient);

        var notifications = await client.GetAllAsync(10, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/fyi/notifications", harness.LastRequest.Path);
        Assert.Equal("?max=10", harness.LastRequest.Query);
        Assert.Equal(2, notifications.Count);

        var first = notifications[0];
        Assert.Equal("2024031947509444", first.Id);
        Assert.Equal("FYI: Changes in Analyst Ratings", first.Title);
        Assert.False(first.IsRead);
        Assert.True(notifications[1].IsRead);

        // 'D' arrives as "1710847062.0": seconds since the epoch, quoted, with a fraction IBKR
        // never populates.
        Assert.Equal(Instant.FromUnixTimeSeconds(1710847062), first.Date);

        // 'PF' is not one of the twenty-three codes IBKR documents, yet it is what IBKR's own
        // example returns. A closed enum here would fail the whole response.
        Assert.Equal(NotificationTypeCode.PortfolioFyis, first.TypeCode);
        Assert.Equal("PF", first.TypeCode?.Value);
    }

    [Fact]
    public async Task Sends_the_category_filters_as_comma_separated_codes()
    {
        using var harness = new ClientHarness();
        harness.RespondWithJson("[]");
        var client = new NotificationsClient(harness.ApiClient);

        await client.GetAllAsync(
            25,
            include: [NotificationTypeCode.OptionExpiration, NotificationTypeCode.DividendsAdvisory],
            exclude: [NotificationTypeCode.SystemMessages],
            notificationId: "2024031947509444",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(
            "?max=25&include=OE%2CDA&exclude=SM&id=2024031947509444",
            harness.LastRequest.Query);
    }

    [Fact]
    public async Task Omits_the_filters_that_were_not_asked_for()
    {
        using var harness = new ClientHarness();
        harness.RespondWithJson("[]");
        var client = new NotificationsClient(harness.ApiClient);

        await client.GetAllAsync(10, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("?max=10", harness.LastRequest.Query);
    }

    [Fact]
    public async Task Refuses_a_listing_of_nothing()
    {
        using var harness = new ClientHarness();
        var client = new NotificationsClient(harness.ApiClient);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.GetAllAsync(0, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Reads_the_settings_and_which_of_them_can_be_changed()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-notifications/get-fyi-settings.documented.json");
        var client = new NotificationsClient(harness.ApiClient);

        var settings = await client.GetSettingsAsync(TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/fyi/settings", harness.LastRequest.Path);
        Assert.Equal(2, settings.Count);

        var portfolio = settings[0];
        Assert.Equal(NotificationTypeCode.PortfolioFyis, portfolio.TypeCode);
        Assert.Equal("Portfolio FYIs", portfolio.Name);
        Assert.Equal("Notify me of recent activity affecting my portfolio holdings.", portfolio.Description);

        // 'A' and 'H' are numbers meaning yes or no.
        Assert.True(portfolio.IsChangeable);
        Assert.False(portfolio.IsDisclaimerAcknowledged);

        Assert.Equal(NotificationTypeCode.PositionTransfer, settings[1].TypeCode);
    }

    [Fact]
    public async Task Puts_the_type_code_in_the_path_when_changing_a_subscription()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-notifications/acknowledgement.documented.json");
        var client = new NotificationsClient(harness.ApiClient);

        var acknowledgement = await client.SetSubscribedAsync(
            NotificationTypeCode.OptionExpiration, enabled: false, TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Post, harness.LastRequest.Method);
        Assert.Equal("/v1/api/fyi/settings/OE", harness.LastRequest.Path);
        Assert.Equal("""{"enabled":false}""", harness.LastRequest.Body);

        Assert.True(acknowledgement.IsAcknowledged);

        // 'T' is how long IBKR took, in milliseconds.
        Assert.Equal(Duration.FromMilliseconds(10), acknowledgement.Elapsed);
    }

    [Fact]
    public async Task Refuses_a_type_code_that_was_never_given_a_value()
    {
        // A default NotificationTypeCode would render as an empty segment, addressing
        // /v1/api/fyi/settings/ -- which is the collection, not a category under it.
        using var harness = new ClientHarness();
        var client = new NotificationsClient(harness.ApiClient);

        await Assert.ThrowsAsync<ArgumentException>(
            () => client.SetSubscribedAsync(
                default, enabled: true, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Reads_a_disclaimer_by_type_code()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-notifications/get-fyi-disclaimer.documented.json");
        var client = new NotificationsClient(harness.ApiClient);

        var disclaimer = await client.GetDisclaimerAsync(
            NotificationTypeCode.BorrowAvailability, TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/fyi/disclaimer/BA", harness.LastRequest.Path);
        Assert.Equal(NotificationTypeCode.BorrowAvailability, disclaimer.TypeCode);
        Assert.StartsWith("This communication is provided for information purposes only", disclaimer.Text);
    }

    [Fact]
    public async Task Marks_a_disclaimer_read_with_a_put_and_no_body()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-notifications/acknowledgement.documented.json");
        var client = new NotificationsClient(harness.ApiClient);

        await client.MarkDisclaimerReadAsync(
            NotificationTypeCode.BorrowAvailability, TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Put, harness.LastRequest.Method);
        Assert.Equal("/v1/api/fyi/disclaimer/BA", harness.LastRequest.Path);
    }

    [Fact]
    public async Task Reads_the_delivery_options_and_the_devices_under_them()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-notifications/get-fyi-delivery.documented.json");
        var client = new NotificationsClient(harness.ApiClient);

        var options = await client.GetDeliveryOptionsAsync(TestContext.Current.CancellationToken);

        Assert.Equal("/v1/api/fyi/deliveryoptions", harness.LastRequest.Path);

        // 'M' is a number and 'A' is the string "1"; both mean enabled.
        Assert.True(options.IsEmailEnabled);

        var device = Assert.Single(options.Devices);
        Assert.Equal("iPhone", device.Name);
        Assert.Equal("apn://mtws@SDFSDFDSFS123123DSFSDF", device.Id);
        Assert.Equal("apn://mtws@SDFSDFDSFS123123DSFSDF", device.UniqueId);
        Assert.True(device.IsEnabled);
    }

    [Fact]
    public async Task Sends_the_whole_device_back_when_toggling_it()
    {
        // IBKR documents every field of the body as optional and does not say which one it matches
        // on, so guessing at one field would be a guess about which device gets changed.
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-notifications/acknowledgement.documented.json");
        var client = new NotificationsClient(harness.ApiClient);

        await client.SetDeviceEnabledAsync(
            new NotificationDevice { Name = "iPhone", Id = "apn://a", UniqueId = "apn://b" },
            enabled: false,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Post, harness.LastRequest.Method);
        Assert.Equal("/v1/api/fyi/deliveryoptions/device", harness.LastRequest.Path);
        Assert.Equal(
            """{"deviceName":"iPhone","deviceId":"apn://a","uiName":"apn://b","enabled":false}""",
            harness.LastRequest.Body);
    }

    [Fact]
    public async Task Toggles_email_through_the_query_string_rather_than_a_body()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-notifications/acknowledgement.documented.json");
        var client = new NotificationsClient(harness.ApiClient);

        await client.SetEmailEnabledAsync(enabled: true, TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Put, harness.LastRequest.Method);
        Assert.Equal("/v1/api/fyi/deliveryoptions/email", harness.LastRequest.Path);
        Assert.Equal("?enabled=true", harness.LastRequest.Query);
    }

    [Fact]
    public async Task Reads_back_the_state_a_notification_was_left_in()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-notifications/read-fyi-notification.documented.json");
        var client = new NotificationsClient(harness.ApiClient);

        var result = await client.MarkReadAsync(
            "2024031947509444", TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Put, harness.LastRequest.Method);
        Assert.Equal("/v1/api/fyi/notifications/2024031947509444", harness.LastRequest.Path);

        Assert.True(result.IsAcknowledged);
        Assert.Equal(Duration.FromMilliseconds(5), result.Elapsed);
        Assert.Equal("2024031947509444", result.State?.Id);
        Assert.True(result.State?.IsRead);
    }

    [Fact]
    public async Task Deletes_a_device_without_expecting_a_payload_back()
    {
        // The endpoint answers 200 with an empty body. Asking for a response type would turn every
        // success into an IbkrApiException.
        using var harness = new ClientHarness();
        harness.RespondWithJson(string.Empty);
        var client = new NotificationsClient(harness.ApiClient);

        await client.DeleteDeviceAsync(
            "apn://mtws@SDFSDFDSFS123123DSFSDF", TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Delete, harness.LastRequest.Method);
        Assert.Equal(
            "/v1/api/fyi/deliveryoptions/apn%3A%2F%2Fmtws%40SDFSDFDSFS123123DSFSDF",
            harness.LastRequest.Path);
    }

    // ---- Captured from a live gateway ---------------------------------------------------------
    //
    // The fixtures below end '.live.json' because this group is where IBKR's documentation and its
    // API diverge most. The reference publishes twenty-three type codes; a live gateway returns
    // thirty, eleven of which are not on the list. It documents the read flag as a string and sends
    // a number. It documents a field the gateway never sends, and sends one it never documented.
    // Each of those would be a deserialization failure of the whole response under a stricter model.

    [Fact]
    public async Task Reads_the_type_codes_a_live_gateway_returns_but_ibkr_never_documented()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-notifications/get-fyi-settings.live.json");
        var client = new NotificationsClient(harness.ApiClient);

        var settings = await client.GetSettingsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(30, settings.Count);

        // None of these five is in IBKR's published table of type codes. An enum would have turned
        // the whole settings call into a failure; the open struct reads them unchanged.
        var codes = settings.Select(s => s.TypeCode?.Value).ToArray();
        Assert.Contains("OI", codes);
        Assert.Contains("AA", codes);
        Assert.Contains("NS", codes);
        Assert.Contains("SP", codes);
        Assert.Contains("TP", codes);
    }

    [Fact]
    public async Task Reads_a_live_settings_entry_that_carries_nothing_but_a_code()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-notifications/get-fyi-settings.live.json");
        var client = new NotificationsClient(harness.ApiClient);

        var settings = await client.GetSettingsAsync(TestContext.Current.CancellationToken);

        // IBKR documents every field as optional and means it: this row has no name, no description
        // and no changeability flag.
        var bare = Assert.Single(settings, s => s.TypeCode?.Value == "NS");
        Assert.Null(bare.Name);
        Assert.Null(bare.Description);
        Assert.Null(bare.IsChangeable);
        Assert.False(bare.IsDisclaimerAcknowledged);
    }

    [Fact]
    public async Task Carries_through_the_undocumented_settings_field()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-notifications/get-fyi-settings.live.json");
        var client = new NotificationsClient(harness.ApiClient);

        var settings = await client.GetSettingsAsync(TestContext.Current.CancellationToken);

        // 'SS' appears nowhere in IBKR's reference. It is on a minority of categories, always with
        // the same value, so it is passed through rather than interpreted.
        var withSs = settings.Where(s => s.SS is not null).ToArray();
        Assert.NotEmpty(withSs);
        Assert.All(withSs, s => Assert.Equal("TA_FYI", s.SS));
    }

    [Fact]
    public async Task Distinguishes_a_category_with_an_unacknowledged_disclaimer_from_one_without()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-notifications/get-fyi-settings.live.json");
        var client = new NotificationsClient(harness.ApiClient);

        var settings = await client.GetSettingsAsync(TestContext.Current.CancellationToken);

        // 'H' is set on the two categories this account actually receives, and unset on M8 -- which
        // does have a disclaimer, so the flag cannot mean "a disclaimer exists".
        Assert.True(Assert.Single(settings, s => s.TypeCode?.Value == "PF").IsDisclaimerAcknowledged);
        Assert.False(Assert.Single(settings, s => s.TypeCode?.Value == "M8").IsDisclaimerAcknowledged);
    }

    [Fact]
    public async Task Reads_a_live_notification_whose_read_flag_is_a_number_and_whose_HT_is_absent()
    {
        using var harness = new ClientHarness();
        harness.RespondWithFixture("trading-notifications/get-all-fyis.live.json");
        var client = new NotificationsClient(harness.ApiClient);

        var notifications = await client.GetAllAsync(
            2, cancellationToken: TestContext.Current.CancellationToken);

        var first = notifications[0];

        // IBKR documents 'R' as the string "0"; the gateway sends the bare number 0.
        Assert.False(first.IsRead);

        // 'HT' is documented on every notification and sent on none of them.
        Assert.Null(first.HT);

        Assert.Equal(Instant.FromUnixTimeSeconds(1788866281), first.Date);
        Assert.Equal("2026090830712308", first.Id);
        Assert.Equal(NotificationTypeCode.PortfolioFyis, first.TypeCode);
    }

    [Fact]
    public async Task Refuses_an_empty_device_identifier()
    {
        using var harness = new ClientHarness();
        var client = new NotificationsClient(harness.ApiClient);

        await Assert.ThrowsAsync<ArgumentException>(
            () => client.DeleteDeviceAsync("  ", TestContext.Current.CancellationToken));
    }
}
