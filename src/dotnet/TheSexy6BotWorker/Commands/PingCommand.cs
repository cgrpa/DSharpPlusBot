using DSharpPlus.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheSexy6BotWorker.Commands
{
    public class PingCommand
    {
        [Command("ping")]

        public static async ValueTask ExecuteAsync(CommandContext context)  =>
            await context.RespondAsync($"Pong!");
    }
}
