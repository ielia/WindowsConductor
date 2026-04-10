using System.Security.Cryptography;
using System.Text;

namespace WindowsConductor.DriverFlaUI;

public sealed class AuthTokenValidator
{
    private readonly string? _plainToken;
    private readonly byte[]? _hashSalt;
    private readonly int _hashIterations;
    private readonly byte[]? _hashExpected;

    private AuthTokenValidator(string? plainToken, byte[]? hashSalt, int hashIterations, byte[]? hashExpected)
    {
        _plainToken = plainToken;
        _hashSalt = hashSalt;
        _hashIterations = hashIterations;
        _hashExpected = hashExpected;
    }

    public bool RequiresAuth => _plainToken is not null || _hashExpected is not null;

    public static AuthTokenValidator None() => new(null, null, 0, null);

    public static AuthTokenValidator FromPlainToken(string token) => new(token, null, 0, null);

    public static AuthTokenValidator FromHashTriplet(string triplet)
    {
        var parts = triplet.Split(':');
        if (parts.Length != 3)
            throw new ArgumentException("Hash triplet must be in the format salt:iterations:hash (all base64, iterations as plain integer).");

        var salt = Convert.FromBase64String(parts[0]);
        if (!int.TryParse(parts[1], out var iterations) || iterations <= 0)
            throw new ArgumentException("Iterations must be a positive integer.");
        var hash = Convert.FromBase64String(parts[2]);

        return new AuthTokenValidator(null, salt, iterations, hash);
    }

    public bool Validate(string? bearerToken)
    {
        if (!RequiresAuth)
            return true;

        if (string.IsNullOrEmpty(bearerToken))
            return false;

        if (_plainToken is not null)
            return _plainToken == bearerToken;

        var actual = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(bearerToken),
            _hashSalt!,
            _hashIterations,
            HashAlgorithmName.SHA256,
            _hashExpected!.Length);

        return CryptographicOperations.FixedTimeEquals(actual, _hashExpected);
    }
}
