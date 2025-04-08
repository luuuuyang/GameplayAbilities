using UnityEngine;

#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif

namespace GameplayAbilities
{
	public class AssetTagsGameplayEffectComponent : GameplayEffectComponent
	{
		public InheritedTagContainer ConfiguredAssetTags => InheritableAssetTags;

		[LabelText("Asset Tags")]
		[SerializeField]
		private InheritedTagContainer InheritableAssetTags = new();

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