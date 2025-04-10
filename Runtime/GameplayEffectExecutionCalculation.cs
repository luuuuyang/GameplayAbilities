using System;
using System.Collections.Generic;
using GameplayTags;
using UnityEngine;
using Sirenix.OdinInspector;

namespace GameplayAbilities
{
    public struct GameplayEffectCustomExecutionParams
    {
        public GameplayEffectSpec Spec;
    }

    public struct GameplayEffectCustomExecutionOutput
    {
        public List<GameplayModifierEvaluatedData> OutputModifiers;
        public bool ShouldTriggerConditionalGameplayEffects;
        public bool IsStackCountHandledManually;
    }

    [CreateAssetMenu(fileName = "GameplayEffectExecutionCalculation", menuName = "GameplayAbilities/GameplayEffectExecutionCalculation")]
    public class GameplayEffectExecutionCalculation : GameplayEffectCalculation
    {
        [FoldoutGroup("Attributes")]
        [PropertyOrder(-1)]
        [SerializeField]
        protected bool RequiresPassedInTags;

#if UNITY_EDITOR
        [FoldoutGroup("Attributes")]
        [SerializeField]
        protected List<GameplayEffectAttributeCaptureDefinition> InvalidScopedModifierAttributes;

        [FoldoutGroup("Non Attribute Calculation")]
        [SerializeField]
        protected GameplayTagContainer ValidTransientAggregatorIdentifiers;
#endif

        public void Execute(in GameplayEffectCustomExecutionParams executionParams, out GameplayEffectCustomExecutionOutput executionOutput)
        {
            executionOutput = new GameplayEffectCustomExecutionOutput();
        }
    }
}