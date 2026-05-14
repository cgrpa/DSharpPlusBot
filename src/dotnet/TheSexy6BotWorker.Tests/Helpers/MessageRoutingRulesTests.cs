using TheSexy6BotWorker.Helpers;

namespace TheSexy6BotWorker.Tests.Helpers;

public class MessageRoutingRulesTests
{
    [Theory]
    [InlineData("/image a skyline", true)]
    [InlineData("/tools", true)]
    [InlineData("/", false)]
    [InlineData("/ image", false)]
    [InlineData("grok hello", false)]
    public void IsLikelyCommandMessage_ClassifiesSlashCommands(string content, bool expected)
    {
        Assert.Equal(expected, MessageRoutingRules.IsLikelyCommandMessage(content));
    }
}
