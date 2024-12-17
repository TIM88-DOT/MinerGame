using System.Linq;
using MiningGame.Core.Models;

namespace MiningGame.Core.Services
{
    public class GameService
    {
        private readonly MapService _mapService;

        public GameService(MapService mapService)
        {
            _mapService = mapService;
        }

        public Block[,] GetMap()
        {
            return _mapService.GetMap();
        }

        public IEnumerable<Block> PlaceBomb(Character character, int x, int y)
        {
            if (character.BombAmmo <= 0)
                throw new InvalidOperationException("Character is out of bombs!");

            character.UseBomb();

            var blocks = _mapService.GetBlocksInRange(x, y, character.BombRange);
            foreach (var block in blocks)
            {
                block.TakeDamage(character.Power);
            }

            return blocks;
        }
    }
}
