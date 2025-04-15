using UnityEngine;
using Sirenix.OdinInspector;

namespace GameplayAbilities
{
	[LabelText("Asset Tags (on Gameplay Effect)")]
	public class AssetTagsGameplayEffectComponent : GameplayEffectComponent
	{
		public InheritedTagContainer ConfiguredAssetTags => InheritableAssetTags;

		[LabelText("Asset Tags")]
		[SerializeField]
		private InheritedTagContainer InheritableAssetTags = new();

		public override void OnGameplayEffectChanged()
		{
			base.OnGameplayEffectChanged();
			SetAndApplyAssetTagChanges(InheritableAssetTags);
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