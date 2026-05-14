using TheSexy6BotWorker.Models;

namespace TheSexy6BotWorker.Helpers;

public sealed record ImageCommandParseResult(
    bool Success,
    ImageGenerationRequest? Request,
    string? ErrorMessage,
    string Usage);

public static class ImageCommandParser
{
    public const string Usage = "/image [flux|seedream] <prompt>";

    public static ImageCommandParseResult Parse(string? firstToken, string? remainingText)
    {
        if (string.IsNullOrWhiteSpace(firstToken))
        {
            return Fail("A prompt is required.", Usage);
        }

        if (ImageGenerationModelChoiceExtensions.TryParseAlias(firstToken, out var requestedModel))
        {
            var prompt = NormalizePrompt(remainingText);
            if (string.IsNullOrWhiteSpace(prompt))
            {
                return Fail("A prompt is required after the model alias.", Usage);
            }

            return new ImageCommandParseResult(
                true,
                new ImageGenerationRequest
                {
                    Prompt = prompt,
                    RequestedModel = requestedModel
                },
                null,
                Usage);
        }

        var promptParts = new List<string> { firstToken.Trim() };
        if (!string.IsNullOrWhiteSpace(remainingText))
        {
            promptParts.Add(remainingText.Trim());
        }

        var promptText = string.Join(" ", promptParts).Trim();
        if (string.IsNullOrWhiteSpace(promptText))
        {
            return Fail("A prompt is required.", Usage);
        }

        return new ImageCommandParseResult(
            true,
            new ImageGenerationRequest
            {
                Prompt = promptText
            },
            null,
            Usage);
    }

    private static ImageCommandParseResult Fail(string message, string usage)
    {
        return new ImageCommandParseResult(false, null, message, usage);
    }

    private static string NormalizePrompt(string? prompt)
    {
        return string.IsNullOrWhiteSpace(prompt)
            ? string.Empty
            : prompt.Trim();
    }
}
