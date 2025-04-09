using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GameplayAbilities
{
	[Serializable]
	public class GameplayAbilitySpecConfig
	{
		public GameplayAbility Ability;

		[LabelText("Level")]
		public ScalableFloat LevelScaleFloat = new ScalableFloat(1);
		public GameplayEffectGrantedAbilityRemovePolicy RemovalPolicy = GameplayEffectGrantedAbilityRemovePolicy.CancelAbilityImmediately;

		public static bool operator ==(GameplayAbilitySpecConfig a, GameplayAbilitySpecConfig b)
		{
			return a.Ability == b.Ability && a.LevelScaleFloat == b.LevelScaleFloat && a.RemovalPolicy == b.RemovalPolicy;
		}

		public static bool operator !=(GameplayAbilitySpecConfig a, GameplayAbilitySpecConfig b)
		{
			return !(a == b);
		}
	}

	[LabelText("Grant Abilities While Active")]
	public class AbilitiesGameplayEffectComponent : GameplayEffectComponent
	{
		[LabelText("Grant Ability Configs")]
		[SerializeField]
		protected List<GameplayAbilitySpecConfig> GrantedAbilityConfigs = new();

		public void AddGrantedAbilityConfig(in GameplayAbilitySpecConfig abilityConfig)
		{
			GrantedAbilityConfigs.Add(abilityConfig);
		}

		protected virtual void OnInhibitionChanged(ActiveGameplayEffectHandle activeGEHandle, bool isInhibited)
		{
			if (isInhibited)
			{
				RemoveAbilities(activeGEHandle);
			}
			else
			{
				GrantAbilities(activeGEHandle);
			}
		}

		protected virtual void GrantAbilities(ActiveGameplayEffectHandle activeGEHandle)
		{
			var ASC = activeGEHandle.OwningAbilitySystemComponent;

			if (ASC == null)
			{
				Debug.LogError("ASC is null");
				return;
			}

			if (ASC.SuppressGrantAbility)
			{
				Debug.LogError("ASC is suppressing grant ability");
				return;
			}

			var activeGE = ASC.GetActiveGameplayEffect(activeGEHandle);
			if (activeGE == null)
			{
				Debug.LogError("ActiveGE is null");
				return;
			}
			var activeGESpec = activeGE.Spec;

			var allAbilities = ASC.GetActivatableAbilities();
			foreach (var abilityConfig in GrantedAbilityConfigs)
			{
				GameplayAbility abilityCDO = abilityConfig.Ability;
				if (abilityCDO == null)
				{
					continue;
				}

				bool alreadyGrantedAbility = allAbilities.Any(spec => spec.Ability == abilityCDO && spec.GameplayEffectHandle == activeGEHandle);
				if (alreadyGrantedAbility)
				{
					continue;
				}

				int level = (int)abilityConfig.LevelScaleFloat.GetValueAtLevel(activeGESpec.Level);
				GameplayAbilitySpec abilitySpec = new(abilityCDO, level, activeGESpec.EffectContext.SourceObject);
				abilitySpec.SetByCallerTagMagnitudes = activeGESpec.SetByCallerTagMagnitudes;
				abilitySpec.GameplayEffectHandle = activeGEHandle;

				ASC.GiveAbility(abilitySpec);
			}
		}

		public void RemoveAbilities(ActiveGameplayEffectHandle activeGEHandle)
		{
			var ASC = activeGEHandle.OwningAbilitySystemComponent;

			if (ASC == null)
			{
				Debug.LogError("ASC is null");
				return;
			}

			List<GameplayAbilitySpec> grantedAbilities = ASC.FindAbilitySpecsFromGEHandle(activeGEHandle);
			foreach (var abilityConfig in GrantedAbilityConfigs)
			{
				GameplayAbility abilityCDO = abilityConfig.Ability;
				if (abilityCDO == null)
				{
					continue;
				}

				GameplayAbilitySpec abilitySpecDef = grantedAbilities.Find(ability => ability.Ability == abilityCDO);
				if (abilitySpecDef == null)
				{
					continue;
				}

				switch (abilityConfig.RemovalPolicy)
				{
					case GameplayEffectGrantedAbilityRemovePolicy.CancelAbilityImmediately:
						ASC.ClearAbility(abilitySpecDef.Handle);
						break;
					case GameplayEffectGrantedAbilityRemovePolicy.RemoveAbilityOnEnd:
						ASC.SetRemoveAbilityOnEnd(abilitySpecDef.Handle);
						break;
					default:
						break;
				}
			}
		}

        public override bool OnActiveGameplayEffectAdded(ActiveGameplayEffectsContainer activeGEContainer, ActiveGameplayEffect activeGE)
        {
			activeGE.EventSet.OnEffectRemoved += OnActiveGameplayEffectRemoved;
			activeGE.EventSet.OnInhibitionChanged += OnInhibitionChanged;
            return true;
        }

		private void OnActiveGameplayEffectRemoved(GameplayEffectRemovalInfo removalInfo)
		{
			var activeGE = removalInfo.ActiveEffect;
			if (activeGE == null)
			{
				Debug.LogError("ActiveGE is null");
				return;
			}

			RemoveAbilities(activeGE.Handle);
		}
    }
}