using ChaoticCupid.PubSubApp;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
builder.Services.AddSingleton<MatchmakingService>();
builder.Services.AddSingleton<IPersonService>(sp => sp.GetRequiredService<MatchmakingService>());
builder.Services.AddSingleton<ICupidService>(sp => sp.GetRequiredService<MatchmakingService>());

var app = builder.Build();

app.MapHub<MessageHub>("/messageHub");

app.Run();
