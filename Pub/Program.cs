using Microsoft.AspNetCore.SignalR.Client;

namespace ChaoticCupid.Pub
{
    internal class Program
    {
        private static async Task Main()
        {
            var connection = new HubConnectionBuilder()
                .WithUrl("http://localhost:7128/messageHub")
                .Build();

            await connection.StartAsync();

            Console.WriteLine("[CUPIDON] Saljem ljubavna pisma svakih 60 sekundi...");

            while (true)
            {
                await connection.InvokeAsync("CupidonTick");
                await Task.Delay(TimeSpan.FromMinutes(1));
            }
        }
    }
}
