# Generates a PBKDF2 hash triplet (salt:iterations:hash) for a bearer token.
# Prints two lines: the plain token, then the triplet.
#
# Usage: .\gen-hash-token.ps1 [iterations] [token]
#   iterations  Number of PBKDF2 iterations (default: 600000)
#   token       Plain-text token to hash. If omitted, a random 32-byte token is generated.

param(
    [int]$Iterations = 600000,
    [string]$Token
)

$ErrorActionPreference = 'Stop'

if ($Iterations -le 0) {
    Write-Error "Iterations must be a positive integer."
    exit 1
}

$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
try {
    if ([string]::IsNullOrEmpty($Token)) {
        $tokenBytes = New-Object byte[] 32
        $rng.GetBytes($tokenBytes)
        $Token = [Convert]::ToBase64String($tokenBytes)
    }

    $salt = New-Object byte[] 16
    $rng.GetBytes($salt)
}
finally {
    $rng.Dispose()
}

$pbkdf2 = New-Object System.Security.Cryptography.Rfc2898DeriveBytes(
    $Token, $salt, $Iterations, [System.Security.Cryptography.HashAlgorithmName]::SHA256)
try {
    $hash = $pbkdf2.GetBytes(32)
}
finally {
    $pbkdf2.Dispose()
}

$saltB64 = [Convert]::ToBase64String($salt)
$hashB64 = [Convert]::ToBase64String($hash)

Write-Output "Token:   $Token"
Write-Output "Triplet: ${saltB64}:${Iterations}:${hashB64}"
