using GameplayTags;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameplayAbilities
{
	[CreateAssetMenu(fileName = "TargetTagRequirementsGameplayEffectComponent", menuName = "GameplayAbilities/GameplayEffectComponents/TargetTagRequirementsGameplayEffectComponent")]
	public class TargetTagRequirementsGameplayEffectComponent : GameplayEffectComponent
	{
		public GameplayTagRequirements ApplicationTagRequirements;
		// Once applied, these tags determine whether the GameplayEffect is on or off. A GameplayEffect can be off and still be applied. If a GameplayEffect is off due to failing the Ongoing Tag Requirements, but the requirements are then met, the GameplayEffect will turn on again and reapply its modifiers. This only works for Duration and Infinite GameplayEffects.
		public GameplayTagRequirements OngoingTagRequirements;
		// Tag requirements that if met will remove this effect. Also prevents effect application.
		public GameplayTagRequirements RemovalTagRequirements;

		public override bool CanGameplayEffectApply(in ActiveGameplayEffectsContainer activeGEContainer, in GameplayEffectSpec spec)
		{
			GameplayTagContainer tags = new();
			activeGEContainer.Owner.GetOwnedGameplayTags(tags);

			if (ApplicationTagRequirements.RequirementsMet(tags) == false)
			{
				return false;
			}

			if (!RemovalTagRequirements.IsEmpty() && OngoingTagRequirements.RequirementsMet(tags) == true)
			{
				return false;
			}

			return true;
		}

		public override bool OnActiveGameplayEffectAdded(ActiveGameplayEffectsContainer GEContainer, ActiveGameplayEffect activeGE)
		{
			AbilitySystemComponent ASC = GEContainer.Owner;
			if (ASC == null)
			{
				return false;
			}

			GameplayTagContainer tags = new();
			ActiveGameplayEffectHandle activeGEHandle = activeGE.Handle;
			ActiveGameplayEffectEvents eventSet = ASC.GetActiveEffectEventSet(activeGEHandle);
			if (eventSet != null)
			{
				List<GameplayTag> gameplayTagsToBind = new();
				gameplayTagsToBind.AppendUnique(OngoingTagRequirements.IgnoreTags.GameplayTags);
				gameplayTagsToBind.AppendUnique(OngoingTagRequirements.RequireTags.GameplayTags);
				gameplayTagsToBind.AppendUnique(OngoingTagRequirements.TagQuery.TagDictionary);
				gameplayTagsToBind.AppendUnique(RemovalTagRequirements.IgnoreTags.GameplayTags);
				gameplayTagsToBind.AppendUnique(RemovalTagRequirements.RequireTags.GameplayTags);
				gameplayTagsToBind.AppendUnique(OngoingTagRequirements.TagQuery.TagDictionary);

				List<Tuple<GameplayTag, OnGameplayEffectTagCountChanged>> allBoundEvents = new();
				foreach (GameplayTag tag in gameplayTagsToBind)
				{
					OnGameplayEffectTagCountChanged onTagEvent = ASC.RegisterGameplayTagEvent(tag, GameplayTagEventType.NewOrRemoved);
					OnGameplayEffectTagCountChanged handle = (tag, newCount) =>
					{
						OnTagChanged(tag, newCount, activeGEHandle);
					};
					allBoundEvents.Add(new(tag, handle));
				}

				// eventSet.OnEffectRemoved += OnGameplayEffectRemoved;
			}
			else
			{
				Debug.LogError($"TargetTagRequirementsGameplayEffectComponent.OnGameplayEffectAdded called with ActiveGE: {activeGE} which had an invalid ActiveGameplayEffectHandle");
			}

			GameplayTagContainer tagContainer = new();
			ASC.GetOwnedGameplayTags(tags);

			return OngoingTagRequirements.RequirementsMet(tagContainer);
		}

		public virtual void OnGameplayEffectRemoved(in GameplayEffectRemovalInfo GERemovalInfo, AbilitySystemComponent ASC, List<Tuple<GameplayTag, OnGameplayEffectTagCountChanged>> allBoundEvents)
		{
			foreach (var tag in allBoundEvents)
			{
				bool success = ASC.UnregisterGameplayTagEvent(tag.Item2, tag.Item1, GameplayTagEventType.NewOrRemoved);
				if (!success)
				{
					Debug.LogError($"{this} tried to unregister GameplayTagEvent '{tag.Item1}' on GameplayEffect '{Owner}' but failed.");
				}
			}
		}

		public void OnTagChanged(in GameplayTag tag, int newCount, ActiveGameplayEffectHandle activeGEHandle)
		{
			AbilitySystemComponent owner = activeGEHandle.OwningAbilitySystemComponent;
			if (owner == null)
			{
				return;
			}

			ActiveGameplayEffect activeGE = owner.GetActiveGameplayEffect(activeGEHandle);
			if (activeGE != null && !activeGE.IsPendingRemove)
			{
				GameplayTagContainer ownedTags = new();
				owner.GetOwnedGameplayTags(ownedTags);

				bool removalRequirementsMet = !RemovalTagRequirements.IsEmpty() && RemovalTagRequirements.RequirementsMet(ownedTags);
				if (removalRequirementsMet)
				{
					owner.RemoveActiveGameplayEffect(activeGEHandle);
				}
				else
				{
					bool ongoingRequirementsMet = !OngoingTagRequirements.IsEmpty() && OngoingTagRequirements.RequirementsMet(ownedTags);
					owner.InhibitActiveGameplayEffect(activeGEHandle, !ongoingRequirementsMet);
				}
			}
		}
	}
}