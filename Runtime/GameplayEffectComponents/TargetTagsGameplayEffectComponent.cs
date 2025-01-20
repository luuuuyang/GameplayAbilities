using System;
using UnityEngine;

namespace GameplayAbilities
{
	[CreateAssetMenu(fileName = "TargetTagsGameplayEffectComponent", menuName = "GameplayAbilities/GameplayEffectComponents/TargetTagsGameplayEffectComponent")]
	public class TargetTagsGameplayEffectComponent : GameplayEffectComponent
	{
		public InheritedTagContainer ConfiguredTargetTagsChanges => InheritableGrantedTagsContainer;

		[SerializeField]
		private InheritedTagContainer InheritableGrantedTagsContainer;

		protected void OnValidate()
		{
			SetAndApplyTargetTagChanges(InheritableGrantedTagsContainer);
			
			GameplayEffect owner = Owner;
			owner.OnGameplayEffectChanged();
		}

		public override void OnGameplayEffectChanged()
		{
			base.OnGameplayEffectChanged();
			ApplyTargetTagChanges();
		}

		public void SetAndApplyTargetTagChanges(in InheritedTagContainer tagContainerMods)
		{
			InheritableGrantedTagsContainer = tagContainerMods;
			ApplyTargetTagChanges();
		}

		private void ApplyTargetTagChanges()
		{
			GameplayEffect owner = Owner;
			InheritableGrantedTagsContainer.ApplyTo(owner.CachedGrantedTags);
		}
	}
}