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

        // Remember the last move direction to reduce twitchy back-and-forth
        private string _lastMoveDirection = null;

        public GameSessionManager()
        {
            _gameService = new GameService(_mapService);
            _character = new Character("Miner", 100, 9999, 1.5, 1, 5, 2.0);

            // Initialize map dimensions
            var map = _mapService.GetMap();
            _mapWidth = map.GetLength(0);
            _mapHeight = map.GetLength(1);

            // Set the character's initial position
            _character.PositionX = 7;
            _character.PositionY = 7;

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
            // Look for any adjacent tile that is intact.
            var adjacentIntactTile = GetAdjacentIntactTile();

            // If we found one and bomb is not on cooldown, place a bomb there.
            if (adjacentIntactTile.HasValue && !_character.IsBombOnCooldown)
            {
                var (targetX, targetY) = adjacentIntactTile.Value;
                Console.WriteLine($"Found an adjacent intact tile at ({targetX}, {targetY}), placing bomb...");

                ProcessAction(new GameAction
                {
                    Type = "PlaceBomb",
                    X = targetX,
                    Y = targetY
                });
            }
            else
            {
                // Otherwise, move (or skip if no valid moves).
                MoveOrSkip();
            }
        }

        /// <summary>
        /// Returns the coordinates of an adjacent tile (Up/Down/Left/Right) 
        /// that is still intact (i.e. IsDestroyed == false), or null if none exist.
        /// </summary>
        private (int X, int Y)? GetAdjacentIntactTile()
        {
            var map = _mapService.GetMap();

            // Offsets for the four adjacent tiles
            var neighbors = new (int offsetX, int offsetY)[]
            {
                (0, -1),  // Up
                (0,  1),  // Down
                (-1, 0),  // Left
                (1,  0)   // Right
            };

            foreach (var (ox, oy) in neighbors)
            {
                int nx = _character.PositionX + ox;
                int ny = _character.PositionY + oy;

                // Check bounds
                if (nx >= 0 && nx < _mapWidth && ny >= 0 && ny < _mapHeight)
                {
                    // If the neighbor is still intact, return it
                    if (!map[nx, ny].IsDestroyed)
                    {
                        return (nx, ny);
                    }
                }
            }

            // No adjacent intact tile found
            return null;
        }

        private void MoveOrSkip()
        {
            var validMoves = GetValidMoves();
            if (validMoves.Any())
            {
                // Shuffle the valid moves randomly
                var shuffled = validMoves.OrderBy(_ => _random.Next()).ToList();

                // Try to pick one that isn't the opposite of the last move
                string chosenDirection = null;
                foreach (var dir in shuffled)
                {
                    if (!IsOpposite(dir, _lastMoveDirection))
                    {
                        chosenDirection = dir;
                        break;
                    }
                }

                // If all moves are opposite or we didn't find a non-opposite,
                // just pick the first valid one anyway
                if (chosenDirection == null)
                {
                    chosenDirection = shuffled.First();
                }

                // Remember this move for the next time
                _lastMoveDirection = chosenDirection;

                // Perform the move
                ProcessAction(new GameAction
                {
                    Type = "Move",
                    Direction = chosenDirection
                });
            }
            else
            {
                Console.WriteLine("No valid moves available. Skipping turn.");
            }
        }

        /// <summary>
        /// Checks whether two directions are opposites, e.g. "Up" vs "Down".
        /// </summary>
        private bool IsOpposite(string dir1, string dir2)
        {
            if (dir1 == null || dir2 == null) return false;

            return (dir1 == "Up" && dir2 == "Down")
                || (dir1 == "Down" && dir2 == "Up")
                || (dir1 == "Left" && dir2 == "Right")
                || (dir1 == "Right" && dir2 == "Left");
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

            var map = _mapService.GetMap();

            // Now we allow movement *onto* a tile if it *is* destroyed
            if (newX >= 0 && newX < _mapWidth &&
                newY >= 0 && newY < _mapHeight &&
                map[newX, newY].IsDestroyed)
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
                Console.WriteLine($"Invalid move {direction}: Cannot move through intact blocks or out of bounds.");
            }
        }

        private List<string> GetValidMoves()
        {
            var validMoves = new List<string>();
            var map = _mapService.GetMap();

            bool IsTilePassable(int x, int y) => map[x, y].IsDestroyed;

            // Up
            if (_character.PositionY > 0 &&
                IsTilePassable(_character.PositionX, _character.PositionY - 1))
            {
                validMoves.Add("Up");
            }
            // Down
            if (_character.PositionY < _mapHeight - 1 &&
                IsTilePassable(_character.PositionX, _character.PositionY + 1))
            {
                validMoves.Add("Down");
            }
            // Left
            if (_character.PositionX > 0 &&
                IsTilePassable(_character.PositionX - 1, _character.PositionY))
            {
                validMoves.Add("Left");
            }
            // Right
            if (_character.PositionX < _mapWidth - 1 &&
                IsTilePassable(_character.PositionX + 1, _character.PositionY))
            {
                validMoves.Add("Right");
            }

            return validMoves;
        }

        private void HandleBombPlacement(int x, int y)
        {
            var map = _mapService.GetMap();

            // Check if there is a block to destroy
            if (!map[x, y].IsDestroyed)
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
            else
            {
                Console.WriteLine($"Cannot place bomb at ({x}, {y}): No block to destroy.");
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

                        // Print ASCII map to console
                        Console.WriteLine(GenerateAsciiMap());
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

        private string GenerateAsciiMap()
        {
            var map = _mapService.GetMap();
            var builder = new StringBuilder();

            for (int y = 0; y < map.GetLength(1); y++)
            {
                for (int x = 0; x < map.GetLength(0); x++)
                {
                    if (x == _character.PositionX && y == _character.PositionY)
                    {
                        builder.Append("P "); // Player
                    }
                    else if (map[x, y].IsDestroyed)
                    {
                        builder.Append("  "); // Empty space for destroyed blocks
                    }
                    else if (map[x, y].HasChest)
                    {
                        builder.Append("C "); // Chest
                    }
                    else
                    {
                        builder.Append("# "); // Block
                    }
                }
                builder.AppendLine(); // New line for each row
            }

            return builder.ToString();
        }
    }
}
