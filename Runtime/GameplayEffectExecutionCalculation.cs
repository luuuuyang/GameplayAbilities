using System;
using System.Collections.Generic;
using GameplayTags;
using UnityEngine;

#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif

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
#if ODIN_INSPECTOR
        [FoldoutGroup("Attributes")]
        [PropertyOrder(-1)]
#endif
        [SerializeField]
        protected bool RequiresPassedInTags;

#if UNITY_EDITOR

#if ODIN_INSPECTOR
        [FoldoutGroup("Attributes")]
#endif
        [SerializeField]
        protected List<GameplayEffectAttributeCaptureDefinition> InvalidScopedModifierAttributes;

#if ODIN_INSPECTOR
        [FoldoutGroup("Non Attribute Calculation")]
#endif
        [SerializeField]
        protected GameplayTagContainer ValidTransientAggregatorIdentifiers;
#endif

        public void Execute(in GameplayEffectCustomExecutionParams executionParams, out GameplayEffectCustomExecutionOutput executionOutput)
        {
            executionOutput = new GameplayEffectCustomExecutionOutput();
        }
    }
}