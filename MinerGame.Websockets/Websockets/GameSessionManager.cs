using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using MiningGame.Core.Models;
using MiningGame.Core.Services;

namespace MiningGame.WebSockets
{
    public class GameSessionManager
    {
        private readonly Dictionary<string, WebSocket> _connectedClients = new();
        private readonly MapService _mapService = new();
        private readonly GameService _gameService;
        private readonly Character _character;

        private readonly Random _random = new();
        private bool _isGameRunning = true;

        public GameSessionManager()
        {
            _gameService = new GameService(_mapService);
            _character = new Character("Miner", 100, 20, 1.5, 1, 5, 2.0);

            // Start the game loop
            Task.Run(() => GameLoop());
        }

        public async Task AddPlayerToSession(string sessionId, WebSocket webSocket)
        {
            _connectedClients[sessionId] = webSocket;

            var serializableMap = SerializationHelper.ConvertToSerializableMap(_gameService.GetMap());

            var gameState = new GameMessage
            {
                Event = "GameState",
                Data = new
                {
                    Character = new
                    {
                        _character.Stamina,
                        _character.MaxStamina,
                        _character.Power,
                        _character.MovementSpeed,
                        _character.BombRange,
                        _character.BombAmmo
                    },
                    Map = serializableMap
                }
            };

            // Send the initial state to the client
            await SendMessage(webSocket, gameState);
        }

        public async Task HandleMessage(string sessionId, string message)
        {
            if (!_connectedClients.ContainsKey(sessionId)) return;

            try
            {
                var gameAction = JsonSerializer.Deserialize<GameAction>(message);
                if (gameAction != null)
                {
                    // Handle manual actions from the client
                    ProcessAction(gameAction);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling message: {ex.Message}");
            }
        }

        private async Task SendMessage(WebSocket webSocket, GameMessage message)
        {
            if (webSocket.State == WebSocketState.Open)
            {
                try
                {
                    var json = JsonSerializer.Serialize(message);
                    var bytes = Encoding.UTF8.GetBytes(json);
                    var buffer = new ArraySegment<byte>(bytes);

                    await webSocket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error sending message: {ex.Message}");
                }
            }
        }

        private async Task GameLoop()
        {
            while (_isGameRunning)
            {
                // Simulate random actions every second
                SimulateRandomAction();

                // Send periodic updates to all connected clients
                await BroadcastGameState();

                await Task.Delay(1000); // Adjust tick rate as needed
            }
        }

        private void SimulateRandomAction()
        {
            var randomValue = _random.Next(0, 2); // 0 = Move, 1 = PlaceBomb

            if (randomValue == 0)
            {
                // Simulate a move action
                var directions = new[] { "Up", "Down", "Left", "Right" };
                var randomDirection = directions[_random.Next(directions.Length)];
                ProcessAction(new GameAction { Type = "Move", Direction = randomDirection });
            }
            else
            {
                // Simulate a bomb placement
                var x = _random.Next(0, 10); // Adjust to your map size
                var y = _random.Next(0, 10);
                ProcessAction(new GameAction { Type = "PlaceBomb", X = x, Y = y });
            }
        }

        private void ProcessAction(GameAction action)
        {
            switch (action.Type)
            {
                case "Move":
                    Console.WriteLine($"Simulated move: {action.Direction}");
                    // Simulate character movement logic here
                    break;

                case "PlaceBomb":
                    Console.WriteLine($"Simulated bomb placement at ({action.X}, {action.Y})");
                    var affectedBlocks = _gameService.PlaceBomb(_character, action.X, action.Y);

                    BroadcastGameUpdate(new
                    {
                        Event = "BombPlaced",
                        Data = new
                        {
                            AffectedBlocks = affectedBlocks.Select(b => new { b.Health, b.HasChest, b.IsDestroyed }),
                            UpdatedCharacter = new { _character.Stamina }
                        }
                    });
                    break;

                default:
                    Console.WriteLine($"Unknown action: {action.Type}");
                    break;
            }
        }

        private async Task BroadcastGameState()
        {
            var gameState = new GameMessage
            {
                Event = "GameStateUpdate",
                Data = new
                {
                    Character = new
                    {
                        _character.Stamina,
                        _character.Power,
                        _character.BombAmmo
                    }
                }
            };

            foreach (var client in _connectedClients.Values)
            {
                await SendMessage(client, gameState);
            }
        }

        private async void BroadcastGameUpdate(object update)
        {
            var message = JsonSerializer.Serialize(update);

            foreach (var client in _connectedClients.Values)
            {
                if (client.State == WebSocketState.Open)
                {
                    var bytes = Encoding.UTF8.GetBytes(message);
                    await client.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }
        }

        public async Task RemovePlayerFromSession(string sessionId)
        {
            if (_connectedClients.TryGetValue(sessionId, out var webSocket))
            {
                if (webSocket.State == WebSocketState.Open)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Session ended", CancellationToken.None);
                    Console.WriteLine($"Player {sessionId} disconnected.");
                }
                _connectedClients.Remove(sessionId);
            }
        }
    }

}
