using System;
using GameplayTags;
using UnityEngine;

namespace GameplayAbilities
{
    [Flags]
    public enum GameplayCueExecutionOptions
    {
        Default = 0,
        IgnoreInterfaces = 1,
        IgnoreNotifies = 2,
        IgnoreTranslation = 4,
        IgnoreSuppression = 8,
        IgnoreDebug = 16,
    }

    public class GameplayCueMananger
    {
        public virtual void HandleGameplayCue(GameObject targetActor, GameplayTag gameplayCueTag, GameplayCueEventType eventType, in GameplayCueParameters parameters, GameplayCueExecutionOptions options)
        {
            if (!options.HasFlag(GameplayCueExecutionOptions.IgnoreSuppression) && ShouldSuppressGameplayCue(targetActor))
            {
                return;
            }

            if (!options.HasFlag(GameplayCueExecutionOptions.IgnoreTranslation))
            {
                TranslateGameplayCue(gameplayCueTag, targetActor, parameters);
            }

            RouteGameplayCue(targetActor, gameplayCueTag, eventType, parameters, options);
        }

        public virtual bool ShouldSuppressGameplayCue(GameObject targetActor)
        {
            return false;
        }

        public virtual void TranslateGameplayCue(GameplayTag gameplayCueTag, GameObject targetActor, in GameplayCueParameters parameters)
        {

        }

        public virtual void RouteGameplayCue(GameObject targetActor, GameplayTag gameplayCueTag, GameplayCueEventType eventType, in GameplayCueParameters parameters, GameplayCueExecutionOptions options)
        {
            if (!parameters.OriginalTag.IsValid())
            {
                parameters.OriginalTag = gameplayCueTag;
            }

            IGameplayCue gameplayCueInterface = !options.HasFlag(GameplayCueExecutionOptions.IgnoreInterfaces) ? targetActor.GetComponent<IGameplayCue>() : null;
            bool acceptsCue = true;
            if (gameplayCueInterface != null)
            {
                acceptsCue = gameplayCueInterface.ShouldAcceptGameplayCue(targetActor, gameplayCueTag, eventType, parameters);
            }

            if (acceptsCue && !options.HasFlag(GameplayCueExecutionOptions.IgnoreNotifies))
            {
                // RuntimeGameplayCueObjectLibrary.CueSet.HandleGameplayCue(targetActor, gameplayCueTag, eventType, parameters);
            }

            if (gameplayCueInterface != null && acceptsCue)
            {
                gameplayCueInterface.HandleGameplayCue(targetActor, gameplayCueTag, eventType, parameters);
            }
        }
    }
}
