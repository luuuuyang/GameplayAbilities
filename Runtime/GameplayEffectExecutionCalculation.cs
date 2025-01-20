using System.Collections.Generic;

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

    public class GameplayEffectExecutionCalculation : GameplayEffectCalculation
    {
        public void Execute(in GameplayEffectCustomExecutionParams execution_params, out GameplayEffectCustomExecutionOutput execution_output)
        {
            execution_output = new GameplayEffectCustomExecutionOutput();
        }
    }
}