using IbkrDotNet.Trading.Configuration;
using Xunit;

namespace IbkrDotNet.Trading.Tests.Streaming;

public class StreamingAddressTests
{
    [Fact]
    public void Derives_the_gateway_socket_from_the_default_base_address()
    {
        var options = new IbkrTradingOptions();

        Assert.Equal(new Uri("wss://localhost:5000/v1/api/ws"), options.ResolveStreamingAddress());
    }

    [Fact]
    public void Keeps_a_custom_gateway_port()
    {
        var options = new IbkrTradingOptions { BaseAddress = new Uri("https://localhost:5050") };

        Assert.Equal(new Uri("wss://localhost:5050/v1/api/ws"), options.ResolveStreamingAddress());
    }

    [Fact]
    public void Leaves_the_default_port_implicit_for_production()
    {
        var options = new IbkrTradingOptions { Environment = IbkrEnvironment.Production };

        Assert.Equal("wss://api.ibkr.com/v1/api/ws", options.ResolveStreamingAddress().ToString());
    }

    [Fact]
    public void Downgrades_to_a_plain_socket_for_a_plain_http_base_address()
    {
        var options = new IbkrTradingOptions { BaseAddress = new Uri("http://localhost:5000") };

        Assert.Equal(new Uri("ws://localhost:5000/v1/api/ws"), options.ResolveStreamingAddress());
    }

    [Fact]
    public void An_explicit_socket_address_wins()
    {
        var options = new IbkrTradingOptions { Environment = IbkrEnvironment.Production };
        options.Streaming.Address = new Uri("wss://proxy.example/ibkr/ws");

        Assert.Equal(new Uri("wss://proxy.example/ibkr/ws"), options.ResolveStreamingAddress());
    }

    [Theory]
    [InlineData("KeepAliveInterval", 0)]
    [InlineData("ReconnectDelay", 0)]
    [InlineData("BufferCapacity", 0)]
    [InlineData("CloseTimeout", 0)]
    public void Validation_rejects_a_value_that_would_disable_the_transport(string property, int value)
    {
        var options = new IbkrStreamingOptions();
        switch (property)
        {
            case "KeepAliveInterval":
                options.KeepAliveInterval = NodaTime.Duration.FromSeconds(value);
                break;
            case "ReconnectDelay":
                options.ReconnectDelay = NodaTime.Duration.FromSeconds(value);
                break;
            case "BufferCapacity":
                options.BufferCapacity = value;
                break;
            default:
                options.CloseTimeout = NodaTime.Duration.FromSeconds(value);
                break;
        }

        var ex = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Contains(property, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validation_rejects_a_maximum_reconnect_delay_below_the_initial_one()
    {
        var options = new IbkrStreamingOptions
        {
            ReconnectDelay = NodaTime.Duration.FromSeconds(10),
            ReconnectMaxDelay = NodaTime.Duration.FromSeconds(5),
        };

        Assert.Throws<InvalidOperationException>(options.Validate);
    }
}
