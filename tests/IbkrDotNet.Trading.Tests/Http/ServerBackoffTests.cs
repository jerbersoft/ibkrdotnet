using IbkrDotNet.Trading.Http;
using IbkrDotNet.Trading.Http.RateLimiting;
using IbkrDotNet.Trading.Tests.TestSupport;
using NodaTime;
using Xunit;

namespace IbkrDotNet.Trading.Tests.Http;

/// <summary>
/// The rules the hold itself follows, exercised directly because the interesting ones -- two
/// rejections racing, and holds accumulating over the life of a process -- are awkward to stage
/// through a client.
/// </summary>
public class ServerBackoffTests
{
    private const string Key = "GET /v1/api/probe/thing";

    [Fact]
    public async Task Lets_an_unrecorded_key_straight_through()
    {
        var scheduler = new FakeDelayScheduler();
        var backoff = new ServerBackoff(scheduler);

        await backoff.WaitAsync(Key, Duration.FromSeconds(30), TestContext.Current.CancellationToken);

        Assert.Equal(Duration.Zero, scheduler.TotalDelay);
    }

    [Fact]
    public async Task Waits_out_a_recorded_hold()
    {
        var scheduler = new FakeDelayScheduler();
        var backoff = new ServerBackoff(scheduler);

        backoff.Record(Key, Duration.FromSeconds(10));
        await backoff.WaitAsync(Key, Duration.FromSeconds(30), TestContext.Current.CancellationToken);

        Assert.Equal(Duration.FromSeconds(10), scheduler.TotalDelay);
    }

    [Fact]
    public void Extends_a_hold_but_never_shortens_one()
    {
        var scheduler = new FakeDelayScheduler();
        var backoff = new ServerBackoff(scheduler);

        Assert.Equal(Duration.FromSeconds(30), backoff.Record(Key, Duration.FromSeconds(30)));

        // Two requests can be in flight against the same limit at once. The milder rejection must
        // not release the hold the sterner one put in place.
        Assert.Equal(Duration.FromSeconds(30), backoff.Record(Key, Duration.FromSeconds(5)));
        Assert.Equal(Duration.FromSeconds(45), backoff.Record(Key, Duration.FromSeconds(45)));
    }

    [Fact]
    public void Reports_the_remaining_hold_rather_than_the_window_just_recorded()
    {
        var scheduler = new FakeDelayScheduler();
        var backoff = new ServerBackoff(scheduler);

        backoff.Record(Key, Duration.FromSeconds(30));
        scheduler.Advance(Duration.FromSeconds(20));

        // What the caller is told to wait is what is left of the hold -- ten of the thirty seconds --
        // not the thirty originally asked for, and not the five this rejection named.
        Assert.Equal(Duration.FromSeconds(10), backoff.Record(Key, Duration.FromSeconds(5)));
    }

    [Fact]
    public async Task Waits_again_for_a_hold_extended_while_it_was_waiting()
    {
        var scheduler = new FakeDelayScheduler();
        var backoff = new ServerBackoff(scheduler);
        backoff.Record(Key, Duration.FromSeconds(10));

        // A second request against the same limit is rejected just as this one settles in to wait,
        // pushing the hold out to fifteen seconds. Returning after the original ten would send the
        // request into a limit that has been rejected more recently than the one it waited for.
        scheduler.OnDelay(about =>
        {
            if (about == Duration.FromSeconds(10))
            {
                backoff.Record(Key, Duration.FromSeconds(15));
            }
        });

        await backoff.WaitAsync(Key, Duration.FromMinutes(1), TestContext.Current.CancellationToken);

        Assert.Equal([Duration.FromSeconds(10), Duration.FromSeconds(5)], scheduler.Delays);
    }

    [Fact]
    public async Task Refuses_a_hold_longer_than_the_maximum_wait()
    {
        var scheduler = new FakeDelayScheduler();
        var backoff = new ServerBackoff(scheduler);
        backoff.Record(Key, Duration.FromMinutes(10));

        var ex = await Assert.ThrowsAsync<IbkrRateLimitExceededException>(
            () => backoff.WaitAsync(Key, Duration.FromSeconds(30), TestContext.Current.CancellationToken));

        Assert.Equal(Key, ex.Limit);
        Assert.Equal(Duration.FromMinutes(10), ex.RetryAfter);
        Assert.Equal(Duration.Zero, scheduler.TotalDelay);
    }

    [Fact]
    public async Task Forgets_a_hold_that_has_elapsed()
    {
        var scheduler = new FakeDelayScheduler();
        var backoff = new ServerBackoff(scheduler);

        backoff.Record(Key, Duration.FromSeconds(10));
        scheduler.Advance(Duration.FromSeconds(10));

        await backoff.WaitAsync(Key, Duration.FromSeconds(30), TestContext.Current.CancellationToken);

        Assert.Equal(Duration.Zero, scheduler.TotalDelay);
    }

    [Fact]
    public async Task Drops_elapsed_holds_so_they_do_not_accumulate_for_the_life_of_the_process()
    {
        var scheduler = new FakeDelayScheduler();
        var backoff = new ServerBackoff(scheduler);

        // Keys are paths when no published limit matches, so a client rejected across many accounts
        // or contracts would otherwise grow an entry per path and never release one.
        for (var i = 0; i < 100; i++)
        {
            backoff.Record($"GET /v1/api/probe/{i}", Duration.FromSeconds(1));
        }

        scheduler.Advance(Duration.FromSeconds(2));
        backoff.Record(Key, Duration.FromSeconds(1));

        Assert.Equal(1, backoff.Count);

        await backoff.WaitAsync("GET /v1/api/probe/0", Duration.FromSeconds(30), TestContext.Current.CancellationToken);
        Assert.Equal(Duration.Zero, scheduler.TotalDelay);
    }

    [Fact]
    public async Task Treats_a_negative_window_as_no_wait_at_all()
    {
        // Retry-After can arrive as an absolute date that has already passed.
        var scheduler = new FakeDelayScheduler();
        var backoff = new ServerBackoff(scheduler);

        Assert.Equal(Duration.Zero, backoff.Record(Key, Duration.FromSeconds(-30)));
        await backoff.WaitAsync(Key, Duration.FromSeconds(30), TestContext.Current.CancellationToken);

        Assert.Equal(Duration.Zero, scheduler.TotalDelay);
    }
}
