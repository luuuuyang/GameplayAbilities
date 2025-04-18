using System;
using GameplayTags;
using UnityEngine;

namespace GameplayAbilities
{
	public enum GameplayAbilityInstancingPolicy
	{
		[Obsolete("Use InstancedPerActor as the default to avoid confusing corner cases")]
		NonInstanced,
		InstancedPerActor,
		InstancedPerExecution
	}

	public enum GameplayAbilityReplicationPolicy
	{
		ReplicateNo,
		ReplicateYes
	}

	public enum GameplayAbilityTriggerSource
	{
		GameplayEvent,
		OwnedTagAdded,
		OwnedTagPresent
	}

	public record GameplayAbilityActorInfo
	{
		public WeakReference<GameObject> OwnerActor = new(null);
		public WeakReference<GameObject> AvatarActor = new(null);
		public WeakReference<AbilitySystemComponent> AbilitySystemComponent = new(null);

		public virtual void InitFromActor(GameObject ownerActor, GameObject avatarActor, AbilitySystemComponent abilitySystemComponent)
		{
			OwnerActor.SetTarget(ownerActor);
			AvatarActor.SetTarget(avatarActor);
			AbilitySystemComponent.SetTarget(abilitySystemComponent);
		}
	}

	public record GameplayEventData
	{
		public GameplayTag EventTag;
		public GameObject Instigator;
		public GameObject Target;
		public GameplayEffectContextHandle ContextHandle;
		public GameplayTagContainer InstigatorTags;
		public GameplayTagContainer TargetTags;
		public float EventMagnitude;
		public GameplayAbilityTargetDataHandle TargetData;
	}

	public delegate void GameplayEventMulticastDelegate(in GameplayEventData data);
	public delegate void GameplayEventTagMulticastDelegate(GameplayTag eventTag, in GameplayEventData data);

	public record AbilityEndedData
	{
		public GameplayAbility AbilityThatEnded;
		public GameplayAbilitySpecHandle AbilitySpecHandle;
		public bool WasCancelled;

		public AbilityEndedData(GameplayAbility ability, GameplayAbilitySpecHandle handle, bool wasCancelled)
		{
			AbilityThatEnded = ability;
			AbilitySpecHandle = handle;
			WasCancelled = wasCancelled;
		}
	}

	public delegate void GameplayAbilityEndedDelegate(in AbilityEndedData data);
	public delegate void GenericAbilityDelegate(GameplayAbility ability);
}
