#!/usr/bin/env python3
"""Generates a PBKDF2 hash triplet (salt:iterations:hash) for a bearer token.

Usage: gen-hash-token.py [iterations] [token]
  iterations  Number of PBKDF2 iterations (default: 600000)
  token       Plain-text token to hash. If omitted, a random 32-byte token is generated.
"""

import base64
import hashlib
import os
import sys


def main() -> int:
    iterations = 600000
    if len(sys.argv) > 1:
        try:
            iterations = int(sys.argv[1])
            if iterations <= 0:
                raise ValueError
        except ValueError:
            print("Error: iterations must be a positive integer.", file=sys.stderr)
            return 1

    if len(sys.argv) > 2:
        token = sys.argv[2]
    else:
        token = base64.b64encode(os.urandom(32)).decode("ascii")

    salt = os.urandom(16)
    hash_bytes = hashlib.pbkdf2_hmac("sha256", token.encode("utf-8"), salt, iterations, dklen=32)

    salt_b64 = base64.b64encode(salt).decode("ascii")
    hash_b64 = base64.b64encode(hash_bytes).decode("ascii")

    print(f"Token:   {token}")
    print(f"Triplet: {salt_b64}:{iterations}:{hash_b64}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
