namespace TheSexy6BotWorker.Helpers;

public static class MessageRoutingRules
{
    public static bool IsLikelyCommandMessage(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        return content.Length > 1
               && content[0] == '/'
               && char.IsLetterOrDigit(content[1]);
    }
}
