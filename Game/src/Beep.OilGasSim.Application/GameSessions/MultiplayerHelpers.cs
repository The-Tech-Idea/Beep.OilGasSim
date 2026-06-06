namespace Beep.OilGasSim.Application.GameSessions;

public static class JoinCodeGenerator
{
    private const string Chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static string Create()
    {
        Span<char> code = stackalloc char[6];
        for (var i = 0; i < code.Length; i++)
        {
            code[i] = Chars[Random.Shared.Next(Chars.Length)];
        }
        return new string(code);
    }
}

public static class CompanyColorPalette
{
    private static readonly string[] Colors =
    [
        "#2563eb", "#dc2626", "#16a34a", "#ca8a04", "#9333ea", "#0891b2"
    ];

    public static string ForIndex(int index) => Colors[index % Colors.Length];
}
