using UnityEngine;

namespace GameplayAbilities
{
	[CreateAssetMenu(fileName = "GameplayEffectComponent", menuName = "GameplayAbilities/GameplayEffectComponent")]
	public class GameplayEffectComponent : ScriptableObject
	{
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
	}
}