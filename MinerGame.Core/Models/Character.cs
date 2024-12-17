using System;

namespace MiningGame.Core.Models
{
    public class Character
    {
        public string Name { get; set; }
        public int Stamina { get; private set; }
        public int MaxStamina { get; private set; }
        public int Power { get; private set; }
        public double MovementSpeed { get; private set; }
        public int BombRange { get; private set; }
        public int BombAmmo { get; private set; }
        public double BombCooldown { get; private set; }
        public bool IsOnCooldown { get; private set; }
        public Dictionary<string, bool> SpecialAbilities { get; private set; }

        public Character(string name, int maxStamina, int power, double movementSpeed, int bombRange, int bombAmmo, double bombCooldown)
        {
            Name = name;
            MaxStamina = maxStamina;
            Stamina = maxStamina;
            Power = power;
            MovementSpeed = movementSpeed;
            BombRange = bombRange;
            BombAmmo = bombAmmo;
            BombCooldown = bombCooldown;
            SpecialAbilities = new Dictionary<string, bool>();
        }

        /// <summary>
        /// Uses a bomb, reducing stamina and triggering cooldown if applicable.
        /// </summary>
        public void UseBomb()
        {
            if (Stamina <= 0)
                throw new InvalidOperationException("Not enough stamina to use a bomb!");
            if (IsOnCooldown)
                throw new InvalidOperationException("Bomb is still on cooldown!");

            Stamina--;
            StartCooldown();
        }

        /// <summary>
        /// Regenerates stamina by a percentage of the max stamina.
        /// </summary>
        public void RegenerateStamina()
        {
            Stamina = Math.Min(Stamina + (MaxStamina / 2), MaxStamina);
        }

        /// <summary>
        /// Adds or activates a special ability.
        /// </summary>
        public void AddSpecialAbility(string abilityName)
        {
            if (!SpecialAbilities.ContainsKey(abilityName))
                SpecialAbilities[abilityName] = true;
        }

        /// <summary>
        /// Starts the bomb cooldown asynchronously.
        /// </summary>
        private async void StartCooldown()
        {
            IsOnCooldown = true;
            await Task.Delay(TimeSpan.FromSeconds(BombCooldown));
            IsOnCooldown = false;
        }

        /// <summary>
        /// Checks if a special ability is active.
        /// </summary>
        public bool HasSpecialAbility(string abilityName)
        {
            return SpecialAbilities.ContainsKey(abilityName) && SpecialAbilities[abilityName];
        }

    }
}
