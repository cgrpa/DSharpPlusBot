using Xunit;
using TheSexy6BotWorker.Markdown;

namespace TheSexy6BotWorker.Tests.Markdown;

public class MarkdownBuilderTests
{
    [Theory]
    [InlineData(1, "# Test")]
    [InlineData(2, "## Test")]
    [InlineData(3, "### Test")]
    [InlineData(6, "###### Test")]
    public void Heading_WithVariousLevels_GeneratesCorrectMarkdown(int level, string expected)
    {
        // Arrange
        var builder = new MarkdownBuilder();

        // Act
        var result = builder.Heading("Test", level).ToString();

        // Assert
        Assert.Contains(expected, result);
    }

    [Fact]
    public void Heading_WithInvalidLevel_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var builder = new MarkdownBuilder();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.Heading("Test", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.Heading("Test", 7));
    }

    [Fact]
    public void Bold_GeneratesCorrectMarkdown()
    {
        // Arrange
        var builder = new MarkdownBuilder();

        // Act
        var result = builder.Bold("test").ToString();

        // Assert
        Assert.Equal("**test**", result);
    }

    [Fact]
    public void Italic_GeneratesCorrectMarkdown()
    {
        // Arrange
        var builder = new MarkdownBuilder();

        // Act
        var result = builder.Italic("test").ToString();

        // Assert
        Assert.Equal("*test*", result);
    }

    [Fact]
    public void Code_GeneratesCorrectMarkdown()
    {
        // Arrange
        var builder = new MarkdownBuilder();

        // Act
        var result = builder.Code("var x = 1;").ToString();

        // Assert
        Assert.Equal("`var x = 1;`", result);
    }

    [Fact]
    public void CodeBlock_WithLanguage_GeneratesCorrectMarkdown()
    {
        // Arrange
        var builder = new MarkdownBuilder();

        // Act
        var result = builder.CodeBlock("var x = 1;", "csharp").ToString();

        // Assert
        Assert.Contains("```csharp", result);
        Assert.Contains("var x = 1;", result);
        Assert.Contains("```", result);
    }

    [Fact]
    public void CodeBlock_WithoutLanguage_GeneratesCorrectMarkdown()
    {
        // Arrange
        var builder = new MarkdownBuilder();

        // Act
        var result = builder.CodeBlock("code").ToString();

        // Assert
        Assert.Contains("```", result);
        Assert.Contains("code", result);
    }

    [Fact]
    public void ListItem_GeneratesCorrectMarkdown()
    {
        // Arrange
        var builder = new MarkdownBuilder();

        // Act
        var result = builder.ListItem("Item 1").ToString();

        // Assert
        Assert.Contains("- Item 1", result);
    }

    [Fact]
    public void ListItem_WithIndent_GeneratesCorrectMarkdown()
    {
        // Arrange
        var builder = new MarkdownBuilder();

        // Act
        var result = builder.ListItem("Nested", 1).ToString();

        // Assert
        Assert.Contains("  - Nested", result);
    }

    [Fact]
    public void BulletPoint_WithBoldLabel_GeneratesCorrectMarkdown()
    {
        // Arrange
        var builder = new MarkdownBuilder();

        // Act
        var result = builder.BulletPoint("Label", "Value", boldLabel: true).ToString();

        // Assert
        Assert.Contains("- **Label**: Value", result);
    }

    [Fact]
    public void BulletPoint_WithoutBoldLabel_GeneratesCorrectMarkdown()
    {
        // Arrange
        var builder = new MarkdownBuilder();

        // Act
        var result = builder.BulletPoint("Label", "Value", boldLabel: false).ToString();

        // Assert
        Assert.Contains("- Label: Value", result);
    }

    [Fact]
    public void BulletPoint_WithNullValue_SkipsOutput()
    {
        // Arrange
        var builder = new MarkdownBuilder();

        // Act
        var result = builder.BulletPoint("Label", null).ToString();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void Link_GeneratesCorrectMarkdown()
    {
        // Arrange
        var builder = new MarkdownBuilder();

        // Act
        var result = builder.Link("Google", "https://google.com").ToString();

        // Assert
        Assert.Equal("[Google](https://google.com)", result);
    }

    [Fact]
    public void Image_GeneratesCorrectMarkdown()
    {
        // Arrange
        var builder = new MarkdownBuilder();

        // Act
        var result = builder.Image("Alt text", "https://example.com/image.png").ToString();

        // Assert
        Assert.Contains("![Alt text](https://example.com/image.png)", result);
    }

    [Fact]
    public void Quote_GeneratesCorrectMarkdown()
    {
        // Arrange
        var builder = new MarkdownBuilder();

        // Act
        var result = builder.Quote("This is a quote").ToString();

        // Assert
        Assert.Contains("> This is a quote", result);
    }

    [Fact]
    public void Quote_WithMultipleLines_QuotesEachLine()
    {
        // Arrange
        var builder = new MarkdownBuilder();

        // Act
        var result = builder.Quote("Line 1\nLine 2").ToString();

        // Assert
        Assert.Contains("> Line 1", result);
        Assert.Contains("> Line 2", result);
    }

    [Fact]
    public void HorizontalRule_GeneratesCorrectMarkdown()
    {
        // Arrange
        var builder = new MarkdownBuilder();

        // Act
        var result = builder.HorizontalRule().ToString();

        // Assert
        Assert.Contains("---", result);
    }

    [Fact]
    public void FluentChaining_CombinesMultipleElements()
    {
        // Arrange & Act
        var result = new MarkdownBuilder()
            .Heading("Title", 1)
            .Text("Some text")
            .Bold("Bold text")
            .ToString();

        // Assert
        Assert.Contains("# Title", result);
        Assert.Contains("Some text", result);
        Assert.Contains("**Bold text**", result);
    }

    [Fact]
    public void Clear_RemovesAllContent()
    {
        // Arrange
        var builder = new MarkdownBuilder();
        builder.Text("Test");

        // Act
        builder.Clear();
        var result = builder.ToString();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void EmptyLine_AddsBlankLine()
    {
        // Arrange
        var builder = new MarkdownBuilder();

        // Act
        builder.Text("Line 1").EmptyLine().Text("Line 2");
        var result = builder.ToString();

        // Assert
        var lines = result.Split(Environment.NewLine, StringSplitOptions.None);
        Assert.Contains(string.Empty, lines);
    }
}
