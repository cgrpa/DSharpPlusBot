using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using TheSexy6BotWorker.Configuration;
using TheSexy6BotWorker.Contracts;
using TheSexy6BotWorker.Models;
using TheSexy6BotWorker.Services;
using Xunit;

namespace TheSexy6BotWorker.Tests.Services;

public class ConversationSessionManagerTests
{
    private readonly ConversationSessionManager _manager;
    private readonly IBotConfiguration _grokBot;
    private readonly IBotConfiguration _geminiBot;

    public ConversationSessionManagerTests()
    {
        _manager = new ConversationSessionManager(NullLogger<ConversationSessionManager>.Instance);
        _grokBot = new GrokBotConfiguration();
        _geminiBot = new GeminiBotConfiguration();
    }

    [Fact]
    public void GetActiveSession_NoSession_ReturnsNull()
    {
        // Act
        var session = _manager.GetActiveSession(12345);

        // Assert
        Assert.Null(session);
    }

    [Fact]
    public void StartSession_CreatesNewSession()
    {
        // Act
        var session = _manager.StartSession(12345, _grokBot);

        // Assert
        Assert.NotNull(session);
        Assert.Equal(12345ul, session.ChannelId);
        Assert.Equal(_grokBot.ServiceId, session.Bot.ServiceId);
        Assert.True(session.IsActive);
    }

    [Fact]
    public void GetActiveSession_AfterStartSession_ReturnsSession()
    {
        // Arrange
        _manager.StartSession(12345, _grokBot);

        // Act
        var session = _manager.GetActiveSession(12345);

        // Assert
        Assert.NotNull(session);
        Assert.Equal(12345ul, session.ChannelId);
    }

    [Fact]
    public void StartSession_ReplacesExistingSession()
    {
        // Arrange
        var firstSession = _manager.StartSession(12345, _grokBot);
        
        // Act
        var secondSession = _manager.StartSession(12345, _geminiBot);

        // Assert
        Assert.NotEqual(firstSession, secondSession);
        Assert.Equal(_geminiBot.ServiceId, secondSession.Bot.ServiceId);
        
        var retrieved = _manager.GetActiveSession(12345);
        Assert.Equal(_geminiBot.ServiceId, retrieved?.Bot.ServiceId);
    }

    [Fact]
    public void GetOrCreateSession_NoExisting_CreatesNew()
    {
        // Act
        var session = _manager.GetOrCreateSession(12345, _grokBot);

        // Assert
        Assert.NotNull(session);
        Assert.Equal(_grokBot.ServiceId, session.Bot.ServiceId);
    }

    [Fact]
    public void GetOrCreateSession_ExistingSameBot_ReturnsSame()
    {
        // Arrange
        var firstSession = _manager.StartSession(12345, _grokBot);

        // Act
        var secondSession = _manager.GetOrCreateSession(12345, _grokBot);

        // Assert
        Assert.Same(firstSession, secondSession);
    }

    [Fact]
    public void GetOrCreateSession_ExistingDifferentBot_CreatesNew()
    {
        // Arrange
        _manager.StartSession(12345, _grokBot);

        // Act
        var session = _manager.GetOrCreateSession(12345, _geminiBot);

        // Assert
        Assert.Equal(_geminiBot.ServiceId, session.Bot.ServiceId);
    }

    [Fact]
    public void AddMessage_RecordsMessageInSession()
    {
        // Arrange
        var session = _manager.StartSession(12345, _grokBot);
        var message = new ChatMessageContent(AuthorRole.User, "Test message");

        // Act
        _manager.AddMessage(12345, message);

        // Assert
        Assert.Equal(1, session.MessageCount);
    }

    [Fact]
    public void AddMessage_NoSession_DoesNotThrow()
    {
        // Arrange
        var message = new ChatMessageContent(AuthorRole.User, "Test message");

        // Act & Assert - should not throw
        _manager.AddMessage(99999, message);
    }

    [Fact]
    public void EndSession_RemovesSession()
    {
        // Arrange
        _manager.StartSession(12345, _grokBot);

        // Act
        _manager.EndSession(12345);

        // Assert
        Assert.Null(_manager.GetActiveSession(12345));
    }

    [Fact]
    public void EndSession_NoSession_DoesNotThrow()
    {
        // Act & Assert - should not throw
        _manager.EndSession(99999);
    }

    [Fact]
    public void HasActiveSession_WithSession_ReturnsTrue()
    {
        // Arrange
        _manager.StartSession(12345, _grokBot);

        // Act
        var result = _manager.HasActiveSession(12345);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasActiveSession_WithoutSession_ReturnsFalse()
    {
        // Act
        var result = _manager.HasActiveSession(12345);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void CleanupExpiredSessions_RemovesExpiredOnly()
    {
        // Arrange - Create sessions for multiple channels
        _manager.StartSession(11111, _grokBot);
        _manager.StartSession(22222, _grokBot);
        
        // Both are active since they were just created
        Assert.True(_manager.HasActiveSession(11111));
        Assert.True(_manager.HasActiveSession(22222));

        // Act
        _manager.CleanupExpiredSessions();

        // Assert - Sessions still active (not expired)
        Assert.True(_manager.HasActiveSession(11111));
        Assert.True(_manager.HasActiveSession(22222));
    }

    [Fact]
    public void MultipleSessions_IndependentChannels()
    {
        // Arrange & Act
        var session1 = _manager.StartSession(11111, _grokBot);
        var session2 = _manager.StartSession(22222, _geminiBot);

        // Assert
        Assert.NotEqual(session1, session2);
        Assert.Equal(_grokBot.ServiceId, _manager.GetActiveSession(11111)?.Bot.ServiceId);
        Assert.Equal(_geminiBot.ServiceId, _manager.GetActiveSession(22222)?.Bot.ServiceId);
    }
}

public class ConversationSessionTests
{
    private readonly IBotConfiguration _grokBot;

    public ConversationSessionTests()
    {
        _grokBot = new GrokBotConfiguration();
    }

    [Fact]
    public void Constructor_InitializesCorrectly()
    {
        // Act
        var session = new ConversationSession(12345, _grokBot);

        // Assert
        Assert.Equal(12345ul, session.ChannelId);
        Assert.Equal(_grokBot.ServiceId, session.Bot.ServiceId);
        Assert.Equal(0, session.MessageCount);
        Assert.True(session.IsActive);
        Assert.NotNull(session.History);
    }

    [Fact]
    public void RecordMessage_IncrementsMessageCount()
    {
        // Arrange
        var session = new ConversationSession(12345, _grokBot);
        var message = new ChatMessageContent(AuthorRole.User, "Hello");

        // Act
        session.RecordMessage(message);

        // Assert
        Assert.Equal(1, session.MessageCount);
        Assert.Single(session.History);
    }

    [Fact]
    public void RecordMessage_EstimatesTokens()
    {
        // Arrange
        var session = new ConversationSession(12345, _grokBot);
        var message = new ChatMessageContent(AuthorRole.User, "This is a test message with multiple words");

        // Act
        session.RecordMessage(message);

        // Assert
        Assert.True(session.EstimatedTokens > 0);
    }

    [Fact]
    public void RecordMessage_UpdatesLastActivity()
    {
        // Arrange
        var session = new ConversationSession(12345, _grokBot);
        var initialActivity = session.LastActivity;

        // Wait a tiny bit to ensure time difference
        Thread.Sleep(10);

        // Act
        session.RecordMessage(new ChatMessageContent(AuthorRole.User, "Test"));

        // Assert
        Assert.True(session.LastActivity > initialActivity);
    }

    [Fact]
    public void IsActive_NewSession_ReturnsTrue()
    {
        // Arrange
        var session = new ConversationSession(12345, _grokBot);

        // Assert
        Assert.True(session.IsActive);
    }

    [Fact]
    public void Touch_UpdatesLastActivity()
    {
        // Arrange
        var session = new ConversationSession(12345, _grokBot);
        var initialActivity = session.LastActivity;

        Thread.Sleep(10);

        // Act
        session.Touch();

        // Assert
        Assert.True(session.LastActivity > initialActivity);
    }

    [Fact]
    public void IsHighActivity_BelowThreshold_ReturnsFalse()
    {
        // Arrange
        var session = new ConversationSession(12345, _grokBot);
        
        // Add fewer messages than threshold
        for (int i = 0; i < _grokBot.HighActivityThreshold - 1; i++)
        {
            session.RecordMessage(new ChatMessageContent(AuthorRole.User, $"Message {i}"));
        }

        // Assert
        Assert.False(session.IsHighActivity);
    }

    [Fact]
    public void IsHighActivity_AtThreshold_ReturnsTrue()
    {
        // Arrange
        var session = new ConversationSession(12345, _grokBot);
        
        // Add exactly threshold messages
        for (int i = 0; i < _grokBot.HighActivityThreshold; i++)
        {
            session.RecordMessage(new ChatMessageContent(AuthorRole.User, $"Message {i}"));
        }

        // Assert
        Assert.True(session.IsHighActivity);
    }

    [Fact]
    public void GetReplyDelay_NotHighActivity_ReturnsNull()
    {
        // Arrange
        var session = new ConversationSession(12345, _grokBot);

        // Act
        var delay = session.GetReplyDelay();

        // Assert
        Assert.Null(delay);
    }

    [Fact]
    public void GetReplyDelay_HighActivity_ReturnsDelay()
    {
        // Arrange
        var session = new ConversationSession(12345, _grokBot);
        
        // Trigger high activity
        for (int i = 0; i < _grokBot.HighActivityThreshold; i++)
        {
            session.RecordMessage(new ChatMessageContent(AuthorRole.User, $"Message {i}"));
        }

        // Act
        var delay = session.GetReplyDelay();

        // Assert
        Assert.NotNull(delay);
        Assert.True(delay >= _grokBot.HighActivityDelayMin);
        Assert.True(delay <= _grokBot.HighActivityDelayMax);
    }

    [Fact]
    public void Duration_ReflectsTimeElapsed()
    {
        // Arrange
        var session = new ConversationSession(12345, _grokBot);

        // Act
        Thread.Sleep(50);
        var duration = session.Duration;

        // Assert
        Assert.True(duration.TotalMilliseconds >= 50);
    }
}
