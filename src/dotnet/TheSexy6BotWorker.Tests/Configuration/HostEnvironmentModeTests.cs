using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using TheSexy6BotWorker.Configuration;

namespace TheSexy6BotWorker.Tests.Configuration;

public class HostEnvironmentModeTests
{
    [Theory]
    [InlineData("Development")]
    [InlineData("development")]
    [InlineData("DEVELOPMENT")]
    public void GetRuntimeMode_ReturnsDevelopment_ForDevelopment(string environmentName)
    {
        var environment = new TestHostEnvironment(environmentName);

        var mode = HostEnvironmentMode.GetRuntimeMode(environment);

        Assert.Equal(RuntimeMode.Development, mode);
    }

    [Theory]
    [InlineData("Staging")]
    [InlineData("Production")]
    [InlineData("qa")]
    [InlineData("")]
    public void GetRuntimeMode_ReturnsRemote_ForNonDevelopment(string environmentName)
    {
        var environment = new TestHostEnvironment(environmentName);

        var mode = HostEnvironmentMode.GetRuntimeMode(environment);

        Assert.Equal(RuntimeMode.Remote, mode);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("development")]
    [InlineData("DEVELOPMENT")]
    public void ShouldLoadUserSecrets_ReturnsTrue_ForDevelopment(string environmentName)
    {
        var environment = new TestHostEnvironment(environmentName);

        var result = HostEnvironmentMode.ShouldLoadUserSecrets(environment);

        Assert.True(result);
    }

    [Theory]
    [InlineData("Staging")]
    [InlineData("Production")]
    [InlineData("qa")]
    public void ShouldLoadUserSecrets_ReturnsFalse_ForNonDevelopment(string environmentName)
    {
        var environment = new TestHostEnvironment(environmentName);

        var result = HostEnvironmentMode.ShouldLoadUserSecrets(environment);

        Assert.False(result);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("development")]
    [InlineData("DEVELOPMENT")]
    public void GetMessagePrefix_ReturnsTestPrefix_ForDevelopment(string environmentName)
    {
        var environment = new TestHostEnvironment(environmentName);

        var result = HostEnvironmentMode.GetMessagePrefix(environment);

        Assert.Equal(HostEnvironmentMode.DevelopmentCommandPrefix, result);
    }

    [Theory]
    [InlineData("Staging")]
    [InlineData("Production")]
    [InlineData("local")]
    public void GetMessagePrefix_ReturnsEmpty_ForNonDevelopment(string environmentName)
    {
        var environment = new TestHostEnvironment(environmentName);

        var result = HostEnvironmentMode.GetMessagePrefix(environment);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ShouldLoadUserSecrets_Throws_WhenEnvironmentIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => HostEnvironmentMode.ShouldLoadUserSecrets(null!));
    }

    [Fact]
    public void GetMessagePrefix_Throws_WhenEnvironmentIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => HostEnvironmentMode.GetMessagePrefix(null!));
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "DSharpPlusBot.Tests";

        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
