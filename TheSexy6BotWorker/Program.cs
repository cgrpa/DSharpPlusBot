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

            builder.Services
                .AddHostedService<DiscordWorker>();



            var host = builder.Build();
            host.Run();
        }
    }
}