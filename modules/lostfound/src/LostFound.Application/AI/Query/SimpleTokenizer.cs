using System;
using System.Collections.Generic;

namespace LostFound.AI.Query
{
    internal sealed class SimpleTokenizer : ITokenizer
    {
        public IReadOnlyList<string> Tokenize(string text) =>
            string.IsNullOrWhiteSpace(text)
                ? Array.Empty<string>()
                : text.ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
    }
}
