using GameplayTags;

namespace GameplayAbilities
{
	public class TagContainerAggregator
	{
		private GameplayTagContainer CapturedActorTags = new();
		private GameplayTagContainer CapturedSpecTags = new();
		private GameplayTagContainer CachedAggregator = new();
		private bool CacheIsValid;

		public void CopyFrom(TagContainerAggregator other)
		{
			CapturedActorTags.CopyFrom(other.CapturedActorTags);
			CapturedSpecTags.CopyFrom(other.CapturedSpecTags);
			CachedAggregator.CopyFrom(other.CachedAggregator);
			CacheIsValid = other.CacheIsValid;
		}

		public GameplayTagContainer ActorTags
		{
			get
			{
                CacheIsValid = false;
                return CapturedActorTags;
            }
        }

        public GameplayTagContainer SpecTags
        {
            get
            {
                CacheIsValid = false;
                return CapturedSpecTags;
            }
        }

        public GameplayTagContainer AggregatedTags
        {
            get
            {
                if (!CacheIsValid)
                {
                    CacheIsValid = true;
                    CachedAggregator.Reset(CapturedActorTags.Num + CapturedSpecTags.Num);
                    CachedAggregator.AppendTags(CapturedActorTags);
                    CachedAggregator.AppendTags(CapturedSpecTags);
                }
                return CachedAggregator;
            }
        }
    }
}
