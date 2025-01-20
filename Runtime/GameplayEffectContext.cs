using GameplayTags;
using UnityEngine;

namespace GameplayAbilities
{
	public class GameplayEffectContext
	{
		public GameObject Instigator;
		public GameObject EffectCauser;
		public Object SourceObject;
		public AbilitySystemComponent InstigatorAbilitySystemComponent;
		public GameplayAbility AbilityCdo;
		public GameplayAbility AbilityInstanceNotReplicated;
		public int AbilityLevel;

		public void AddInstigator(GameObject instigator, GameObject effectCauser)
		{
			Instigator = instigator;
			SetEffectCauser(effectCauser);

			InstigatorAbilitySystemComponent = null;
			InstigatorAbilitySystemComponent = AbilitySystemGlobals.GetAbilitySystemComponentFromActor(Instigator);
		}

		public void SetEffectCauser(GameObject effectCauser)
		{
			this.EffectCauser = effectCauser;
		}

		public void SetAbility(GameplayAbility ability)
		{
			this.AbilityCdo = ability;
			this.AbilityInstanceNotReplicated = ability;
			this.AbilityLevel = ability.GetAbilityLevel();
		}

		public void AddSourceObject(Object source_object)
		{
			this.SourceObject = source_object;
		}

		public void GetOwnedGameplayTags(GameplayTagContainer actor_tag_container, GameplayTagContainer spec_tag_container)
		{
			if (InstigatorAbilitySystemComponent != null)
			{
				InstigatorAbilitySystemComponent.GetOwnedGameplayTags(actor_tag_container);
			}
		}
	}
}