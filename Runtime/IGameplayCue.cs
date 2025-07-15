using System;
using System.Collections.Generic;
using GameplayTags;
using UnityEngine;

namespace GameplayAbilities
{
    internal static class GameplayCueInterfacePrivate
    {
        public struct CueNameAndFunction
        {
            public GameplayTag Tag;
            public Action Func;
        }

        public static Dictionary<object, Dictionary<GameplayTag, List<CueNameAndFunction>>> PerClassGameplayTagToFunctionMap = new();

        public static bool UseEqualTagCountAndRemovalCallbacks = true;
    }

    public interface IGameplayCue
    {
        private bool ForwardToParent
        {
            get
            {
                return false;
            }
            set
            {
                ForwardToParent = value;
            }
        }

        public virtual void GetGameplayCueSets(List<GameplayCueSet> sets) { }

        public virtual void HandleGameplayCue(UnityEngine.Object self, GameplayTag gameplayCueTag, GameplayCueEventType eventType, in GameplayCueParameters parameters)
        {
            if (self == null)
            {
                return;
            }

            Type type = self.GetType();
            GameplayTagContainer tagAndParentsContainer = gameplayCueTag.GetGameplayTagParents();

            parameters.OriginalTag = gameplayCueTag;

            GameplayCueInterfacePrivate.PerClassGameplayTagToFunctionMap.TryGetValue(self, out var gameplayTagFunctionList);
            gameplayTagFunctionList.TryGetValue(gameplayCueTag, out var functionList);

            if (functionList == null)
            {
                functionList = new List<GameplayCueInterfacePrivate.CueNameAndFunction>();
            }

            bool shouldContinue = true;
            for (int functionIndex = 0; functionIndex < functionList.Count && shouldContinue; functionIndex++)
            {
                var cueFunctionPair = functionList[functionIndex];
                Action func = cueFunctionPair.Func;
                parameters.MatchedTagName = cueFunctionPair.Tag;

                ForwardToParent = false;

                DispatchBlueprintCustomHandler(self, func, eventType, parameters);

                shouldContinue = ForwardToParent;

            }

            if (shouldContinue)
            {
                if (self is GameObject selfActor)
                {
                    List<GameplayCueSet> sets = new();
                    GetGameplayCueSets(sets);
                    foreach (GameplayCueSet set in sets)
                    {
                        shouldContinue = set.HandleGameplayCue(selfActor, gameplayCueTag, eventType, parameters);
                        if (!shouldContinue)
                        {
                            break;
                        }
                    }
                }
            }

            if (shouldContinue)
            {
                parameters.MatchedTagName = gameplayCueTag;
                GameplayCueDefaultHandler(eventType, parameters);
            }
        }

        public virtual void GameplayCueDefaultHandler(GameplayCueEventType eventType, in GameplayCueParameters parameters)
        {

        }

        public void ForwardGameplayCueToParent()
        {
            ForwardToParent = true;
        }

        public virtual void BlueprintCustomHandler(GameplayCueEventType eventType, in GameplayCueParameters parameters)
        {

        }

        public static void DispatchBlueprintCustomHandler(UnityEngine.Object self, Action func, GameplayCueEventType eventType, in GameplayCueParameters parameters)
        {
            func?.Invoke();
        }

        public static void ClearTagToFunctionMap()
        {
            GameplayCueInterfacePrivate.PerClassGameplayTagToFunctionMap.Clear();
        }

        public virtual void HandleGameplayCues(UnityEngine.Object self, in GameplayTagContainer gameplayCueTags, GameplayCueEventType eventType, in GameplayCueParameters parameters)
        {
            foreach (GameplayTag gameplayCueTag in gameplayCueTags)
            {
                HandleGameplayCue(self, gameplayCueTag, eventType, parameters);
            }
        }

        public virtual bool ShouldAcceptGameplayCue(UnityEngine.Object self, GameplayTag gameplayCueTag, GameplayCueEventType eventType, in GameplayCueParameters parameters)
        {
            return true;
        }
    }

    public struct ActiveGameplayCue
    {
        public GameplayTag GameplayCueTag;
        public GameplayCueParameters Parameters;

        public override string ToString()
        {
            return GameplayCueTag.ToString();
        }
    }

    public class ActiveGameplayCueContainer
    {
        public List<ActiveGameplayCue> GameplayCues;
        public AbilitySystemComponent Owner { get; set; }

        public void AddCue(in GameplayTag Tag, in GameplayCueParameters parameters)
        {
            if (Owner == null)
            {
                return;
            }

            ActiveGameplayCue newCue = new()
            {
                GameplayCueTag = Tag,
                Parameters = parameters
            };

            GameplayCues.Add(newCue);

            Owner.UpdateTagMap(Tag, 1);
        }

        public void RemoveCue(in GameplayTag Tag)
        {
            if (Owner == null)
            {
                return;
            }

            int countDelta = 0;

            if (!GameplayCueInterfacePrivate.UseEqualTagCountAndRemovalCallbacks)
            {
                foreach (ActiveGameplayCue gameplayCue in GameplayCues)
                {
                    if (gameplayCue.GameplayCueTag == Tag)
                    {
                        countDelta++;
                    }
                }

                if (countDelta > 0)
                {
                    Owner.UpdateTagMap(Tag, -countDelta);
                }
            }

            for (int idx = GameplayCues.Count - 1; idx >= 0; idx--)
            {
                ActiveGameplayCue cue = GameplayCues[idx];

                if (cue.GameplayCueTag == Tag)
                {
                    if (GameplayCueInterfacePrivate.UseEqualTagCountAndRemovalCallbacks)
                    {
                        countDelta++;
                        Owner.UpdateTagMap(Tag, -1);
                    }

                    Owner.InvokeGameplayCueEvent(Tag,GameplayCueEventType.Removed, cue.Parameters);
                    GameplayCues.RemoveAt(idx);
                }
            }

            Owner.UpdateTagMap(Tag, -1);
        }

        public bool HasCue(in GameplayTag Tag)
        {
            return false;
        }

        public void RemoveAllCues()
        {
            if (Owner == null)
            {
                return;
            }

            for (int idx = 0; idx < GameplayCues.Count; idx++)
            {
                ActiveGameplayCue cue = GameplayCues[idx];
                Owner.UpdateTagMap(cue.GameplayCueTag, -1);
                Owner.InvokeGameplayCueEvent(cue.GameplayCueTag, GameplayCueEventType.Removed, cue.Parameters);
            }
        }

       
        
    }
}
