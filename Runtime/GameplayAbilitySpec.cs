using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using GameplayTags;

namespace GameplayAbilities
{
	
	public class GameplayAbilitySpec
	{
		public GameplayAbilitySpecHandle Handle;
		public GameplayAbility Ability;
		public int Level;
		public List<GameplayAbility> ReplicatedInstances;
		public List<GameplayAbility> NonReplicatedInstances;
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
		public ActiveGameplayEffectHandle GameplayEffectHandle = new(-1);

		public GameplayTagContainer DynamicAbilityTags;
		public GameplayTagContainer DynamicSpecSourceTags => DynamicAbilityTags;
		public Dictionary<GameplayTag, float> SetByCallerTagMagnitudes;

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