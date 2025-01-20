using UnityEngine;

namespace GameplayAbilities
{
	//# The actor that owns the abilities, shouldn't be null

	public class GameplayAbilityActorInfo
	{
		public GameObject OwnerActor;
		//# The physical representation of the owner, used for targeting and animation. This will often be null!
		public GameObject AvatarActor;
		//# Ability System component associated with the owner actor, shouldn't be null
		public AbilitySystemComponent AbilitySystemComponent;

		public virtual void InitFromActor(GameObject ownerActor, GameObject avatarActor, AbilitySystemComponent abilitySystemComponent)
		{
			OwnerActor = ownerActor;
			AvatarActor = avatarActor;
			AbilitySystemComponent = abilitySystemComponent;
		}
	}
}
