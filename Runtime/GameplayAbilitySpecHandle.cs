namespace GameplayAbilities
{
	public struct GameplayAbilitySpecHandle
	{
		private int Handle;

		private static int GHandle = 1;

		public readonly bool IsValid()
		{
			return Handle != 0;
		}

		public void GenerateNewHandle()
		{
			Handle = GHandle++;
		}

		public static bool operator ==(GameplayAbilitySpecHandle a, GameplayAbilitySpecHandle b)
		{
			return a.Handle == b.Handle;
		}

		public static bool operator !=(GameplayAbilitySpecHandle a, GameplayAbilitySpecHandle b)
		{
			return a.Handle != b.Handle;
		}

		public override bool Equals(object obj)
		{
			if (obj is GameplayAbilitySpecHandle)
			{
				return this == (GameplayAbilitySpecHandle)obj;
			}
			return false;
		}

		public override int GetHashCode()
		{
			return Handle.GetHashCode();
		}

		public override string ToString()
		{
			return IsValid() ? Handle.ToString() : "Invalid";
		}
	}
}