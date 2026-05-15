using TheSexy6BotWorker.Configuration;

namespace TheSexy6BotWorker.Models;

public enum ImageGenerationModelChoice
{
    Flux = 0,
    Seedream = 1,
}

public static class ImageGenerationModelChoiceExtensions
{
    public static string ToAlias(this ImageGenerationModelChoice choice)
    {
        return choice switch
        {
            ImageGenerationModelChoice.Flux => "flux",
            ImageGenerationModelChoice.Seedream => "seedream",
            _ => throw new ArgumentOutOfRangeException(nameof(choice), choice, "Unknown image model choice."),
        };
    }

    public static string ToModelId(this ImageGenerationModelChoice choice, ImageGenerationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return choice switch
        {
            ImageGenerationModelChoice.Flux => options.DefaultModelId,
            ImageGenerationModelChoice.Seedream => options.FallbackModelId,
            _ => throw new ArgumentOutOfRangeException(nameof(choice), choice, "Unknown image model choice."),
        };
    }

    public static bool TryParseAlias(string? value, out ImageGenerationModelChoice choice)
    {
        choice = ImageGenerationModelChoice.Flux;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "flux" => TryAssign(ImageGenerationModelChoice.Flux, out choice),
            "seedream" => TryAssign(ImageGenerationModelChoice.Seedream, out choice),
            _ => false,
        };
    }

    private static bool TryAssign(ImageGenerationModelChoice assigned, out ImageGenerationModelChoice choice)
    {
        choice = assigned;
        return true;
    }
}
