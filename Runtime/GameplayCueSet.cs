using System;
using System.Collections.Generic;
using GameplayTags;
using UnityEngine;

namespace GameplayAbilities
{
    public struct GameplayCueNotifyData
    {
        public GameplayTag GameplayCueTag;
        public UnityEngine.Object GameplayCueNotifyObj;
        public int ParentDataIdx;
    }

    public struct GameplayCueReferencePair
    {
        public GameplayTag GameplayCueTag;
        public UnityEngine.Object StringRef;
    }

    public class GameplayCueSet : ScriptableObject
    {

        public List<GameplayCueNotifyData> GameplayCueData;
        public Dictionary<GameplayTag, int> GameplayCueDataMap;

        static NativeGameplayTag StaticTag_GameplayCue = new NativeGameplayTag("GameplayTags", "GameplayCue");
        public static GameplayTag BaseGameplayCueTag => StaticTag_GameplayCue;

        public virtual bool HandleGameplayCue(GameObject targetActor, GameplayTag gameplayCueTag, GameplayCueEventType eventType, in GameplayCueParameters parameters)
        {
            return true;
        }

        public virtual void AddCue(in List<GameplayCueReferencePair> cuesToAdd)
        {
            foreach (GameplayCueReferencePair pair in cuesToAdd)
            {
                GameplayCueDataMap.Add(pair.GameplayCueTag, GameplayCueData.Count);
            }
        }

        public virtual void RemoveCuesByTags(in GameplayTagContainer tagsToRemove)
        {
            foreach (GameplayTag tag in tagsToRemove)
            {
                GameplayCueDataMap.Remove(tag);
            }
        }

        public virtual void RemoveCuesByNotifyObjects(in List<UnityEngine.Object> cuesToRemove)
        {
            
        }

        public virtual void RemoveLoadedClass(Type type)
        {

        }

        public virtual void GetFilenames(List<string> filenames)
        {

        }

        public virtual void Empty()
        {

        }

        public virtual void PrintCues()
        {
            
        }

        protected virtual bool HandleGameplayCueNotify_Internal(GameObject targetActor, GameplayTag gameplayCueTag, GameplayCueEventType eventType, in GameplayCueParameters parameters)
        {
            return true;
        }

        protected virtual void BuildAccelarationMap_Internal()
        {

        }
    }
}

