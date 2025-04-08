using System;
using UnityEngine;

namespace GameplayAbilities
{
	public abstract class GameplayEffectComponent : ScriptableObject
	{
		[HideInInspector]
		public GameplayEffect Owner;

		public virtual bool CanGameplayEffectApply(in ActiveGameplayEffectsContainer activeGEContainer, in GameplayEffectSpec GESpec)
		{
			return true;
		}

		public virtual bool OnActiveGameplayEffectAdded(ActiveGameplayEffectsContainer activeGEContainer, ActiveGameplayEffect activeGE)
		{
			return true;
		}

		public virtual void OnGameplayEffectExecuted(ActiveGameplayEffectsContainer activeGEContainer, GameplayEffectSpec GESpec)
		{
		}

		public virtual void OnGameplayEffectApplied(ActiveGameplayEffectsContainer activeGEContainer, GameplayEffectSpec GESpec)
		{
		}

		public virtual void OnGameplayEffectChanged()
		{
		}

		public static GameplayEffectComponent CreateInstance(Type type, GameplayEffect owner)
        {
            GameplayEffectComponent component = CreateInstance(type) as GameplayEffectComponent;
            component.Owner = owner;
            component.name = type.Name;
            return component;
        }
	}
}