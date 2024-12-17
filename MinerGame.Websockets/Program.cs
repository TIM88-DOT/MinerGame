using Microsoft.AspNetCore.Builder;
using MiningGame.WebSockets;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
var app = builder.Build();

app.UseWebSockets(); // Enable WebSocket middleware
app.Map("/ws", WebSocketHandlerService.HandleConnection); // WebSocket endpoint

app.MapControllers(); // Fallback for REST endpoints (if needed)

app.Run();
