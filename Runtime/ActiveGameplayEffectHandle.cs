using System.Collections.Generic;

namespace GameplayAbilities
{
	public static class GlobalActiveGameplayEffectHandles
	{
		public static Dictionary<ActiveGameplayEffectHandle, AbilitySystemComponent> Map = new();
	}

	public class ActiveGameplayEffectHandle
	{
		private int Handle;
		private bool PassedFiltersAndWasExecuted;

		private static int GHandleID = 0;

		public ActiveGameplayEffectHandle()
		{
			Handle = -1;
			PassedFiltersAndWasExecuted = false;
		}

		public ActiveGameplayEffectHandle(int handle)
		{
			Handle = handle;
			PassedFiltersAndWasExecuted = true;
		}

		public bool IsValid()
		{
			return Handle != -1;
		}

		public bool WasSuccessfullyApplied()
		{
			return PassedFiltersAndWasExecuted;
		}

		public static void ResetGlobalHandleMap()
		{
			GlobalActiveGameplayEffectHandles.Map.Clear();
		}

		public static ActiveGameplayEffectHandle GenerateNewHandle(AbilitySystemComponent owningComponent)
		{
			ActiveGameplayEffectHandle newHandle = new(GHandleID++);

			GlobalActiveGameplayEffectHandles.Map.Add(newHandle, owningComponent);

			return newHandle;
		}

		public AbilitySystemComponent OwningAbilitySystemComponent
		{
			get
			{
				if (GlobalActiveGameplayEffectHandles.Map.TryGetValue(this, out var abilitySystemComponent))
				{
					return abilitySystemComponent;
				}

				return null;
			}
		}

		public void RemoveFromGlobalMap()
		{
			GlobalActiveGameplayEffectHandles.Map.Remove(this);
		}

		public static bool operator ==(ActiveGameplayEffectHandle a, ActiveGameplayEffectHandle b)
		{
			if (a is null && b is null)
			{
				return true;
			}

			if (a is null || b is null)
			{
				return false;
			}
			
			return a.Handle == b.Handle;
		}

		public static bool operator !=(ActiveGameplayEffectHandle a, ActiveGameplayEffectHandle b)
		{
			return !(a == b);
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

		public override string ToString()
		{
			return Handle.ToString();
		}
	}
}