using System.Collections.Generic;
using System.Linq;
using GameplayTags;
using System;

namespace GameplayAbilities
{
	public enum GameplayEffectGrantedAbilityRemovePolicy
	{
		CancelAbilityImmediately,
		RemoveAbilityOnEnd,
		DoNothing
	}

	public class GameplayAbilitySpecDef : IEquatable<GameplayAbilitySpecDef>
	{
		public GameplayAbility Ability;
		public ScalableFloat LevelScalableFloat = new(1);
		public GameplayEffectGrantedAbilityRemovePolicy RemovePolicy;
		public WeakReference<UnityEngine.Object> SourceObject;
		public Dictionary<GameplayTag, float> SetByCallerTagMagnitudes;
		public GameplayAbilitySpecHandle AssignedHandle;

		public bool Equals(GameplayAbilitySpecDef other)
		{
			if (other is null)
			{
				return false;
			}

			return Ability == other.Ability &&
				LevelScalableFloat == other.LevelScalableFloat &&
				RemovePolicy == other.RemovePolicy;
		}

		public static bool operator ==(GameplayAbilitySpecDef lhs, GameplayAbilitySpecDef rhs)
		{
			if (lhs is null)
			{
				return rhs is null;
			}

			return lhs.Equals(rhs);
		}

		public static bool operator !=(GameplayAbilitySpecDef lhs, GameplayAbilitySpecDef rhs)
		{
			return !(lhs == rhs);
		}

		public override bool Equals(object obj)
		{
			return Equals(obj as GameplayAbilitySpecDef);
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(Ability, LevelScalableFloat, RemovePolicy);
		}
	}

	public class GameplayAbilitySpec
	{
		public GameplayAbilitySpecHandle Handle = new();
		public GameplayAbility Ability;
		public int Level;
		public List<GameplayAbility> ReplicatedInstances = new();
		public List<GameplayAbility> NonReplicatedInstances = new();
		public List<GameplayAbility> AbilityInstances
		{
			get
			{
				return ReplicatedInstances.Concat(NonReplicatedInstances).ToList();
			}
		}
		public WeakReference<UnityEngine.Object> SourceObject;
		public int ActiveCount;
		public bool RemoveAfterActivation;
		public bool PendingRemove;
		public bool ActivateOnce;
		public ActiveGameplayEffectHandle GameplayEffectHandle = new();
		[Obsolete("Use DynamicSpecSourceTags which better represents what this variable does")]
		public GameplayTagContainer DynamicAbilityTags = new();
		public GameplayTagContainer DynamicSpecSourceTags => DynamicAbilityTags;
		public Dictionary<GameplayTag, float> SetByCallerTagMagnitudes = new();

		public GameplayAbilitySpec()
		{
			Ability = null;
			Level = 1;
			SourceObject = null;
			ActiveCount = 0;
			RemoveAfterActivation = false;
		}

		public GameplayAbilitySpec(GameplayAbility ability, int level = 1, UnityEngine.Object sourceObject = null)
		{
			Ability = ability;
			Level = level;
			SourceObject = new WeakReference<UnityEngine.Object>(sourceObject);

			Handle.GenerateNewHandle();
		}

		public GameplayAbility GetPrimaryInstance()
		{
			if (Ability.InstancingPolicy == GameplayAbilityInstancingPolicy.InstancedPerActor)
			{
				if (ReplicatedInstances.Count > 0)
				{
					return ReplicatedInstances[0];
				}
				if (NonReplicatedInstances.Count > 0)
				{
					return NonReplicatedInstances[0];
				}
			}
			return null;
		}

		public bool IsActive => Ability != null && ActiveCount > 0;
	}
}