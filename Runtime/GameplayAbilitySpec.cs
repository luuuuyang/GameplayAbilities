using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using GameplayTags;
using System;

namespace GameplayAbilities
{
	public class GameplayAbilitySpecDef
	{
		public GameplayAbility Ability;
		public ScalableFloat LevelScalableFloat;
		public GameplayEffectGrantedAbilityRemovePolicy RemovePolicy;
		public object SourceObject;
		public Dictionary<GameplayTag, float> SetByCallerTagMagnitudes;
		public GameplayAbilitySpecHandle AssignedHandle;

		public static bool operator ==(GameplayAbilitySpecDef a, GameplayAbilitySpecDef b)
		{
			return a.Ability == b.Ability &&
				a.LevelScalableFloat == b.LevelScalableFloat &&
				a.RemovePolicy == b.RemovePolicy;
		}

		public static bool operator !=(GameplayAbilitySpecDef a, GameplayAbilitySpecDef b)
		{
			return !(a == b);
		}

		public override bool Equals(object obj)
		{
			if (obj == null)
			{
				return false;
			}
			return obj is GameplayAbilitySpecDef def && this == def;
		}
	}

	public class GameplayAbilitySpec
	{
		public GameplayAbilitySpecHandle Handle;
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
			set { _AbilityInstances = value; }
		}
		private List<GameplayAbility> _AbilityInstances;

		public object SourceObject;
		// A count of the number of times this ability has been activated minus the number of times it has been ended. For instanced abilities this will be the number of currently active instances. Can't replicate until prediction accurately handles this.
		public int ActiveCount;
		public bool RemoveAfterActivation;
		public bool ActivateOnce;
		public ActiveGameplayEffectHandle GameplayEffectHandle = new(-1);

		public GameplayTagContainer DynamicAbilityTags = new();
		public GameplayTagContainer DynamicSpecSourceTags => DynamicAbilityTags;

		// Passed on SetByCaller magnitudes if this ability was granted by a GE
		public Dictionary<GameplayTag, float> SetByCallerTagMagnitudes = new();

		public GameplayAbilitySpec()
		{
			Ability = null;
			Level = 1;
			SourceObject = null;
			ActiveCount = 0;
			RemoveAfterActivation = false;
		}

		public GameplayAbilitySpec(GameplayAbility ability, int level = 1, GameObject sourceObject = null)
		{
			Ability = ability;
			Level = level;
			SourceObject = sourceObject;

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