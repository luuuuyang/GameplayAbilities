using System.Reflection;
using GameplayTags;
using UnityEngine;

namespace GameplayAbilities.Tests
{
    public class AbilitySystemTestAttributeSet : AttributeSet
    {
        public float MaxHealth;
        public float Health;
        public float MaxMana;
        public float Mana;
        public float Damage;
        public float SpellDamage;
        public float PhysicalDamage;
        public float CritChance;
        public float CritMultiplier;
        public float ArmorDamageReduction;
        public float DodgeChance;
        public float LifeSteal;
        public float Strength;
        public float StackingAttribute1;
        public float StackingAttribute2;
        public float NoStackAttribute;

        public override bool PreGameplayEffectExecute(GameplayEffectModCallbackData data)
        {
            return true;
        }

        public override void PostGameplayEffectExecute(GameplayEffectModCallbackData data)
        {
            FieldInfo damageField = typeof(AbilitySystemTestAttributeSet).GetField("Damage");
            FieldInfo modifiedField = data.EvaluatedData.Attribute.GetField();
            if (damageField == modifiedField)
            {
                if (data.EffectSpec.CapturedSourceTags.AggregatedTags.HasTag(GameplayTag.RequestGameplayTag("FireDamage")))
                {

                }

                Health -= Damage;
                Damage = 0;
            }
        }
    }
}
