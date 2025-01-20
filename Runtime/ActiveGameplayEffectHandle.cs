using System.Collections.Generic;

namespace GameplayAbilities
{
	public static class GlobalActiveGameplayEffectHandles
	{
		public static Dictionary<ActiveGameplayEffectHandle, AbilitySystemComponent> Map = new Dictionary<ActiveGameplayEffectHandle, AbilitySystemComponent>();
	}

	public struct ActiveGameplayEffectHandle
	{
		private int Handle;

		private static int GHandleID = 0;

		public ActiveGameplayEffectHandle(int handle)
		{
			this.Handle = handle;
		}

		public bool IsValid()
		{
			return Handle != GameplayEffectConstants.IndexNone;
		}

		public static void ResetGlobalHandleMap()
		{
			GlobalActiveGameplayEffectHandles.Map.Clear();
		}

		public static ActiveGameplayEffectHandle GenerateNewHandle(AbilitySystemComponent owning_component)
		{
			var new_handle = new ActiveGameplayEffectHandle(GHandleID++);

			GlobalActiveGameplayEffectHandles.Map[new_handle] = owning_component;
			return new_handle;
		}

        public AbilitySystemComponent OwningAbilitySystemComponent => GlobalActiveGameplayEffectHandles.Map[this];

        public void RemoveFromGlobalMap()
		{
			GlobalActiveGameplayEffectHandles.Map.Remove(this);
		}

		public static bool operator ==(ActiveGameplayEffectHandle a, ActiveGameplayEffectHandle b)
		{
			return a.Handle == b.Handle;
		}

		public static bool operator !=(ActiveGameplayEffectHandle a, ActiveGameplayEffectHandle b)
		{
			return a.Handle != b.Handle;
		}
	}
}