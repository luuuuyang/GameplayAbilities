using UnityEngine;

namespace GameplayAbilities
{
    public enum GameplayAbilityInstancingPolicy
	{
		NonInstanced,
		InstancedPerActor,
		InstancedPerExecution
	}
    
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

    public struct GameplayEventData
    {

    }

    public struct AbilityEndedData
	{
		public GameplayAbility AbilityThatEnded;
		public GameplayAbilitySpecHandle AbilitySpecHandle;
		public bool WasCancelled;

		public AbilityEndedData(GameplayAbility ability, GameplayAbilitySpecHandle handle, bool was_cancelled)
		{
			AbilityThatEnded = ability;
			AbilitySpecHandle = handle;
			WasCancelled = was_cancelled;
		}
	}

    public delegate void GameplayAbilityEndedDelegate(AbilityEndedData data);

}
