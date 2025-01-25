using System.Collections.Generic;
using UnityEngine;

namespace GameplayAbilities
{
	public class RemoveOtherGameplayEffectComponent : GameplayEffectComponent
	{
		public List<GameplayEffectQuery> RemoveGameplayEffectsQueries = new();

		public override void OnGameplayEffectApplied(ActiveGameplayEffectsContainer activeGEContainer, GameplayEffectSpec GESpec)
		{
			GameplayEffectQuery findOwnerQuery = new();
			findOwnerQuery.EffectDefinition = Owner ?? null;

			List<ActiveGameplayEffectHandle> activeGEHandles = activeGEContainer.GetActiveEffects(findOwnerQuery);

			int removeAllStacks = -1;
			foreach (GameplayEffectQuery removeQuery in RemoveGameplayEffectsQueries)
			{
				if (!removeQuery.IsEmpty())
				{
					if (activeGEHandles.IsEmpty())
					{
						activeGEContainer.RemoveActiveEffects(removeQuery, removeAllStacks);
					}
					else
					{
						GameplayEffectQuery mutableRemoveQuery = removeQuery;
						mutableRemoveQuery.IgnoreHandles = activeGEHandles;

						activeGEContainer.RemoveActiveEffects(mutableRemoveQuery, removeAllStacks);
					}
				}
			}
		}
	}
}