namespace GameplayAbilities
{
	// Describes what happens when a GameplayEffect, that is granting an active ability, is removed from its owner
	public enum GameplayEffectGrantedAbilityRemovePolicy { CancelAbilityImmediately, RemoveAbilityOnEnd, DoNothing }

	public struct GameplayAbilitySpecConfig
	{

		public GameplayEffectGrantedAbilityRemovePolicy RemovalPolicy;


	}
}