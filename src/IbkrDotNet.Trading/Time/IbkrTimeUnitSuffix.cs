using System.Globalization;

namespace IbkrDotNet.Trading.Time;

/// <summary>
/// Shared parsing and formatting of the <c>{count}{suffix}</c> encoding that IBKR uses for both bar
/// widths and history periods.
/// </summary>
internal static class IbkrTimeUnitSuffix
{
    public static string ToSuffix(IbkrTimeUnit unit) => unit switch
    {
        IbkrTimeUnit.Seconds => "S",
        IbkrTimeUnit.Minutes => "min",
        IbkrTimeUnit.Hours => "h",
        IbkrTimeUnit.Days => "d",
        IbkrTimeUnit.Weeks => "w",
        IbkrTimeUnit.Months => "m",
        IbkrTimeUnit.Years => "y",
        _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unknown time unit."),
    };

    public static bool TryParse(string? value, out int count, out IbkrTimeUnit unit)
    {
        count = 0;
        unit = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var span = value.AsSpan().Trim();
        var digits = 0;
        while (digits < span.Length && char.IsAsciiDigit(span[digits]))
        {
            digits++;
        }

        if (digits == 0 ||
            !int.TryParse(span[..digits], NumberStyles.None, CultureInfo.InvariantCulture, out count) ||
            count <= 0)
        {
            return false;
        }

        // 'S' (seconds) is the one case-sensitive suffix: 'm' means months, so treating 's' and 'S'
        // as interchangeable would make 'm' vs 'M' ambiguous too. IBKR documents 'S' uppercase.
        var suffix = span[digits..];
        if (suffix.Equals("S", StringComparison.Ordinal))
        {
            unit = IbkrTimeUnit.Seconds;
        }
        else if (suffix.Equals("min", StringComparison.OrdinalIgnoreCase))
        {
            unit = IbkrTimeUnit.Minutes;
        }
        else if (suffix.Equals("h", StringComparison.OrdinalIgnoreCase))
        {
            unit = IbkrTimeUnit.Hours;
        }
        else if (suffix.Equals("d", StringComparison.OrdinalIgnoreCase))
        {
            unit = IbkrTimeUnit.Days;
        }
        else if (suffix.Equals("w", StringComparison.OrdinalIgnoreCase))
        {
            unit = IbkrTimeUnit.Weeks;
        }
        else if (suffix.Equals("m", StringComparison.Ordinal))
        {
            unit = IbkrTimeUnit.Months;
        }
        else if (suffix.Equals("y", StringComparison.OrdinalIgnoreCase))
        {
            unit = IbkrTimeUnit.Years;
        }
        else
        {
            return false;
        }

        return true;
    }
}
