using System.Collections.Generic;
using Microsoft.Extensions.Options;

namespace LostFound.AI.Configuration
{
    // Registered as IValidateOptions<LocalAiRuntimeOptions> - the standard
    // .NET Options pattern hook that runs automatically the first time
    // IOptions<LocalAiRuntimeOptions>.Value is accessed, failing loudly
    // (OptionsValidationException) rather than letting a misconfiguration
    // (e.g. a blank path from a bad appsettings edit) surface later as a
    // confusing file-not-found deep inside OnnxEmbeddingModel.
    internal sealed class LocalAiRuntimeOptionsValidator : IValidateOptions<LocalAiRuntimeOptions>
    {
        public ValidateOptionsResult Validate(string? name, LocalAiRuntimeOptions options)
        {
            var failures = new List<string>();

            if (string.IsNullOrWhiteSpace(options.ModelDirectory))
            {
                failures.Add("LostFound:AI:LocalRuntime:ModelDirectory must not be empty.");
            }

            if (string.IsNullOrWhiteSpace(options.DatabasePath))
            {
                failures.Add("LostFound:AI:LocalRuntime:DatabasePath must not be empty.");
            }

            if (string.IsNullOrWhiteSpace(options.ModelFileName) || string.IsNullOrWhiteSpace(options.TokenizerFileName))
            {
                failures.Add("LostFound:AI:LocalRuntime:ModelFileName and TokenizerFileName must not be empty.");
            }

            if (options.EmbeddingDimensions <= 0)
            {
                failures.Add("LostFound:AI:LocalRuntime:EmbeddingDimensions must be positive.");
            }

            if (options.MaxSequenceLength <= 0)
            {
                failures.Add("LostFound:AI:LocalRuntime:MaxSequenceLength must be positive.");
            }

            if (options.MemoryCacheMaxEntries <= 0)
            {
                failures.Add("LostFound:AI:LocalRuntime:MemoryCacheMaxEntries must be positive.");
            }

            return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
        }
    }
}
