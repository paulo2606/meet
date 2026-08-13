using System.Security.Cryptography;

namespace Meet.Api.Services;

public static class MeetingCodeGenerator
{
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public static string Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(10);
        var code = new char[10];
        for (var i = 0; i < bytes.Length; i++)
        {
            code[i] = Alphabet[bytes[i] % Alphabet.Length];
        }
        return new string(code);
    }
}
