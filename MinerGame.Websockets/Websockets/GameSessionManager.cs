using MiningGame.Core.Models;
using MiningGame.Core.Services;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

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

        // Track destroyed blocks and collected chests
        private int _destroyedBlocksCount = 0;
        private int _collectedChestCount = 0;

        // Map dimensions
        private readonly int _mapWidth;
        private readonly int _mapHeight;

        public GameSessionManager()
        {
            _gameService = new GameService(_mapService);
            _character = new Character("Miner", 100, 20, 1.5, 1, 5, 2.0);

            // Initialize map dimensions
            var map = _mapService.GetMap();
            _mapWidth = map.GetLength(0);
            _mapHeight = map.GetLength(1);

            // Set the character's initial position
            _character.PositionX = _mapWidth / 2; // Center horizontally
            _character.PositionY = _mapHeight / 2; // Center vertically

            Task.Run(() => GameLoop()).ContinueWith(task =>
            {
                if (task.Exception != null)
                {
                    Console.WriteLine($"GameLoop failed: {task.Exception.InnerException?.Message}");
                }
            });
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
                        _character.BombAmmo,
                        PositionX = _character.PositionX,
                        PositionY = _character.PositionY
                    },
                    Map = serializableMap,
                    DestroyedBlocksCount = _destroyedBlocksCount,
                    CollectedChestCount = _collectedChestCount
                }
            };

            await SendMessage(webSocket, gameState);
            Console.WriteLine($"Player {sessionId} connected.");
        }
        public async Task HandleMessage(string sessionId, string message)
        {
            if (!_connectedClients.ContainsKey(sessionId)) return;

            try
            {
                var gameAction = JsonSerializer.Deserialize<GameAction>(message);
                if (gameAction != null)
                {
                    Console.WriteLine($"Received message from {sessionId}: {message}");
                    ProcessAction(gameAction);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling message: {ex.Message}");
            }
        }
        private async Task GameLoop()
        {
            try
            {
                while (_isGameRunning)
                {
                    Console.WriteLine("Game Loop Tick: Running...");

                    if (_connectedClients.Count == 0)
                    {
                        Console.WriteLine("No clients connected. Waiting...");
                        await Task.Delay(1000);
                        continue;
                    }

                    if (IsAllBlocksDestroyed())
                    {
                        EndGame("All chests have been destroyed. You win!");
                        break;
                    }

                    if (_character.Stamina <= 0)
                    {
                        _character.RegenerateStamina();

                        if (_character.Stamina <= 0)
                        {
                            EndGame("Game Over: Character ran out of stamina.");
                            break;
                        }

                        BroadcastGameUpdate(new
                        {
                            Event = "StaminaRegenerated",
                            Data = new
                            {
                                Stamina = _character.Stamina,
                                DestroyedBlocksCount = _destroyedBlocksCount,
                                CollectedChestCount = _collectedChestCount
                            }
                        });
                    }
                    else
                    {
                        // Simulate random actions
                        SimulateRandomAction();
                    }

                    await Task.Delay(1000); // Tick rate: 1 second
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GameLoop: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        private void SimulateRandomAction()
        {
            var randomValue = _random.Next(0, 2); // 0 = Move, 1 = PlaceBomb

            if (randomValue == 0)
            {
                var directions = new[] { "Up", "Down", "Left", "Right" };
                var randomDirection = directions[_random.Next(directions.Length)];
                ProcessAction(new GameAction { Type = "Move", Direction = randomDirection });
            }
            else
            {
                if (!_character.IsBombOnCooldown)
                {
                    ProcessAction(new GameAction { Type = "PlaceBomb", X = _character.PositionX, Y = _character.PositionY });
                }
                else
                {
                    Console.WriteLine("Bomb is on cooldown. Skipping bomb placement.");
                }
            }
        }

        private void ProcessAction(GameAction action)
        {
            switch (action.Type)
            {
                case "Move":
                    HandleMovement(action.Direction);
                    break;

                case "PlaceBomb":
                    HandleBombPlacement(action.X, action.Y);
                    break;

                default:
                    Console.WriteLine($"Unknown action: {action.Type}");
                    break;
            }
        }

        private void HandleMovement(string direction)
        {
            int newX = _character.PositionX;
            int newY = _character.PositionY;

            switch (direction)
            {
                case "Up": newY--; break;
                case "Down": newY++; break;
                case "Left": newX--; break;
                case "Right": newX++; break;
            }

            // Check bounds
            if (newX >= 0 && newX < _mapWidth && newY >= 0 && newY < _mapHeight)
            {
                _character.PositionX = newX;
                _character.PositionY = newY;

                Console.WriteLine($"Character moved {direction} to ({_character.PositionX}, {_character.PositionY}).");
                BroadcastGameUpdate(new
                {
                    Event = "CharacterMoved",
                    Data = new
                    {
                        Direction = direction,
                        PositionX = _character.PositionX,
                        PositionY = _character.PositionY,
                        DestroyedBlocksCount = _destroyedBlocksCount,
                        CollectedChestCount = _collectedChestCount
                    }
                });
            }
            else
            {
                Console.WriteLine($"Invalid move {direction}: Out of bounds.");
            }
        }

        private void HandleBombPlacement(int x, int y)
        {
            Console.WriteLine($"Character places bomb at ({x}, {y})");

            var affectedBlocks = _gameService.PlaceBomb(_character, x, y);

            // Count destroyed blocks and chests
            var destroyedBlocks = affectedBlocks.Count(b => b.IsDestroyed);
            var collectedChests = affectedBlocks.Count(b => b.HasChest && b.IsDestroyed);

            _destroyedBlocksCount += destroyedBlocks;
            _collectedChestCount += collectedChests;

            BroadcastGameUpdate(new
            {
                Event = "BombPlaced",
                Data = new
                {
                    AffectedBlocks = affectedBlocks.Select(b => new { b.Health, b.HasChest, b.IsDestroyed }),
                    UpdatedCharacter = new { _character.Stamina },
                    DestroyedBlocksCount = _destroyedBlocksCount,
                    CollectedChestCount = _collectedChestCount
                }
            });
        }

        private bool IsAllBlocksDestroyed()
        {
            var map = _mapService.GetMap();
            for (int i = 0; i < map.GetLength(0); i++)
            {
                for (int j = 0; j < map.GetLength(1); j++)
                {
                    if (map[i, j].HasChest && !map[i, j].IsDestroyed)
                    {
                        return false; // Chests remain
                    }
                }
            }

            return true; // All chests destroyed
        }

        private async void BroadcastGameUpdate(object update)
        {
            var message = JsonSerializer.Serialize(update);

            foreach (var session in _connectedClients.ToList()) // Copy to avoid modification during iteration
            {
                var client = session.Value;

                if (client.State == WebSocketState.Open)
                {
                    try
                    {
                        var bytes = Encoding.UTF8.GetBytes(message);
                        await client.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to send update to client {session.Key}: {ex.Message}");
                        await RemovePlayerFromSession(session.Key); // Remove broken client
                    }
                }
                else
                {
                    await RemovePlayerFromSession(session.Key); // Cleanup disconnected clients
                }
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
                    await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error sending message: {ex.Message}");
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
        private void EndGame(string reason)
        {
            Console.WriteLine($"Game Over: {reason}");
            _isGameRunning = false;

            BroadcastGameUpdate(new
            {
                Event = "GameOver",
                Data = new
                {
                    Message = reason,
                    DestroyedBlocksCount = _destroyedBlocksCount,
                    CollectedChestCount = _collectedChestCount
                }
            });
        }
    }
}
