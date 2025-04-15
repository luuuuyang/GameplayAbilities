using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GameplayAbilities
{
    [LabelText("Additional Effects")]
    public class AdditionalEffectsGameplayEffectComponent : GameplayEffectComponent
    {
        public bool OnApplicationCopyDataFromOriginalSpec = false;
        public List<ConditionalGameplayEffect> OnApplicationGameplayEffects = new();
        public List<GameplayEffect> OnCompleteAlways;
        public List<GameplayEffect> OnCompleteNormal;
        public List<GameplayEffect> OnCompletePrematurely;

        public override bool OnActiveGameplayEffectAdded(ActiveGameplayEffectsContainer activeGEContainer, ActiveGameplayEffect activeGE)
        {
            activeGE.EventSet.OnEffectRemoved.AddListener(removalInfo => OnActiveGameplayEffectRemoved(removalInfo, activeGEContainer));
            return true;
        }

        public override void OnGameplayEffectApplied(ActiveGameplayEffectsContainer activeGEContainer, GameplayEffectSpec GESpec)
        {
            if (activeGEContainer.Owner == null)
            {
                Debug.LogError("OnGameplayEffectApplied is passed an ActiveGEContainer which lives within an ASC but that ASC was somehow null");
                return;
            }

            float GELevel = GESpec.Level;
            GameplayEffectContextHandle GEContextHandle = GESpec.EffectContext;

            List<GameplayEffectSpecHandle> targetEffectSpecs = new();
            foreach (ConditionalGameplayEffect conditionalEffect in OnApplicationGameplayEffects)
            {
                GameplayEffect gameplayEffectDef = conditionalEffect.Effect;
                if (gameplayEffectDef == null)
                {
                    continue;
                }

                if (conditionalEffect.CanApply(GESpec.CapturedSourceTags.ActorTags, GELevel))
                {
                    GameplayEffectSpecHandle specHandle;
                    if (OnApplicationCopyDataFromOriginalSpec)
                    {
                        specHandle = new GameplayEffectSpecHandle(new GameplayEffectSpec());
                        specHandle.Data.InitializeFromLinkedSpec(gameplayEffectDef, GESpec);
                    }
                    else
                    {
                        specHandle = conditionalEffect.CreateSpec(GEContextHandle, GELevel);
                    }

                    if (specHandle.IsValid())
                    {
                        targetEffectSpecs.Add(specHandle);
                    }
                }
            }

            targetEffectSpecs.AddRange(GESpec.TargetEffectSpecs);

            AbilitySystemComponent appliedToASC = activeGEContainer.Owner;
            foreach (GameplayEffectSpecHandle targetSpec in targetEffectSpecs)
            {
                if (targetSpec.IsValid())
                {
                    appliedToASC.ApplyGameplayEffectSpecToSelf(targetSpec.Data);
                }
            }
        }

        protected void OnActiveGameplayEffectRemoved(in GameplayEffectRemovalInfo removalInfo, ActiveGameplayEffectsContainer activeGEContainer)
        {
            ActiveGameplayEffect activeGE = removalInfo.ActiveEffect;
            if (activeGE == null)
            {
                Debug.LogError("GameplayEffectRemovalInfo::ActiveEffect was not populated in OnActiveGameplayEffectRemoved");
                return;
            }

            AbilitySystemComponent ASC = activeGEContainer.Owner;
            if (ASC == null)
            {
                Debug.LogError("ActiveGEContainer was invalid in OnActiveGameplayEffectRemoved");
                return;
            }

            List<GameplayEffect> expiryEffects = removalInfo.PrematureRemoval ? OnCompletePrematurely : OnCompleteNormal;

            List<GameplayEffect> allGameplayEffects = new List<GameplayEffect>(expiryEffects);
            allGameplayEffects.AddRange(OnCompleteAlways);

            foreach (GameplayEffect curExpiryEffect in allGameplayEffects)
            {
                GameplayEffectSpec newSpec = new();
                newSpec.InitializeFromLinkedSpec(curExpiryEffect, activeGE.Spec);

                ASC.ApplyGameplayEffectSpecToSelf(newSpec);
            }
        }
    }
}

