using System.Collections.Generic;
using Microsoft.Extensions.Options;

namespace LostFound.AI.Configuration
{
    internal sealed class KnowledgeGraphOptionsValidator : IValidateOptions<KnowledgeGraphOptions>
    {
        public ValidateOptionsResult Validate(string? name, KnowledgeGraphOptions options)
        {
            var failures = new List<string>();

            if (string.IsNullOrWhiteSpace(options.DatabasePath))
            {
                failures.Add("LostFound:AI:KnowledgeGraph:DatabasePath must not be empty.");
            }

            if (options.ConceptCacheMaxEntries <= 0)
            {
                failures.Add("LostFound:AI:KnowledgeGraph:ConceptCacheMaxEntries must be positive.");
            }

            return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
        }
    }
}
