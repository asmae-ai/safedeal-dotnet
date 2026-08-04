using System.Text.RegularExpressions;

namespace SafeDeal.Application.Common.Extensions;

public static partial class StringExtensions
{
    [GeneratedRegex("([a-z])([A-Z])")]
    private static partial Regex SnakeCaseRegex();

    public static string ToSnakeCase(this string value)
        => SnakeCaseRegex().Replace(value, "$1_$2").ToLower();
}