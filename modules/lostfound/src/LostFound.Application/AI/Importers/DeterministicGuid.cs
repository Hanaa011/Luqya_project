using System;
using System.Security.Cryptography;
using System.Text;

namespace LostFound.AI.Importers
{
    // Stable Guid derived from a string via SHA-256 (first 16 bytes) - NOT
    // an RFC 4122-compliant "named UUID" (no version/variant bits set),
    // which doesn't matter here since these IDs are only ever compared for
    // equality within this application, never interpreted as a UUID
    // version. What matters is: the same input string always produces the
    // same Guid, which is what makes re-running an import idempotent
    // (UpsertAsync overwrites the same row instead of creating a duplicate)
    // and resumable after an interruption.
    internal static class DeterministicGuid
    {
        public static Guid From(string input)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return new Guid(hash[..16]);
        }
    }
}
