namespace MiningGame.Core.Models
{
    public class Block
    {
        public int Health { get; set; }
        public bool HasChest { get; set; }
        public Chest? Chest { get; set; }

        public Block(int health, bool hasChest = false, Chest? chest = null)
        {
            Health = health;
            HasChest = hasChest;
            Chest = chest;
        }

        public void TakeDamage(int damage)
        {
            Health = Math.Max(0, Health - damage);
        }

        public bool IsDestroyed => Health == 0;
    }
}
