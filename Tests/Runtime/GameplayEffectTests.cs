using NUnit.Framework;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameplayAbilities.Tests
{
    public class GameplayEffectTests
    {
        private GameObject SourceObj;
        private GameObject DestObj;
        private AbilitySystemComponent SourceComponent;
        private AbilitySystemComponent DestComponent;

        [SetUp]
        public void Setup()
        {
            SourceObj = new GameObject();
            DestObj = new GameObject();

            SourceComponent = SourceObj.AddComponent<AbilitySystemComponent>();
            DestComponent = DestObj.AddComponent<AbilitySystemComponent>();

            SourceComponent.InitStats(typeof(AbilitySystemTestAttributeSet));
            DestComponent.InitStats(typeof(AbilitySystemTestAttributeSet));

            // 初始化属性值
            const float startingHealth = 100f;
            const float startingMana = 200f;

            SourceComponent.GetSet<AbilitySystemTestAttributeSet>().Health = startingHealth;
            SourceComponent.GetSet<AbilitySystemTestAttributeSet>().MaxHealth = startingHealth;
            SourceComponent.GetSet<AbilitySystemTestAttributeSet>().Mana = startingMana;
            SourceComponent.GetSet<AbilitySystemTestAttributeSet>().MaxMana = startingMana;

            DestComponent.GetSet<AbilitySystemTestAttributeSet>().Health = startingHealth;
            DestComponent.GetSet<AbilitySystemTestAttributeSet>().MaxHealth = startingHealth;
            DestComponent.GetSet<AbilitySystemTestAttributeSet>().Mana = startingMana;
            DestComponent.GetSet<AbilitySystemTestAttributeSet>().MaxMana = startingMana;
        }

        [Test]
        public void Test_InstantDamage()
        {
            const float damageValue = 5f;
            float startingHealth = DestComponent.GetSet<AbilitySystemTestAttributeSet>().Health;

            // 创建即时伤害效果
            GameplayEffect effect = ScriptableObject.CreateInstance<GameplayEffect>();
            effect.DurationPolicy = GameplayEffectDurationType.Instant;
            AddModifier(effect, typeof(AbilitySystemTestAttributeSet).GetField("Health"), GameplayModOp.Additive, -damageValue);

            // 应用效果
            SourceComponent.ApplyGameplayEffectToTarget(effect, DestComponent, 1);

            // 验证生命值减少
            Assert.AreEqual(startingHealth - damageValue, DestComponent.GetSet<AbilitySystemTestAttributeSet>().Health, "Health Reduced");
        }

        [Test]
        public void Test_InstantDamageRemap()
        {
            const float damageValue = 5f;
            float startingHealth = DestComponent.GetSet<AbilitySystemTestAttributeSet>().Health;

            // 创建即时伤害效果
            GameplayEffect baseDmgEffect = ScriptableObject.CreateInstance<GameplayEffect>();
            AddModifier(baseDmgEffect, typeof(AbilitySystemTestAttributeSet).GetField("Damage"), GameplayModOp.Additive, damageValue);
            baseDmgEffect.DurationPolicy = GameplayEffectDurationType.Instant;

            // 应用效果
            SourceComponent.ApplyGameplayEffectToTarget(baseDmgEffect, DestComponent, 1);

            // 验证生命值减少
            Assert.AreEqual(startingHealth - damageValue, DestComponent.GetSet<AbilitySystemTestAttributeSet>().Health, "Health Reduced");

            Assert.AreEqual(0, DestComponent.GetSet<AbilitySystemTestAttributeSet>().Damage, "Damage Applied");
        }

        [Test]
        public void Test_ManaBuff()
        {
            const float buffValue = 30f;
            float startingMana = DestComponent.GetSet<AbilitySystemTestAttributeSet>().Mana;

            ActiveGameplayEffectHandle buffHandle;

            GameplayEffect damageBuffEffect = ScriptableObject.CreateInstance<GameplayEffect>();
            AddModifier(damageBuffEffect, typeof(AbilitySystemTestAttributeSet).GetField("Mana"), GameplayModOp.Additive, buffValue);
            damageBuffEffect.DurationPolicy = GameplayEffectDurationType.Infinite;

            buffHandle = SourceComponent.ApplyGameplayEffectToTarget(damageBuffEffect, DestComponent, 1);

            Assert.AreEqual(startingMana + buffValue, DestComponent.GetSet<AbilitySystemTestAttributeSet>().Mana, "Mana Buffed");

            DestComponent.RemoveActiveGameplayEffect(buffHandle);

            Assert.AreEqual(startingMana, DestComponent.GetSet<AbilitySystemTestAttributeSet>().Mana, "Mana Restored");
        }

        [UnityTest]
        public IEnumerator Test_PeriodicDamage()
        {
            const int numPeriod = 10;
            const float periodSecs = 1f;
            const float damagePerPeriod = 5f;
            float startingHealth = DestComponent.GetSet<AbilitySystemTestAttributeSet>().Health;

            GameplayEffect baseDamageEffect = ScriptableObject.CreateInstance<GameplayEffect>();
            AddModifier(baseDamageEffect, typeof(AbilitySystemTestAttributeSet).GetField("Health"), GameplayModOp.Additive, -damagePerPeriod);
            baseDamageEffect.DurationPolicy = GameplayEffectDurationType.HasDuration;
            baseDamageEffect.DurationMagnitude = new GameplayEffectModifierMagnitude(new ScalableFloat(numPeriod * periodSecs));
            baseDamageEffect.Period.Value = periodSecs;

            SourceComponent.ApplyGameplayEffectToTarget(baseDamageEffect, DestComponent, 1);

            int numApplications = 0;

            yield return null;
            numApplications++;

            Assert.AreEqual(startingHealth - damagePerPeriod * numApplications, DestComponent.GetSet<AbilitySystemTestAttributeSet>().Health, "Health Reduced");

            yield return new WaitForSeconds(periodSecs * 0.1f);

            for (int i = 0; i < numPeriod; i++)
            {
                yield return new WaitForSeconds(periodSecs);

                numApplications++;

                Assert.AreEqual(startingHealth - damagePerPeriod * numApplications, DestComponent.GetSet<AbilitySystemTestAttributeSet>().Health, "Health Reduced");
            }

            yield return new WaitForSeconds(periodSecs);

            Assert.AreEqual(startingHealth - damagePerPeriod * numApplications, DestComponent.GetSet<AbilitySystemTestAttributeSet>().Health, "Health Reduced");
        }

        [Test]
        public void Test_StackLimit()
        {
            const float duration = 10f;
            const float halfDuration = duration / 2f;
            const float changePerGE = 5f;
            const int stackLimit = 2;
            float startingAttributeValue = DestComponent.GetSet<AbilitySystemTestAttributeSet>().StackingAttribute1;

            GameplayEffect stackingEffect = ScriptableObject.CreateInstance<GameplayEffect>();
            AddModifier(stackingEffect, typeof(AbilitySystemTestAttributeSet).GetField("StackingAttribute1"), GameplayModOp.Additive, changePerGE);
            stackingEffect.DurationPolicy = GameplayEffectDurationType.HasDuration;
            stackingEffect.DurationMagnitude = new GameplayEffectModifierMagnitude(new ScalableFloat(duration));
            stackingEffect.StackLimitCount = stackLimit;
            stackingEffect.StackingType = GameplayEffectStackingType.AggregateByTarget;
            stackingEffect.StackDurationRefreshPolicy = GameplayEffectStackingDurationPolicy.NeverRefresh;
            stackingEffect.StackExpirationPolicy = GameplayEffectStackingExpirationPolicy.ClearEntireStack;

            for (int i = 0; i <= stackLimit; i++)
            {
                SourceComponent.ApplyGameplayEffectToTarget(stackingEffect, DestComponent, 1);
            }

            Assert.AreEqual(startingAttributeValue + stackLimit * changePerGE, DestComponent.GetSet<AbilitySystemTestAttributeSet>().StackingAttribute1, "Stacking GEs");
        }

        [UnityTest]
        public IEnumerator Test_SetByCallerStackingDuration()
        {
            const float duration = 10f;
            const float halfDuration = duration / 2f;
            const float changePerGE = 5f;
            const int stackLimit = 2;
            float startingAttributeValue = DestComponent.GetSet<AbilitySystemTestAttributeSet>().StackingAttribute1;

            string durationName = "Duration";
            SetByCallerFloat setByCallerDuration = new()
            {
                DataName = durationName,
            };

            GameplayEffect stackingEffect = ScriptableObject.CreateInstance<GameplayEffect>();
            AddModifier(stackingEffect, typeof(AbilitySystemTestAttributeSet).GetField("StackingAttribute1"), GameplayModOp.Additive, changePerGE);
            stackingEffect.DurationPolicy = GameplayEffectDurationType.HasDuration;
            stackingEffect.DurationMagnitude = new GameplayEffectModifierMagnitude(setByCallerDuration);
            stackingEffect.StackLimitCount = stackLimit;
            stackingEffect.StackingType = GameplayEffectStackingType.AggregateByTarget;
            stackingEffect.StackDurationRefreshPolicy = GameplayEffectStackingDurationPolicy.NeverRefresh;
            stackingEffect.StackExpirationPolicy = GameplayEffectStackingExpirationPolicy.RemoveSingleStackAndRefreshDuration;

            {
                GameplayEffectSpec spec = new(stackingEffect, new GameplayEffectContextHandle(), 1);
                spec.SetSetByCallerMagnitude(durationName, duration);
                SourceComponent.ApplyGameplayEffectSpecToTarget(spec, DestComponent);
            }

            yield return new WaitForSeconds(halfDuration);

            Assert.AreEqual(startingAttributeValue + changePerGE, DestComponent.GetSet<AbilitySystemTestAttributeSet>().StackingAttribute1, "Stacking GEs");

            {
                GameplayEffectSpec spec = new(stackingEffect, new GameplayEffectContextHandle(), 1);
                spec.SetSetByCallerMagnitude(durationName, duration);
                SourceComponent.ApplyGameplayEffectSpecToTarget(spec, DestComponent);
            }

            Assert.AreEqual(startingAttributeValue + 2 * changePerGE, DestComponent.GetSet<AbilitySystemTestAttributeSet>().StackingAttribute1, "Stacking GEs");

            yield return new WaitForSeconds(halfDuration + 0.1f);

            Assert.AreEqual(startingAttributeValue + changePerGE, DestComponent.GetSet<AbilitySystemTestAttributeSet>().StackingAttribute1, "Stacking GEs");

            yield return new WaitForSeconds(duration - 0.2f);

            Assert.AreEqual(startingAttributeValue + changePerGE, DestComponent.GetSet<AbilitySystemTestAttributeSet>().StackingAttribute1, "Stacking GEs");

            yield return new WaitForSeconds(0.2f);

            Assert.AreEqual(startingAttributeValue, DestComponent.GetSet<AbilitySystemTestAttributeSet>().StackingAttribute1, "Stacking GEs");
        }

        private void AddModifier(GameplayEffect effect, FieldInfo fieldInfo, GameplayModOp op, float value)
        {
            GameplayModifierInfo modifier = new();
            modifier.ModifierOp = op;
            modifier.ModifierMagnitude = new GameplayEffectModifierMagnitude(new ScalableFloat(value));
            modifier.Attribute.SetField(fieldInfo);
            effect.Modifiers.Add(modifier);
        }
    }
}

