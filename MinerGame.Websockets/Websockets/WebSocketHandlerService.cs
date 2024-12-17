using System.Net.WebSockets;
using System.Text;

namespace MiningGame.WebSockets
{
    public static class WebSocketHandlerService
    {
        private static readonly GameSessionManager SessionManager = new();

        public static async Task HandleConnection(HttpContext context)
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                return;
            }

            var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            var sessionId = Guid.NewGuid().ToString();

            await SessionManager.AddPlayerToSession(sessionId, webSocket);

            try
            {
                await ListenForMessages(sessionId, webSocket);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in WebSocket session: {ex.Message}");
            }
            finally
            {
                await SessionManager.RemovePlayerFromSession(sessionId);
            }
        }

        private static async Task ListenForMessages(string sessionId, WebSocket webSocket)
        {
            var buffer = new byte[1024 * 4];

            while (webSocket.State == WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    await SessionManager.HandleMessage(sessionId, message);
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }
            }
        }
    }
}
