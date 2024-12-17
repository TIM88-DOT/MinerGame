using MinerGame.Core.Enums;
using MiningGame.Core.Models;
using System;

namespace MiningGame.Core.Services
{
    public class MapService
    {
        private readonly Block[,] _map;

        public MapService()
        {
            _map = InitializeMap(10, 10);
        }

        private Block[,] InitializeMap(int width, int height)
        {
            var random = new Random();
            var map = new Block[width, height];

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    var hasChest = random.NextDouble() > 0.8;
                    var chest = hasChest ? new Chest(random.Next(10, 100), ChestType.Normal) : null;
                    map[x, y] = new Block(random.Next(50, 150), hasChest, chest);
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

                    if (targetX >= 0 && targetX < _map.GetLength(0) && targetY >= 0 && targetY < _map.GetLength(1))
                    {
                        blocks.Add(_map[targetX, targetY]);
                    }
                }
            }

            return blocks;
        }
    }
}
