using GameplayTags;
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace GameplayAbilities
{
	[LabelText("Target Tag Reqs (While GE is Active)")]
	public class TargetTagRequirementsGameplayEffectComponent : GameplayEffectComponent
	{
		public GameplayTagRequirements ApplicationTagRequirements = new();
		public GameplayTagRequirements OngoingTagRequirements = new();
		public GameplayTagRequirements RemovalTagRequirements = new();

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

				List<Tuple<GameplayTag, UnityAction<GameplayTag, int>>> allBoundEvents = new();
				foreach (GameplayTag tag in gameplayTagsToBind)
				{
					OnGameplayEffectTagCountChanged onTagEvent = ASC.RegisterGameplayTagEvent(tag, GameplayTagEventType.NewOrRemoved);
					void call(GameplayTag gameplayTag, int newCount)
					{
						OnTagChanged(gameplayTag, newCount, activeGEHandle);
					}
					onTagEvent.AddListener(call);
					allBoundEvents.Add(new(tag, call));
				}

				eventSet.OnEffectRemoved += (GERemovalInfo) => OnGameplayEffectRemoved(GERemovalInfo, ASC, allBoundEvents);
			}
			else
			{
				Debug.LogError($"TargetTagRequirementsGameplayEffectComponent.OnGameplayEffectAdded called with ActiveGE: {activeGE} which had an invalid ActiveGameplayEffectHandle");
			}

			GameplayTagContainer tagContainer = new();
			ASC.GetOwnedGameplayTags(tagContainer);

			return OngoingTagRequirements.RequirementsMet(tagContainer);
		}

		public virtual void OnGameplayEffectRemoved(in GameplayEffectRemovalInfo GERemovalInfo, AbilitySystemComponent ASC, List<Tuple<GameplayTag, UnityAction<GameplayTag, int>>> allBoundEvents)
		{
			foreach (Tuple<GameplayTag, UnityAction<GameplayTag, int>> pair in allBoundEvents)
			{
				bool success = ASC.UnregisterGameplayTagEvent(pair.Item2, pair.Item1, GameplayTagEventType.NewOrRemoved);
				if (!success)
				{
					Debug.LogError($"{this} tried to unregister GameplayTagEvent '{pair.Item1}' on GameplayEffect '{Owner}' but failed.");
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
					owner.SetActiveGameplayEffectInhibit(activeGEHandle, !ongoingRequirementsMet);
				}
			}
		}
	}
}