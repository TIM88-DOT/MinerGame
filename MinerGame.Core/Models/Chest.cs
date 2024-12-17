using MinerGame.Core.Enums;

namespace MiningGame.Core.Models
{
    public class Chest
    {
        public int Money { get; set; }
        public ChestType Type { get; set; }

        public Chest(int money, ChestType type)
        {
            Money = money;
            Type = type;
        }
    }
}
