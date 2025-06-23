using System;
using System.Collections.Generic;
using GameplayTags;
using UnityEngine;
using Sirenix.OdinInspector;

namespace GameplayAbilities
{
    public class GameplayEffectCustomExecutionParams
    {
        private Dictionary<GameplayEffectAttributeCaptureDefinition, Aggregator> ScopedModifierAggregators = new();
        private Dictionary<GameplayTag, Aggregator> ScopedTransientAggregators = new();
        public GameplayEffectSpec OwningSpec { get; private set; }
        public GameplayEffectSpec OwningSpecForPreExecuteMod => OwningSpec;
        public WeakReference<AbilitySystemComponent> TargetAbilitySystemComponent { get; private set; }
        public GameplayTagContainer PassedInTags { get; private set; }
        public List<ActiveGameplayEffectHandle> IgnoreHandles { get; private set; }

        public GameplayEffectCustomExecutionParams()
        {

        }

        public GameplayEffectCustomExecutionParams(GameplayEffectSpec owningSpec, in List<GameplayEffectExecutionScopedModifierInfo> scopedMods, AbilitySystemComponent targetAbilitySystemComponent, in GameplayTagContainer passedInTags)
        {
            OwningSpec = owningSpec;
            TargetAbilitySystemComponent = new WeakReference<AbilitySystemComponent>(targetAbilitySystemComponent);
            PassedInTags = passedInTags;

            Debug.Assert(owningSpec.Def != null);

            ActiveGameplayEffectHandle modifierHandle = ActiveGameplayEffectHandle.GenerateNewHandle(targetAbilitySystemComponent);

            foreach (GameplayEffectExecutionScopedModifierInfo curScopedMod in scopedMods)
            {
                Aggregator scopedAggregator;
                if (curScopedMod.AggregatorType == GameplayEffectScopedModifierAggregatorType.CapturedAttributeBacked)
                {
                    if (!ScopedModifierAggregators.TryGetValue(curScopedMod.CaptureAttribute, out scopedAggregator))
                    {
                        GameplayEffectAttributeCaptureSpec captureSpec = owningSpec.CapturedRelevantAttributes.FindCaptureSpecByDefinition(curScopedMod.CaptureAttribute, true);

                        Aggregator snapshotAggregator = new();
                        if (captureSpec != null && captureSpec.AttemptGetAttributeAggregatorSnapshot(ref snapshotAggregator))
                        {
                            ScopedModifierAggregators.Add(curScopedMod.CaptureAttribute, snapshotAggregator);
                            scopedAggregator = snapshotAggregator;
                        }
                    }
                }
                else
                {
                    if (!ScopedTransientAggregators.TryGetValue(curScopedMod.TransientAggregatorIdentifier, out scopedAggregator))
                    {
                        ScopedTransientAggregators.Add(curScopedMod.TransientAggregatorIdentifier, scopedAggregator);
                    }
                }

                float modEvalValue = 0f;
                if (scopedAggregator != null && curScopedMod.ModifierMagnitude.AttemptCalculateMagnitude(owningSpec, ref modEvalValue))
                {
                    scopedAggregator.AddAggregatorMod(modEvalValue, curScopedMod.ModifierOp, curScopedMod.EvaluationChannelSettings.EvaluationChannel, curScopedMod.SourceTags, curScopedMod.TargetTags, modifierHandle);
                }
                else
                {
                    Debug.LogWarning($"Attempted to apply a scoped modifier from {owningSpec.Def.name}'s {curScopedMod.CaptureAttribute} magnitude calculation that could not be properly calculated. Some attributes necessary for the calculation were missing.");
                }
            }
        }

        public GameplayEffectCustomExecutionParams(GameplayEffectSpec owningSpec, in List<GameplayEffectExecutionScopedModifierInfo> scopedMods, AbilitySystemComponent targetAbilitySystemComponent, in GameplayTagContainer passedInTags, in List<ActiveGameplayEffectHandle> ignoreHandles)
            : this(owningSpec, scopedMods, targetAbilitySystemComponent, passedInTags)
        {
            IgnoreHandles = ignoreHandles;
        }

        public AbilitySystemComponent GetTargetAbilitySystemComponent()
        {
            if (TargetAbilitySystemComponent.TryGetTarget(out AbilitySystemComponent abilitySystemComponent))
            {
                return abilitySystemComponent;
            }

            return null;
        }

        public AbilitySystemComponent GetSourceAbilitySystemComponent()
        {
            return OwningSpec.EffectContext.InstigatorAbilitySystemComponent;
        }

        public bool AttemptCalculateCapturedAttributeMagnitude(in GameplayEffectAttributeCaptureDefinition captureDef, in AggregatorEvaluateParameters evalParams, ref float magnitude)
        {
            Debug.Assert(OwningSpec != null);

            if (ScopedModifierAggregators.TryGetValue(captureDef, out Aggregator calcAggregator))
            {
                magnitude = calcAggregator.Evaluate(evalParams);
                return true;
            }
            else
            {
                GameplayEffectAttributeCaptureSpec captureSpec = OwningSpec.CapturedRelevantAttributes.FindCaptureSpecByDefinition(captureDef, true);
                if (captureSpec != null)
                {
                    return captureSpec.AttemptCalculateAttributeMagnitude(evalParams, ref magnitude);
                }
            }

            return false;
        }

        public bool AttemptCalculateCapturedAttributeMagnitudeWithBase(in GameplayEffectAttributeCaptureDefinition captureDef, in AggregatorEvaluateParameters evalParams, float baseValue, ref float magnitude)
        {
            Debug.Assert(OwningSpec != null);

            if (ScopedModifierAggregators.TryGetValue(captureDef, out Aggregator calcAggregator))
            {
                magnitude = calcAggregator.EvaluateWithBase(baseValue, evalParams);
                return true;
            }
            else
            {
                GameplayEffectAttributeCaptureSpec captureSpec = OwningSpec.CapturedRelevantAttributes.FindCaptureSpecByDefinition(captureDef, true);
                if (captureSpec != null)
                {
                    return captureSpec.AttemptCalculateAttributeMagnitudeWithBase(evalParams, baseValue, ref magnitude);
                }
            }

            return false;
        }

        public bool AttemptCalculateCapturedAttributeBaseValue(in GameplayEffectAttributeCaptureDefinition captureDef, ref float magnitude)
        {
            Debug.Assert(OwningSpec != null);

            if (ScopedModifierAggregators.TryGetValue(captureDef, out Aggregator calcAggregator))
            {
                magnitude = calcAggregator.BaseValue;
                return true;
            }
            else
            {
                GameplayEffectAttributeCaptureSpec captureSpec = OwningSpec.CapturedRelevantAttributes.FindCaptureSpecByDefinition(captureDef, true);
                if (captureSpec != null)
                {
                    return captureSpec.AttemptCalculateAttributeBaseValue(ref magnitude);
                }
            }

            return false;
        }

        public bool AttemptCalculateCapturedAttributeBonusMagnitude(in GameplayEffectAttributeCaptureDefinition captureDef, in AggregatorEvaluateParameters evalParams, ref float magnitude)
        {
            Debug.Assert(OwningSpec != null);

            if (ScopedModifierAggregators.TryGetValue(captureDef, out Aggregator calcAggregator))
            {
                magnitude = calcAggregator.EvaluateBonus(evalParams);
                return true;
            }
            else
            {
                GameplayEffectAttributeCaptureSpec captureSpec = OwningSpec.CapturedRelevantAttributes.FindCaptureSpecByDefinition(captureDef, true);
                if (captureSpec != null)
                {
                    return captureSpec.AttemptCalculateAttributeBonusMagnitude(evalParams, ref magnitude);
                }
            }

            return false;
        }

        public bool AttemptGetAttributeAggregatorSnapshot(in GameplayEffectAttributeCaptureDefinition captureDef, ref Aggregator snapshottedAggregator)
        {
            Debug.Assert(OwningSpec != null);

            if (ScopedModifierAggregators.TryGetValue(captureDef, out Aggregator calcAggregator))
            {
                calcAggregator.TakeSnapshotOf(snapshottedAggregator);
                return true;
            }
            else
            {
                GameplayEffectAttributeCaptureSpec captureSpec = OwningSpec.CapturedRelevantAttributes.FindCaptureSpecByDefinition(captureDef, true);
                if (captureSpec != null)
                {
                    return captureSpec.AttemptGetAttributeAggregatorSnapshot(ref snapshottedAggregator);
                }
            }

            return false;
        }

        public bool AttemptGatherAttributeMods(in GameplayEffectAttributeCaptureDefinition captureDef, in AggregatorEvaluateParameters evalParams, Dictionary<GameplayModEvaluationChannel, List<AggregatorMod>[]> modMap)
        {
            Debug.Assert(OwningSpec != null);

            if (ScopedModifierAggregators.TryGetValue(captureDef, out Aggregator calcAggregator))
            {
                calcAggregator.GetAllAggregatorMods(modMap);
                return true;
            }
            else
            {
                GameplayEffectAttributeCaptureSpec captureSpec = OwningSpec.CapturedRelevantAttributes.FindCaptureSpecByDefinition(captureDef, true);
                if (captureSpec != null)
                {
                    return captureSpec.AttemptGatherAttributeMods(evalParams, modMap);
                }
            }

            return false;
        }

        public bool ForEachQualifiedAttributeMod(in GameplayEffectAttributeCaptureDefinition captureDef, in AggregatorEvaluateParameters evalParams, Action<GameplayModEvaluationChannel, GameplayModOp, AggregatorMod> action)
        {
            Dictionary<GameplayModEvaluationChannel, List<AggregatorMod>[]> modMap = new();
            if (AttemptGatherAttributeMods(captureDef, evalParams, modMap))
            {
                foreach (var item in modMap)
                {
                    List<AggregatorMod>[] mods = item.Value;
                    for (int modOpIdx = 0; modOpIdx < mods.Length; modOpIdx++)
                    {
                        List<AggregatorMod> curModArray = mods[modOpIdx];
                        foreach (AggregatorMod aggMod in curModArray)
                        {
                            if (aggMod.Qualifies)
                            {
                                action(item.Key, (GameplayModOp)modOpIdx, aggMod);
                            }
                        }
                    }
                }

                return true;
            }

            return false;
        }

        public bool AttemptCalculateTransientAggregatorMagnitude(in GameplayTag aggregatorIdentifier, in AggregatorEvaluateParameters evalParams, ref float magnitude)
        {
            if (ScopedTransientAggregators.TryGetValue(aggregatorIdentifier, out Aggregator calcAggregator))
            {
                magnitude = calcAggregator.Evaluate(evalParams);
                return true;
            }

            return false;
        }

        public bool AttemptCalculateTransientAggregatorMagnitudeWithBase(in GameplayTag aggregatorIdentifier, in AggregatorEvaluateParameters evalParams, float baseValue, ref float magnitude)
        {
            if (ScopedTransientAggregators.TryGetValue(aggregatorIdentifier, out Aggregator calcAggregator))
            {
                magnitude = calcAggregator.EvaluateWithBase(baseValue, evalParams);
                return true;
            }

            return false;
        }

        public bool AttemptCalculateTransientAggregatorBaseValue(in GameplayTag aggregatorIdentifier, ref float magnitude)
        {
            if (ScopedTransientAggregators.TryGetValue(aggregatorIdentifier, out Aggregator calcAggregator))
            {
                magnitude = calcAggregator.BaseValue;
                return true;
            }

            return false;
        }

        public bool AttemptCalculateTransientAggregatorBonusMagnitude(in GameplayTag aggregatorIdentifier, in AggregatorEvaluateParameters evalParams, ref float magnitude)
        {
            if (ScopedTransientAggregators.TryGetValue(aggregatorIdentifier, out Aggregator calcAggregator))
            {
                magnitude = calcAggregator.EvaluateBonus(evalParams);
                return true;
            }

            return false;
        }

        public bool AttemptGetTransientAggregatorSnapshot(in GameplayTag aggregatorIdentifier, ref Aggregator snapshottedAggregator)
        {
            if (ScopedTransientAggregators.TryGetValue(aggregatorIdentifier, out Aggregator calcAggregator))
            {
                calcAggregator.TakeSnapshotOf(snapshottedAggregator);
                return true;
            }

            return false;
        }
    }

    public class GameplayEffectCustomExecutionOutput
    {
        public bool ShouldTriggerConditionalGameplayEffects => TriggerConditionalGameplayEffects;
        public bool IsStackCountHandledManually => HandleStackCountManually;

        public List<GameplayModifierEvaluatedData> OutputModifiers { get; private set; } = new();
        private bool TriggerConditionalGameplayEffects;
        private bool HandleStackCountManually;

        public void AddOutputModifier(in GameplayModifierEvaluatedData outputModifier)
        {
            OutputModifiers.Add(outputModifier);
        }

        public void MarkConditionalGameplayEffectsToTrigger()
        {
            TriggerConditionalGameplayEffects = true;
        }

        public void MarkStackCountHandledManually()
        {
            HandleStackCountManually = true;
        }
    }

    [CreateAssetMenu(fileName = "GameplayEffectExecutionCalculation", menuName = "GameplayAbilities/GameplayEffectExecutionCalculation")]
    public class GameplayEffectExecutionCalculation : GameplayEffectCalculation
    {
        [FoldoutGroup("Attributes")]
        [PropertyOrder(0)]
        [SerializeField]
        protected bool RequiresPassedInTags;

#if UNITY_EDITOR

        // Any attribute in this list will not show up as a valid option for scoped modifiers; Used to allow attribute capture for internal calculation while preventing modification
        [FoldoutGroup("Attributes")]
        [SerializeField]
        [PropertyOrder(1)]
        protected List<GameplayEffectAttributeCaptureDefinition> InvalidScopedModifierAttributes;

        // Any tag in this container will show up as a valid "temporary variable" for scoped modifiers; Used to allow for data-driven variable support that doesn't rely on scoped modifiers
        [FoldoutGroup("Non Attribute Calculation")]
        [SerializeField]
        protected GameplayTagContainer ValidTransientAggregatorIdentifiers;

        public virtual void GetValidScopedModifierAttributeCaptureDefinitions(List<GameplayEffectAttributeCaptureDefinition> outScopableModifiers)
        {
            List<GameplayEffectAttributeCaptureDefinition> defaultCaptureDefs = AttributeCaptureDefinitions;
            foreach (GameplayEffectAttributeCaptureDefinition curDef in defaultCaptureDefs)
            {
                if (!InvalidScopedModifierAttributes.Contains(curDef))
                {
                    outScopableModifiers.Add(curDef);
                }
            }
        }

        public virtual GameplayTagContainer GetValidTransientAggregatorIdentifiers()
        {
            return ValidTransientAggregatorIdentifiers;
        }
#endif

        public void Execute(in GameplayEffectCustomExecutionParams executionParams, GameplayEffectCustomExecutionOutput executionOutput)
        {
            Execute_Implementation(executionParams, executionOutput);
        }

        protected virtual void Execute_Implementation(in GameplayEffectCustomExecutionParams executionParams, GameplayEffectCustomExecutionOutput executionOutput)
        {

        }
    }
}