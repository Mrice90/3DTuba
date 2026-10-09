// Port of game-core/src/main/java/com/infiniteconquest/core/CardInstance.java @ Desolate-Tuba e0e565b
using System;
using InfiniteConquest.RulesCore.Data;

namespace InfiniteConquest.RulesCore.Core
{
    /// <summary>
    /// Mutable live card instance.
    /// DEVIATION (per task contract): Java UUID instanceId → long, issued from
    /// a GameState-owned counter seeded through the RNG stream. No Guid.
    /// </summary>
    public sealed class CardInstance
    {
        public long InstanceId { get; }
        public CardDefinition Definition { get; }
        public int Owner { get; }
        public Zone Zone { get; private set; }
        public int Damage { get; private set; }
        public bool Tapped { get; private set; }
        public int MovementSpent { get; private set; }
        public bool AttackedThisTurn { get; private set; }
        public bool BlinkUsedThisTurn { get; private set; }
        public int AttackBonus { get; private set; }
        public int DefenseBonus { get; private set; }
        public int CombatDamage { get; private set; }
        public bool AbilityUsedThisTurn { get; private set; }

        public CardInstance(long instanceId, CardDefinition definition, int owner, Zone zone)
        {
            InstanceId = instanceId;
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            if (owner < 0 || owner > 1) throw new ArgumentException("Owner must be player 0 or 1");
            Owner = owner;
            Zone = zone;
        }

        /// <summary>
        /// Deep copy of a live instance; the definition reference is shared
        /// (definitions are immutable).
        /// </summary>
        internal CardInstance(CardInstance source)
            : this(source.InstanceId, source.Definition, source.Owner, source.Zone)
        {
            Damage = source.Damage;
            Tapped = source.Tapped;
            MovementSpent = source.MovementSpent;
            AttackedThisTurn = source.AttackedThisTurn;
            BlinkUsedThisTurn = source.BlinkUsedThisTurn;
            AttackBonus = source.AttackBonus;
            DefenseBonus = source.DefenseBonus;
            CombatDamage = source.CombatDamage;
            AbilityUsedThisTurn = source.AbilityUsedThisTurn;
        }

        public int MovementRemaining() => Math.Max(0, Definition.Movement - MovementSpent);
        public int EffectiveAttack() => Definition.Attack + AttackBonus;
        public int EffectiveDefense() => Definition.Defense + DefenseBonus;
        public int DefenseRemaining() => Math.Max(0, EffectiveDefense() - CombatDamage);

        public void MoveTo(Zone newZone) { Zone = newZone; }

        public void AddDamage(int amount)
        {
            if (amount < 0) throw new ArgumentException("Damage cannot be negative");
            Damage += amount;
        }

        public void SetTapped(bool value) { Tapped = value; }

        public void HealDamage(int amount)
        {
            if (amount < 0) throw new ArgumentException("Healing cannot be negative");
            Damage = Math.Max(0, Damage - amount);
        }

        public void AddAttackBonus(int amount)
        {
            if (amount < 0) throw new ArgumentException("Bonus cannot be negative");
            AttackBonus += amount;
        }

        public void AddDefenseBonus(int amount)
        {
            if (amount < 0) throw new ArgumentException("Bonus cannot be negative");
            DefenseBonus += amount;
        }

        public void AddCombatDamage(int amount)
        {
            if (amount < 0) throw new ArgumentException("Combat damage cannot be negative");
            CombatDamage += amount;
        }

        public void SpendMovement(int amount)
        {
            if (amount < 0 || amount > MovementRemaining()) throw new ArgumentException("Insufficient movement");
            MovementSpent += amount;
        }

        public void RestoreMovement(int amount)
        {
            if (amount < 0) throw new ArgumentException("Movement restoration cannot be negative");
            MovementSpent = Math.Max(0, MovementSpent - amount);
        }

        public void MarkAttacked() { AttackedThisTurn = true; }
        public void MarkBlinkUsed() { BlinkUsedThisTurn = true; }
        public void MarkAbilityUsed() { AbilityUsedThisTurn = true; }

        public void ResetTurnActions()
        {
            MovementSpent = 0;
            AttackedThisTurn = false;
            BlinkUsedThisTurn = false;
            AbilityUsedThisTurn = false;
            AttackBonus = 0;
            DefenseBonus = 0;
            Tapped = false;
        }

        public void HealCombatDamage(int amount)
        {
            if (amount < 0) throw new ArgumentException("Healing cannot be negative");
            CombatDamage = Math.Max(0, CombatDamage - amount);
        }

        public void ClearCombatDamage() { CombatDamage = 0; }
    }
}
