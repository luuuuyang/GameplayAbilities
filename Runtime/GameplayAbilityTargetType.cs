using System.Collections.Generic;
using GameplayTags;
using Sirenix.Utilities;
using UnityEngine;

namespace GameplayAbilities
{
    public class GameplayAbilityTargetData
    {
        public virtual List<GameObject> Actors { get; set; } = new();

        public List<ActiveGameplayEffectHandle> ApplyGameplayEffect(in GameplayEffect gameplayEffect, in GameplayEffectContextHandle effectContext, float level)
        {
            GameplayEffectSpec tempSpecToApply = new(gameplayEffect, effectContext, level);
            return ApplyGameplayEffectSpec(tempSpecToApply);
        }

        public virtual List<ActiveGameplayEffectHandle> ApplyGameplayEffectSpec(GameplayEffectSpec spec)
        {
            List<ActiveGameplayEffectHandle> appliedHandles = new();

            List<GameObject> actors = Actors;

            appliedHandles.Capacity = actors.Count;

            foreach (GameObject targetActor in actors)
            {
                AbilitySystemComponent targetComponent = targetActor.GetComponent<AbilitySystemComponent>();
                if (targetComponent != null)
                {
                    GameplayEffectSpec specToApply = new(spec);
                    GameplayEffectContextHandle effectContext = specToApply.EffectContext.Duplicate();
                    specToApply.SetContext(effectContext);

                    AddTargetDataToContext(effectContext, false);

                    appliedHandles.Add(effectContext.InstigatorAbilitySystemComponent.ApplyGameplayEffectSpecToTarget(specToApply, targetComponent));
                }
            }
            return appliedHandles;
        }

        public virtual void AddTargetDataToContext(GameplayEffectContextHandle context, bool includeActorArray)
        {
            if (includeActorArray)
            {
                List<GameObject> actors = Actors;
                if (actors.Count > 0)
                {
                    context.AddActors(actors);
                }
            }
        }
    }

    public class GameplayAbilityTargetDataHandle
    {
        public List<GameplayAbilityTargetData> Data = new();

        public GameplayAbilityTargetDataHandle(GameplayAbilityTargetData data)
        {
            Data.Add(data);
        }

        public static bool operator ==(GameplayAbilityTargetDataHandle a, GameplayAbilityTargetDataHandle b)
        {
            if (a.Data.Count != b.Data.Count)
            {
                return false;
            }
            for (int i = 0; i < a.Data.Count; i++)
            {
                if (a.Data[i] != null && b.Data[i] != null)
                {
                    return false;
                }
                if (a.Data[i] != b.Data[i])
                {
                    return false;
                }
            }
            return true;
        }

        public static bool operator !=(GameplayAbilityTargetDataHandle a, GameplayAbilityTargetDataHandle b)
        {
            return !(a == b);
        }
    }

    public class GameplayAbilityTargetData_ActorArray : GameplayAbilityTargetData
    {
        public List<GameObject> TargetActorArray = new();

        public override List<GameObject> Actors => TargetActorArray;
    }

    

    public delegate void AbilityTargetDataSetDelegate(in GameplayAbilityTargetDataHandle targetData, GameplayTag gameplayTag);
}
