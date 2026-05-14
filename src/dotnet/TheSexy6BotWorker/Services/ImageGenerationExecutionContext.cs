namespace TheSexy6BotWorker.Services;

public sealed record ImageGenerationExecutionContext(
    ulong SourceMessageId,
    ulong ChannelId,
    ulong UserId,
    bool IsAuto);
