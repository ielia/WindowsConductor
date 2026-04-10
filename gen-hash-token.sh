#!/usr/bin/env bash
# Generates a PBKDF2 hash triplet (salt:iterations:hash) for a bearer token.
#
# Usage: ./gen-hash-token.sh [iterations] [token]
#   iterations  Number of PBKDF2 iterations (default: 600000)
#   token       Plain-text token to hash. If omitted, a random 32-byte token is generated.
#
# Requires OpenSSL 3.0+ (for the `kdf PBKDF2` subcommand).

set -euo pipefail

iterations="${1:-600000}"

if ! [[ "$iterations" =~ ^[1-9][0-9]*$ ]]; then
    echo "Error: iterations must be a positive integer." >&2
    exit 1
fi

# Use the provided token or generate a 32-byte random base64-encoded one.
if [[ $# -ge 2 ]]; then
    token="$2"
else
    token=$(openssl rand -base64 32 | tr -d '\r\n')
fi

# Generate 16 random salt bytes (binary) and their base64 form.
salt_hex=$(openssl rand -hex 16 | tr -d '\r\n')
salt_b64=$(printf '%s' "$salt_hex" | xxd -r -p | openssl base64 -A | tr -d '\r\n')

# Run PBKDF2-HMAC-SHA256 to derive a 32-byte key from the token using the salt.
# `openssl kdf` writes the derived key as hex; convert to base64.
hash_hex=$(openssl kdf \
    -keylen 32 \
    -kdfopt digest:SHA256 \
    -kdfopt "pass:$token" \
    -kdfopt "hexsalt:$salt_hex" \
    -kdfopt "iter:$iterations" \
    PBKDF2 | tr -d ':\r\n' | tr '[:upper:]' '[:lower:]')

hash_b64=$(printf '%s' "$hash_hex" | xxd -r -p | openssl base64 -A | tr -d '\r\n')

echo "Token:   $token"
echo "Triplet: ${salt_b64}:${iterations}:${hash_b64}"
