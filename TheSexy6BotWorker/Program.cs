using Microsoft.SemanticKernel;
using TheSexy6BotWorker.Handlers;

namespace TheSexy6BotWorker
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);
            builder.Configuration.AddUserSecrets<Program>();
            // Ensure console logs include Debug by default unless overridden by env
            builder.Logging.ClearProviders();
            builder.Logging.AddSimpleConsole(o =>
            {
                o.SingleLine = true;
                o.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
                o.IncludeScopes = false;
            });
            builder.Logging.SetMinimumLevel(LogLevel.Debug);

            builder.Services
                .AddHostedService<DiscordWorker>();



            var host = builder.Build();
            host.Run();
        }
    }
}
