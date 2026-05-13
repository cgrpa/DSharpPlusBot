using TheSexy6BotWorker.Configuration;
using TheSexy6BotWorker.Services;

namespace TheSexy6BotWorker
{
    public class Program
    {
        private const string SmokeTestArgument = "--smoke-test";

        public static int Main(string[] args)
        {
            var isSmokeTest = args.Contains(SmokeTestArgument, StringComparer.OrdinalIgnoreCase);
            var hostArgs = args
                .Where(arg => !string.Equals(arg, SmokeTestArgument, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            var builder = Host.CreateApplicationBuilder(hostArgs);
            if (HostEnvironmentMode.ShouldLoadUserSecrets(builder.Environment))
            {
                builder.Configuration.AddUserSecrets<Program>();
            }

            builder.Services
                .AddOptions<McpOptions>()
                .Bind(builder.Configuration.GetSection(McpOptions.SectionName));

            builder.Services.AddSingleton<IEnvironmentVariableProvider, ProcessEnvironmentVariableProvider>();
            builder.Services.AddSingleton<McpServerConfigurationResolver>();
            builder.Services.AddSingleton<IMcpKernelPluginRegistrar, SemanticKernelMcpPluginRegistrar>();
            builder.Services.AddSingleton<IMcpServerPluginAliasProvider, StableMcpServerPluginAliasProvider>();
            builder.Services.AddSingleton<IMcpServerToolDiscoveryClient, NoOpStreamableHttpMcpToolDiscoveryClient>();
            builder.Services.AddSingleton<IMcpServerToolDiscoveryClient, NoOpSseMcpToolDiscoveryClient>();
            builder.Services.AddSingleton<IMcpJitterProvider, RandomMcpJitterProvider>();
            builder.Services.AddSingleton<IMcpReconnectDelayPolicy, ExponentialMcpReconnectDelayPolicy>();
            builder.Services.AddSingleton<IMcpDelayScheduler, SystemMcpDelayScheduler>();
            builder.Services.AddSingleton<IMcpRuntimeTelemetrySink, LoggerMcpRuntimeTelemetrySink>();
            builder.Services.AddSingleton<IMcpRuntimeClient, NoOpMcpRuntimeClient>();
            builder.Services.AddSingleton<IMcpRuntimeSupervisor, McpRuntimeSupervisor>();
            builder.Services.AddSingleton<IMcpToolInvoker, SupervisedMcpToolInvoker>();
            builder.Services.AddSingleton<McpKernelPluginRegistrationCoordinator>();
            builder.Services.AddSingleton<IMcpFeature, McpFeature>();

            if (!isSmokeTest)
            {
                builder.Services
                    .AddHostedService<DiscordWorker>();
            }


            using var host = builder.Build();

            if (isSmokeTest)
            {
                Console.WriteLine("Container smoke test passed.");
                Console.WriteLine($"DOTNET_ENVIRONMENT={builder.Environment.EnvironmentName}");
                Console.WriteLine($"APP_VERSION={Environment.GetEnvironmentVariable("APP_VERSION") ?? "unset"}");
                Console.WriteLine($"APP_COMMIT_MSG={Environment.GetEnvironmentVariable("APP_COMMIT_MSG") ?? "unset"}");
                return 0;
            }
            host.Run();
            return 0;
        }
    }
}
