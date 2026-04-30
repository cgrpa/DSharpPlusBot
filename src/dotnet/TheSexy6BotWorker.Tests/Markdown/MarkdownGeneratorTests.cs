using System.ComponentModel;
using Xunit;
using TheSexy6BotWorker.Markdown;

namespace TheSexy6BotWorker.Tests.Markdown;

public class ObjectMarkdownBuilderTests
{
    private class TestModel
    {
        [MarkdownProperty(Icon = "📝")]
        public string Name { get; set; } = "Test";
        
        [MarkdownProperty(Icon = "🔢")]
        public int Age { get; set; } = 25;
        
        [MarkdownProperty(Label = "Is Active", Icon = "✅")]
        public bool Active { get; set; } = true;
        
        [MarkdownIgnore]
        public string IgnoredProperty { get; set; } = "Should not appear";
    }

    [Fact]
    public void FluentBuilder_CreatesSectionsAndProperties()
    {
        // Arrange
        var model = new TestModel();

        // Act
        var result = new ObjectMarkdownBuilder<TestModel>(model)
            .Section("Basic Info", section => section
                .Property(m => m.Name, icon: "📝")
                .Property(m => m.Age, icon: "🔢"))
            .Section("Settings", section => section
                .Property(m => m.Active, label: "Is Active", icon: "✅"))
            .Build();

        // Assert
        Assert.Contains("## Basic Info", result);
        Assert.Contains("## Settings", result);
        Assert.Contains("📝 Name", result);
        Assert.Contains("Test", result);
        Assert.Contains("🔢 Age", result);
        Assert.Contains("25", result);
        Assert.Contains("✅ Is Active", result);
        Assert.Contains("True", result);
    }

    [Fact]
    public void FluentBuilder_Property_UsesPropertyNameWhenNoLabelProvided()
    {
        // Arrange
        var model = new TestModel();

        // Act
        var result = new ObjectMarkdownBuilder<TestModel>(model)
            .Property(m => m.Name)
            .Build();

        // Assert - BulletPoint format is "- **Label**: Value"
        Assert.Contains("**Name**: Test", result);
    }

    [Fact]
    public void FluentBuilder_SelectiveInclusion_OnlyIncludesSelectedProperties()
    {
        // Arrange
        var model = new TestModel();

        // Act
        var result = new ObjectMarkdownBuilder<TestModel>(model)
            .Section("Selected", section => section
                .Property(m => m.Name))
            .Build();

        // Assert
        Assert.Contains("Name", result);
        Assert.DoesNotContain("Age", result);
        Assert.DoesNotContain("Active", result);
        Assert.DoesNotContain("IgnoredProperty", result);
    }

    [Fact]
    public void FluentBuilder_PropertyWithAttributes_UsesAttributeValues()
    {
        // Arrange
        var model = new TestModel();

        // Act
        var result = new ObjectMarkdownBuilder<TestModel>(model)
            .PropertyWithAttributes(m => m.Name)
            .PropertyWithAttributes(m => m.Active)
            .Build();

        // Assert
        Assert.Contains("📝 Name", result);
        Assert.Contains("✅ Is Active", result);
    }

    [Fact]
    public void FluentBuilder_OrdersPropertiesAsAdded()
    {
        // Arrange
        var model = new TestModel();

        // Act
        var result = new ObjectMarkdownBuilder<TestModel>(model)
            .Property(m => m.Age)
            .Property(m => m.Name)
            .Build();

        // Assert
        var ageIndex = result.IndexOf("Age");
        var nameIndex = result.IndexOf("Name");
        Assert.True(ageIndex < nameIndex, "Age should appear before Name (order as added)");
    }

    [Fact]
    public void FluentBuilder_OrdersSectionsAsAdded()
    {
        // Arrange
        var model = new TestModel();

        // Act
        var result = new ObjectMarkdownBuilder<TestModel>(model)
            .Section("Settings")
            .Property(m => m.Active)
            .Section("Basic Info")
            .Property(m => m.Name)
            .Build();

        // Assert
        var settingsIndex = result.IndexOf("## Settings");
        var basicInfoIndex = result.IndexOf("## Basic Info");
        Assert.True(settingsIndex < basicInfoIndex, "Settings should appear before Basic Info (order as added)");
    }

    private class FormattedModel
    {
        [MarkdownProperty(Format = "F2")]
        public double Value { get; set; } = 3.14159;
    }

    [Fact]
    public void FluentBuilder_AppliesFormatString()
    {
        // Arrange
        var model = new FormattedModel();

        // Act
        var result = new ObjectMarkdownBuilder<FormattedModel>(model)
            .Property(m => m.Value, format: "F2")
            .Build();

        // Assert
        Assert.Contains("3.14", result);
        Assert.DoesNotContain("3.14159", result);
    }

    [Fact]
    public void FluentBuilder_UsesCustomLabel()
    {
        // Arrange
        var model = new TestModel();

        // Act
        var result = new ObjectMarkdownBuilder<TestModel>(model)
            .Property(m => m.Name, label: "Custom Label")
            .Build();

        // Assert
        Assert.Contains("Custom Label", result);
    }

    [Fact]
    public void FluentBuilder_CustomAccessToBuilder()
    {
        // Arrange
        var model = new TestModel();

        // Act
        var result = new ObjectMarkdownBuilder<TestModel>(model)
            .Custom(builder =>
            {
                builder.Heading("Custom Heading", 1);
                builder.BulletPoint("Custom Bullet", "Custom Value");
            })
            .Build();

        // Assert
        Assert.Contains("# Custom Heading", result);
        Assert.Contains("Custom Bullet", result);
    }

    private class CollectionModel
    {
        public List<string> Items { get; set; } = ["A", "B", "C"];
        public Dictionary<string, object> Dict { get; set; } = new() { ["key"] = "value" };
    }

    [Fact]
    public void FluentBuilder_FormatsCollections_AsCount()
    {
        // Arrange
        var model = new CollectionModel();

        // Act
        var result = new ObjectMarkdownBuilder<CollectionModel>(model)
            .Property(m => m.Items)
            .Property(m => m.Dict)
            .Build();

        // Assert
        Assert.Contains("Collection (3 items)", result);
        Assert.Contains("Dictionary (1 items)", result);
    }

    [Fact]
    public void FluentBuilder_BulletPoint_AddsCustomBullet()
    {
        // Arrange
        var model = new TestModel();

        // Act
        var result = new ObjectMarkdownBuilder<TestModel>(model)
            .BulletPoint("Custom", "Value", icon: "🎯")
            .Build();

        // Assert
        Assert.Contains("🎯 Custom", result);
        Assert.Contains("Value", result);
    }

    [Fact]
    public void FluentBuilder_Text_AddsRawText()
    {
        // Arrange
        var model = new TestModel();

        // Act
        var result = new ObjectMarkdownBuilder<TestModel>(model)
            .Text("Some raw text")
            .Build();

        // Assert
        Assert.Contains("Some raw text", result);
    }

    [Fact]
    public void FluentBuilder_EmptyLine_AddsNewLine()
    {
        // Arrange
        var model = new TestModel();

        // Act
        var result = new ObjectMarkdownBuilder<TestModel>(model)
            .Text("Line 1")
            .EmptyLine()
            .Text("Line 2")
            .Build();

        // Assert - Check that both lines exist with an empty line between (using Environment.NewLine)
        Assert.Contains("Line 1", result);
        Assert.Contains("Line 2", result);
        Assert.True(result.IndexOf("Line 2") > result.IndexOf("Line 1") + "Line 1".Length);
    }

    [Fact]
    public void PropertySectionBuilder_PropertyIf_AddsConditionally()
    {
        // Arrange
        var model = new TestModel();

        // Act
        var result = new ObjectMarkdownBuilder<TestModel>(model)
            .Section("Conditional", section => section
                .PropertyIf(true, m => m.Name)
                .PropertyIf(false, m => m.Age))
            .Build();

        // Assert
        Assert.Contains("Name", result);
        Assert.DoesNotContain("Age", result);
    }

    [Fact]
    public void FluentBuilder_ReflectObject_AddsAllPublicProperties()
    {
        // Arrange
        var settings = new { Temperature = 0.7, MaxTokens = 1000 };

        // Act
        var result = new ObjectMarkdownBuilder<TestModel>(new TestModel())
            .Section("Settings")
            .ReflectObject(settings)
            .Build();

        // Assert
        Assert.Contains("Temperature", result);
        Assert.Contains("MaxTokens", result);
    }

    [Fact]
    public void FluentBuilder_ToString_ReturnsBuild()
    {
        // Arrange
        var model = new TestModel();
        var builder = new ObjectMarkdownBuilder<TestModel>(model)
            .Property(m => m.Name);

        // Act & Assert
        Assert.Equal(builder.Build(), builder.ToString());
    }
}
