using GameplayTags;
using UnityEngine;

namespace GameplayAbilities
{
	public struct GameplayEffectContextHandle
	{
		public GameplayEffectContext Data;

		public GameplayEffectContextHandle(GameplayEffectContext data)
		{
			Data = data;
		}

		public bool IsValid()
		{
			return Data is not null;
		}

        public AbilitySystemComponent InstigatorAbilitySystemComponent
        {
            get
            {
                if (IsValid())
                {
                    return Data.InstigatorAbilitySystemComponent;
                }
                return null;
            }
        }

		public object SourceObject
		{
			get
			{
				if (IsValid())
				{
					return Data.SourceObject;
				}
				return null;
			}
		}

        public void AddInstigator(GameObject instigator, GameObject effect_causer)
		{
			if (IsValid())
			{
				Data.AddInstigator(instigator, effect_causer);
			}
		}

		public void SetAbility(GameplayAbility ability)
		{
			if (IsValid())
			{
				Data.SetAbility(ability);
			}
		}

		public void AddSourceObject(Object sourceObject)
		{
			if (IsValid())
			{
				Data.AddSourceObject(sourceObject);
			}
		}

		public GameplayEffectContextHandle Duplicate()
		{
			GameplayEffectContextHandle newContext = this;

			return newContext;
		}

		public static bool operator ==(GameplayEffectContextHandle a, GameplayEffectContextHandle b)
		{
			return a.Data == b.Data;
		}

		public static bool operator !=(GameplayEffectContextHandle a, GameplayEffectContextHandle b)
		{
			return !(a == b);
		}

		public void GetOwnedGameplayTags(GameplayTagContainer actor_tag_container, GameplayTagContainer spec_tag_container)
		{
			if (IsValid())
			{
				Data.GetOwnedGameplayTags(actor_tag_container, spec_tag_container);
			}
		}
	}
}