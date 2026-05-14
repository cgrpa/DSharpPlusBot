using System.ComponentModel;
using Microsoft.SemanticKernel;
using TheSexy6BotWorker.Models;

namespace TheSexy6BotWorker.Services;

public sealed class ImageGenerationPlugin
{
    private readonly ImageGenerationService _service;

    public ImageGenerationPlugin(ImageGenerationService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    [KernelFunction("generate_image")]
    [Description("Generates an image from a text prompt, stores it, and returns a strict JSON marker for the conversation history.")]
    public async Task<string> GenerateImageAsync(
        [Description("The text prompt to render as an image.")] string prompt,
        [Description("Optional model alias: flux or seedream.")] string? model = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new InvalidOperationException("Image prompt is required.");
        }

        ImageGenerationModelChoice? requestedModel = null;
        if (!string.IsNullOrWhiteSpace(model))
        {
            if (!ImageGenerationModelChoiceExtensions.TryParseAlias(model, out var parsedModel))
            {
                throw new InvalidOperationException($"Unsupported image model alias '{model}'.");
            }

            requestedModel = parsedModel;
        }

        var result = await _service.GenerateAsync(
            new ImageGenerationRequest
            {
                Prompt = prompt,
                RequestedModel = requestedModel
            },
            cancellationToken).ConfigureAwait(false);

        if (!result.Success)
        {
            throw new InvalidOperationException(result.ResponseText);
        }

        return result.HistoryMarker?.ToJson() ?? result.ResponseText;
    }
}
