using MinerGame.Core.Enums;
using MiningGame.Core.Models;

namespace MiningGame.Core.Services
{
    public class MapService
    {
        private readonly Block[,] _map;

        public MapService()
        {
            // Create a map of 80 x 45
            _map = InitializeMap(80, 45);
        }

        private Block[,] InitializeMap(int width, int height)
        {
            var random = new Random();
            var map = new Block[width, height];

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    // 10% chance to have a chest
                    var hasChest = random.NextDouble() < 0.1;

                    // Create a new chest if hasChest is true
                    var chest = hasChest
                        ? new Chest(random.Next(10, 100), ChestType.Normal)
                        : null;

                    // health is a random number between 1 and 50
                    var health = random.Next(1, 51);

                    map[x, y] = new Block(health, hasChest, chest);
                }
            }

            return map;
        }

        public Block[,] GetMap()
        {
            return _map;
        }

        public IEnumerable<Block> GetBlocksInRange(int x, int y, int range)
        {
            var blocks = new List<Block>();

            for (int i = -range; i <= range; i++)
            {
                for (int j = -range; j <= range; j++)
                {
                    int targetX = x + i;
                    int targetY = y + j;

                    if (targetX >= 0 && targetX < _map.GetLength(0) &&
                        targetY >= 0 && targetY < _map.GetLength(1))
                    {
                        blocks.Add(_map[targetX, targetY]);
                    }
                }
            }

            return blocks;
        }
    }
}
