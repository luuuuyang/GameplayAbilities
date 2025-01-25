using UnityEngine;

#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif

namespace GameplayAbilities
{
	public class AssetTagsGameplayEffectComponent : GameplayEffectComponent
	{
		public InheritedTagContainer ConfiguredAssetTags => InheritableAssetTags;

#if ODIN_INSPECTOR
		[LabelText("Asset Tags")]
#endif
		[SerializeField]
		private InheritedTagContainer InheritableAssetTags;

		public override void OnGameplayEffectChanged()
		{
			base.OnGameplayEffectChanged();
			ApplyAssetTagChanges();
		}

		public void SetAndApplyAssetTagChanges(in InheritedTagContainer tagContainerMods)
		{
			InheritableAssetTags = tagContainerMods;

			ApplyAssetTagChanges();
		}

		private void ApplyAssetTagChanges()
		{
			InheritableAssetTags.ApplyTo(Owner.CachedAssetTags);
		}
	}
}