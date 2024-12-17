using Microsoft.AspNetCore.Mvc;
using MiningGame.Core.Models;
using MiningGame.Core.Services;

namespace MiningGame.WebSockets.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GameController : ControllerBase
    {
        private readonly GameService _gameService;
        private readonly Character _character;

        public GameController()
        {
            var mapService = new MapService();
            _gameService = new GameService(mapService);
            _character = new Character("Miner", 100, 20, 1.5, 1, 5, 2.0);
        }

        [HttpGet("state")]
        public IActionResult GetGameState()
        {
            // Convert the map to a serializable format
            var serializableMap = SerializationHelper.ConvertToSerializableMap(_gameService.GetMap());

            var state = new
            {
                Character = new
                {
                    Name = _character.Name,
                    Stamina = _character.Stamina,
                    MaxStamina = _character.MaxStamina,
                    Power = _character.Power,
                    MovementSpeed = _character.MovementSpeed,
                    BombRange = _character.BombRange,
                    BombAmmo = _character.BombAmmo
                },
                Map = serializableMap
            };

            return Ok(state);
        }
    }
}
