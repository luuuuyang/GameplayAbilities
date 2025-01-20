using System.Collections.Generic;

namespace GameplayAbilities
{
	public class AbilitiesGameplayEffectComponent : GameplayEffectComponent
	{
		public List<GameplayAbilitySpecConfig> GrantedAbilityConfigs;

		public virtual void GrantAbilities(ActiveGameplayEffectHandle active_ge_handle)
		{
			var Asc = active_ge_handle.OwningAbilitySystemComponent;
		}

		public void RemoveAbilities(ActiveGameplayEffectHandle active_ge_handle)
		{

		}
	}
}