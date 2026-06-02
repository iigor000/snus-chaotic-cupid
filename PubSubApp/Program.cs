using ChaoticCupid.PubSubApp;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();

var app = builder.Build();

app.MapHub<MessageHub>("/messageHub");

app.Run();
