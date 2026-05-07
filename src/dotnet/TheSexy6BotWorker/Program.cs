using Microsoft.SemanticKernel;
using TheSexy6BotWorker.Handlers;

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
            builder.Configuration.AddUserSecrets<Program>();

            if (!isSmokeTest)
            {
                builder.Services
                    .AddHostedService<DiscordWorker>();
            }


            using var host = builder.Build();

            if (isSmokeTest)
            {
                Console.WriteLine("Container smoke test passed.");
                Console.WriteLine($"APP_VERSION={Environment.GetEnvironmentVariable("APP_VERSION") ?? "unset"}");
                Console.WriteLine($"APP_COMMIT_MSG={Environment.GetEnvironmentVariable("APP_COMMIT_MSG") ?? "unset"}");
                return 0;
            }
            host.Run();
            return 0;
        }
    }
}
