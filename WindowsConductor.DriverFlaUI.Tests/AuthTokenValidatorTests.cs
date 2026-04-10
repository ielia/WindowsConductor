using System.Security.Cryptography;
using System.Text;
using WindowsConductor.DriverFlaUI;

namespace WindowsConductor.DriverFlaUI.Tests;

[TestFixture]
[Category("Unit")]
public class AuthTokenValidatorTests
{
    // ── None ──────────────────────────────────────────────────────────────────

    [Test]
    public void None_DoesNotRequireAuth()
    {
        var validator = AuthTokenValidator.None();
        Assert.That(validator.RequiresAuth, Is.False);
    }

    [Test]
    public void None_AcceptsAnyToken()
    {
        var validator = AuthTokenValidator.None();
        Assert.That(validator.Validate(null), Is.True);
        Assert.That(validator.Validate(""), Is.True);
        Assert.That(validator.Validate("anything"), Is.True);
    }

    // ── Plain token ───────────────────────────────────────────────────────────

    [Test]
    public void PlainToken_RequiresAuth()
    {
        var validator = AuthTokenValidator.FromPlainToken("secret");
        Assert.That(validator.RequiresAuth, Is.True);
    }

    [Test]
    public void PlainToken_AcceptsMatchingToken()
    {
        var validator = AuthTokenValidator.FromPlainToken("secret");
        Assert.That(validator.Validate("secret"), Is.True);
    }

    [Test]
    public void PlainToken_RejectsWrongToken()
    {
        var validator = AuthTokenValidator.FromPlainToken("secret");
        Assert.That(validator.Validate("wrong"), Is.False);
    }

    [Test]
    public void PlainToken_RejectsNull()
    {
        var validator = AuthTokenValidator.FromPlainToken("secret");
        Assert.That(validator.Validate(null), Is.False);
    }

    [Test]
    public void PlainToken_RejectsEmpty()
    {
        var validator = AuthTokenValidator.FromPlainToken("secret");
        Assert.That(validator.Validate(""), Is.False);
    }

    // ── Hash triplet ──────────────────────────────────────────────────────────

    [Test]
    public void HashTriplet_RequiresAuth()
    {
        var triplet = GenerateTriplet("my-token", 1000);
        var validator = AuthTokenValidator.FromHashTriplet(triplet);
        Assert.That(validator.RequiresAuth, Is.True);
    }

    [Test]
    public void HashTriplet_AcceptsMatchingToken()
    {
        var triplet = GenerateTriplet("my-token", 1000);
        var validator = AuthTokenValidator.FromHashTriplet(triplet);
        Assert.That(validator.Validate("my-token"), Is.True);
    }

    [Test]
    public void HashTriplet_RejectsWrongToken()
    {
        var triplet = GenerateTriplet("my-token", 1000);
        var validator = AuthTokenValidator.FromHashTriplet(triplet);
        Assert.That(validator.Validate("wrong-token"), Is.False);
    }

    [Test]
    public void HashTriplet_RejectsNull()
    {
        var triplet = GenerateTriplet("my-token", 1000);
        var validator = AuthTokenValidator.FromHashTriplet(triplet);
        Assert.That(validator.Validate(null), Is.False);
    }

    [Test]
    public void HashTriplet_InvalidFormat_Throws()
    {
        Assert.Throws<ArgumentException>(() => AuthTokenValidator.FromHashTriplet("onlyonepart"));
    }

    [Test]
    public void HashTriplet_InvalidIterations_Throws()
    {
        var salt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        var hash = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        Assert.Throws<ArgumentException>(() => AuthTokenValidator.FromHashTriplet($"{salt}:notanumber:{hash}"));
    }

    [Test]
    public void HashTriplet_ZeroIterations_Throws()
    {
        var salt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        var hash = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        Assert.Throws<ArgumentException>(() => AuthTokenValidator.FromHashTriplet($"{salt}:0:{hash}"));
    }

    private static string GenerateTriplet(string token, int iterations)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(token), salt, iterations, HashAlgorithmName.SHA256, 32);
        return $"{Convert.ToBase64String(salt)}:{iterations}:{Convert.ToBase64String(hash)}";
    }
}
