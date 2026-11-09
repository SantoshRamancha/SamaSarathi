using System.Text.RegularExpressions;

namespace Samasarathi.Domain;

public static partial class PhoneNumber
{
    /// <summary>
    /// Accepts Indian mobile numbers typed any common way ("98765 43210", "098765-43210", "+91 9876543210")
    /// and returns one canonical form, "+919876543210", so the same person always maps to the same account.
    /// </summary>
    public static bool TryNormalize(string? input, out string normalized)
    {
        normalized = "";
        if (string.IsNullOrWhiteSpace(input) || input.Any(char.IsLetter)) return false;

        var digits = new string(input.Where(char.IsAsciiDigit).ToArray());
        if (digits.Length == 12 && digits.StartsWith("91")) digits = digits[2..];
        else if (digits.Length == 11 && digits.StartsWith('0')) digits = digits[1..];

        if (!IndianMobile().IsMatch(digits)) return false;
        normalized = "+91" + digits;
        return true;
    }

    [GeneratedRegex("^[6-9][0-9]{9}$")]
    private static partial Regex IndianMobile();
}

public static partial class VehicleNumber
{
    /// <summary>"ka 01 ab-1234" → "KA01AB1234". Also accepts Bharat series ("22 BH 1234 AA").</summary>
    public static bool TryNormalize(string? input, out string normalized)
    {
        normalized = new string((input ?? "").Where(char.IsAsciiLetterOrDigit).ToArray()).ToUpperInvariant();
        return StateSeries().IsMatch(normalized) || BharatSeries().IsMatch(normalized);
    }

    [GeneratedRegex("^[A-Z]{2}[0-9]{1,2}[A-Z]{0,3}[0-9]{1,4}$")]
    private static partial Regex StateSeries();

    [GeneratedRegex("^[0-9]{2}BH[0-9]{4}[A-Z]{1,2}$")]
    private static partial Regex BharatSeries();
}
