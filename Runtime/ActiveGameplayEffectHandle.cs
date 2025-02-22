using System.Collections.Generic;

namespace GameplayAbilities
{
	public static class GlobalActiveGameplayEffectHandles
	{
		public static Dictionary<ActiveGameplayEffectHandle, AbilitySystemComponent> Map = new();
	}

	public struct ActiveGameplayEffectHandle
	{
		private int Handle;

		private static int GHandleID = 0;

		public ActiveGameplayEffectHandle(int handle = -1)
		{
			Handle = handle;
		}

		public bool IsValid()
		{
			return Handle != -1;
		}

		public static void ResetGlobalHandleMap()
		{
			GlobalActiveGameplayEffectHandles.Map.Clear();
		}

		public static ActiveGameplayEffectHandle GenerateNewHandle(AbilitySystemComponent owningComponent)
		{
			ActiveGameplayEffectHandle newHandle = new ActiveGameplayEffectHandle(GHandleID++);

			GlobalActiveGameplayEffectHandles.Map.Add(newHandle, owningComponent);

			return newHandle;
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

		public override int GetHashCode()
		{
			return Handle.GetHashCode();
		}

		public override bool Equals(object obj)
		{
			if (obj is ActiveGameplayEffectHandle other)
			{
				return Handle == other.Handle;
			}

			return false;
		}
	}
}