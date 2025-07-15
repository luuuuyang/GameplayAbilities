using System.Collections.Generic;
using System.Linq;
using GameplayTags;
using UnityEngine;

namespace GameplayAbilities
{
#if UNITY_EDITOR
    public struct GameplayCueTranslationEditorOnlyData
    {
        public string EditorDescription;
        public string ToolTip;
        public int UniqueId;
        public bool Enabled;
    }

    public struct GameplayCueTranslationEditorInfo
    {
        public GameplayTag GameplayTag;
        public string GameplayTagName;
        public GameplayCueTranslationEditorOnlyData EditorData;
    }
#endif

    public class GameplayCueTranslationNameSwap
    {
        public string FromName;
        public List<string> ToNames;
#if UNITY_EDITOR
        public GameplayCueTranslationEditorOnlyData EditorData;
#endif
    }

    public struct GameplayCueTranslationNodeIndex
    {
        public int Index;

        public bool IsValid => Index != -1;

        public static bool operator ==(GameplayCueTranslationNodeIndex a, GameplayCueTranslationNodeIndex b)
        {
            return a.Index == b.Index;
        }

        public static bool operator !=(GameplayCueTranslationNodeIndex a, GameplayCueTranslationNodeIndex b)
        {
            return a.Index != b.Index;
        }

        public static implicit operator int(GameplayCueTranslationNodeIndex index)
        {
            return index.Index;
        }

        public override bool Equals(object obj)
        {
            if (obj is GameplayCueTranslationNodeIndex other)
            {
                return Index == other.Index;
            }
            return false;
        }
    }

    public struct GameplayCueTranslationLink
    {
        public GameplayCueTranslator RulesCDO;
        public List<GameplayCueTranslationNodeIndex> NodeLookup;
    }

    public class GameplayCueTranslationNode
    {
        public List<GameplayCueTranslationLink> Links;
        public GameplayCueTranslationNodeIndex CachedIndex;
        public GameplayTag CachedGameplayTag;
        public string CachedGameplayTagName;
        public HashSet<GameplayCueTranslator> UsedTranslators;
        public GameplayCueTranslationLink FindOrCreateLink(in GameplayCueTranslator ruleClassCDO, int lookupSize)
        {
            int insertIdx = 0;
            int newPriority = ruleClassCDO.Priority;

            for (int linkIdx = 0; linkIdx < Links.Count; linkIdx++)
            {
                if (Links[linkIdx].RulesCDO == ruleClassCDO)
                {
                    return Links[linkIdx];
                }

                if (Links[linkIdx].RulesCDO.Priority > newPriority)
                {
                    insertIdx = linkIdx + 1;
                }
            }

            GameplayCueTranslationLink newLink = new()
            {
                RulesCDO = ruleClassCDO,
                NodeLookup = new List<GameplayCueTranslationNodeIndex>(lookupSize)
            };
            Links.Insert(insertIdx, newLink);

            return newLink;
        }
    }

    public struct NameSwapData
    {
        public GameplayCueTranslator ClassCDO;
        public List<GameplayCueTranslationNameSwap> NameSwaps;
    }

    public struct GameplayCueTranslationManager
    {
        private List<GameplayCueTranslationNode> TranslationLUT;
        private Dictionary<string, GameplayCueTranslationNodeIndex> TranslationNameToIndexMap;
        private GameplayTagsManager TagManager;
        private List<NameSwapData> AllNameSwaps;
        private int TotalNumTranslations;
        private int TotalNumTheoreticalTranslations;

        public void TranslateTag(GameplayTag tag, GameObject targetActor, in GameplayCueParameters parameters)
        {
            GameplayCueTranslationNode node = GetTranslationNodeForTag(tag);
            if (node != null)
            {
                TranslateTag_Internal(node, ref tag, tag.TagName, targetActor, parameters);
            }
        }

        public void BuildTagTranslationTable()
        {
            TagManager = GameplayTagsManager.Instance;

            GameplayTagContainer allGameplayCueTags = TagManager.RequestGameplayTagChildren(GameplayCueSet.BaseGameplayCueTag);

            ResetTranslationLUT();
            RefreshNameSwaps();

            List<string> splitNames = new(10);

            foreach (GameplayTag tag in allGameplayCueTags)
            {
                splitNames.Clear();
                TagManager.SplitGameplayTagName(tag, splitNames);

                BuildTagTranslationTable(tag.TagName, splitNames);
            }
        }

        public bool BuildTagTranslationTable(in string tagName, in List<string> splitNames)
        {
            bool hasValidRootTag = false;

            List<string> swappedNames = new(10);

            foreach (NameSwapData nameSwapData in AllNameSwaps)
            {
                {
                    GameplayCueTranslationNode childNode = GetTranslationNodeForName(tagName, false);
                    if (childNode.UsedTranslators.Contains(nameSwapData.ClassCDO))
                    {
                        continue;
                    }
                }

                for (int swapRuleIdx = 0; swapRuleIdx < nameSwapData.NameSwaps.Count; swapRuleIdx++)
                {
                    GameplayCueTranslationNameSwap swapRule = nameSwapData.NameSwaps[swapRuleIdx];

#if UNITY_EDITOR
                    if (!swapRule.EditorData.Enabled)
                    {
                        continue;
                    }
#endif
                    for (int tagIdx = 0; tagIdx < splitNames.Count; tagIdx++)
                    {
                        for (int toNameIdx = 0; toNameIdx < swapRule.ToNames.Count && tagIdx < splitNames.Count; toNameIdx++)
                        {
                            if (swapRule.ToNames[toNameIdx] == splitNames[tagIdx])
                            {
                                if (toNameIdx == swapRule.ToNames.Count - 1)
                                {
                                    swappedNames = splitNames;

                                    int numRemoves = swapRule.ToNames.Count;
                                    int removeAtIdx = tagIdx - (swapRule.ToNames.Count - 1);

                                    swappedNames.RemoveRange(removeAtIdx, numRemoves);
                                    swappedNames.Insert(removeAtIdx, swapRule.FromName);

                                    string composedString = swappedNames[0];
                                    for (int composeIdx = 1; composeIdx < swappedNames.Count; composeIdx++)
                                    {
                                        composedString += $".{swappedNames[composeIdx]}";
                                    }

                                    string composedName = composedString;

                                    {
                                        GameplayTag composedTag = TagManager.RequestGameplayTag(composedName, false);
                                        if (!composedTag.IsValid())
                                        {
                                            GameplayCueTranslationNodeIndex parentIdx = GetTranslationIndexForName(composedName, false);
                                            if (!parentIdx.IsValid)
                                            {
                                                parentIdx = GetTranslationIndexForName(composedName, true);
                                                TranslationLUT[parentIdx].UsedTranslators.Add(nameSwapData.ClassCDO);

                                                hasValidRootTag |= BuildTagTranslationTable(composedName, swappedNames);
                                            }
                                        }
                                        else
                                        {
                                            hasValidRootTag = true;
                                        }
                                    }

                                    if (hasValidRootTag)
                                    {
                                        GameplayCueTranslationNodeIndex parentIdx = GetTranslationIndexForName(composedName, true);

                                        GameplayCueTranslationNodeIndex childIdx = GetTranslationIndexForName(tagName, true);

                                        GameplayCueTranslationNode parentNode = TranslationLUT[parentIdx];

                                        GameplayCueTranslationLink newLink = parentNode.FindOrCreateLink(nameSwapData.ClassCDO, nameSwapData.NameSwaps.Count);

                                        newLink.NodeLookup[swapRuleIdx] = childIdx;

                                        GameplayCueTranslationNode childNode = TranslationLUT[childIdx];
                                        childNode.UsedTranslators.UnionWith(parentNode.UsedTranslators);
                                        childNode.UsedTranslators.Add(nameSwapData.ClassCDO);
                                    }
                                    else
                                    {

                                    }

                                    break;
                                }
                                else
                                {
                                    tagIdx++;
                                    continue;
                                }
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                }
            }

            return hasValidRootTag;
        }

        public void BuildTagTranslationTable_Forward()
        {
            BuildTagTranslationTable();

            List<string> splitNames = new(10);

            GameplayTagContainer allGameplayCueTags = TagManager.RequestGameplayTagChildren(GameplayCueSet.BaseGameplayCueTag);

            foreach (GameplayTag tag in allGameplayCueTags)
            {
                splitNames.Clear();
                TagManager.SplitGameplayTagName(tag, splitNames);

                BuildTagTranslationTable_Forward(tag.TagName, splitNames);
            }
        }

        public void BuildTagTranslationTable_Forward(in string tagName, in List<string> splitNames)
        {
            List<string> swappedNames = new(10);

            foreach (NameSwapData nameSwapData in AllNameSwaps)
            {
                {
                    GameplayCueTranslationNode childNode = GetTranslationNodeForName(tagName, false);
                    if (childNode.UsedTranslators.Contains(nameSwapData.ClassCDO))
                    {
                        continue;
                    }
                }

                for (int swapRuleIdx = 0; swapRuleIdx < nameSwapData.NameSwaps.Count; swapRuleIdx++)
                {
                    GameplayCueTranslationNameSwap swapRule = nameSwapData.NameSwaps[swapRuleIdx];

#if UNITY_EDITOR
                    if (!swapRule.EditorData.Enabled)
                    {
                        continue;
                    }
#endif
                    for (int tagIdx = 0; tagIdx < splitNames.Count; tagIdx++)
                    {
                        if (splitNames[tagIdx] == swapRule.FromName)
                        {
                            swappedNames = splitNames;

                            swappedNames.RemoveAt(tagIdx);
                            for (int toIdx = 0; toIdx < swapRule.ToNames.Count; toIdx++)
                            {
                                swappedNames.Insert(tagIdx + toIdx, swapRule.ToNames[toIdx]);
                            }

                            string composedString = swappedNames[0];
                            for (int composeIdx = 1; composeIdx < swappedNames.Count; composeIdx++)
                            {
                                composedString += $".{swappedNames[composeIdx]}";
                            }

                            string composedName = composedString;

                            GameplayCueTranslationNodeIndex childIdx = GetTranslationIndexForName(composedName, true);
                            if (childIdx.IsValid)
                            {
                                GameplayCueTranslationNodeIndex parentIdx = GetTranslationIndexForName(tagName, true);
                                if (parentIdx.IsValid)
                                {
                                    GameplayCueTranslationNode parentNode = TranslationLUT[parentIdx];
                                    GameplayCueTranslationNode childNode = TranslationLUT[childIdx];

                                    GameplayCueTranslationLink newLink = parentNode.FindOrCreateLink(nameSwapData.ClassCDO, nameSwapData.NameSwaps.Count);

                                    newLink.NodeLookup[swapRuleIdx] = childNode.CachedIndex;

                                    childNode.UsedTranslators.UnionWith(parentNode.UsedTranslators);
                                    childNode.UsedTranslators.Add(nameSwapData.ClassCDO);
                                }
                            }

                            BuildTagTranslationTable_Forward(composedName, swappedNames);
                        }
                    }
                }
            }
        }

        public void RefreshNameSwaps()
        {
            AllNameSwaps.Clear();
            List<GameplayCueTranslator> CDOList = new();

            CDOList.Sort((a, b) => a.Priority.CompareTo(b.Priority));

            foreach (GameplayCueTranslator CDO in CDOList)
            {
                NameSwapData data = new();
                AllNameSwaps.Add(data);
                CDO.GetTranslationNameSpawns(data.NameSwaps);
                if (data.NameSwaps.Count > 0)
                {
                    data.ClassCDO = CDO;
                }
                else
                {
                    AllNameSwaps.Remove(data);
                }
            }

#if UNITY_EDITOR
            int id = 1;
            foreach (NameSwapData groupData in AllNameSwaps)
            {
                foreach (GameplayCueTranslationNameSwap swap in groupData.NameSwaps)
                {
                    swap.EditorData.UniqueId = id++;
                }
            }
#endif
        }

        public void PrintTranslationTable()
        {

        }

        public void PrintTranslationTable(GameplayCueTranslationNode node, string identStr = null)
        {

        }

        private bool TranslateTag_Internal(GameplayCueTranslationNode node, ref GameplayTag tag, in string tagName, GameObject targetActor, in GameplayCueParameters parameters)
        {
            foreach (GameplayCueTranslationLink link in node.Links)
            {
                int translationIndex = link.RulesCDO.GameplayCueToTranslationIndex(tagName, targetActor, parameters);
                if (translationIndex != -1)
                {
                    if (!link.NodeLookup.IsValidIndex(translationIndex))
                    {
                        continue;
                    }

                    GameplayCueTranslationNodeIndex nodeIndex = link.NodeLookup[translationIndex];
                    if (nodeIndex.IsValid)
                    {
                        if (!TranslationLUT.IsValidIndex(nodeIndex))
                        {
                            continue;
                        }

                        GameplayCueTranslationNode innerNode = TranslationLUT[nodeIndex];

                        tag = innerNode.CachedGameplayTag;

                        TranslateTag_Internal(innerNode, ref tag, innerNode.CachedGameplayTagName, targetActor, parameters);
                        return true;
                    }
                }
            }

            return false;
        }

        private GameplayCueTranslationNodeIndex GetTranslationIndexForTag(in GameplayTag tag, bool createIfInvalid = false)
        {
            return GetTranslationIndexForName(tag.TagName, createIfInvalid);
        }

        private GameplayCueTranslationNode GetTranslationNodeForTag(in GameplayTag tag, bool createIfInvalid = false)
        {
            GameplayCueTranslationNodeIndex idx = GetTranslationIndexForTag(tag, createIfInvalid);
            if (TranslationLUT.IsValidIndex(idx))
            {
                return TranslationLUT[idx];
            }

            return null;
        }

        private GameplayCueTranslationNode GetTranslationNodeForName(string name, bool createIfInvalid = false)
        {
            GameplayCueTranslationNodeIndex idx = GetTranslationIndexForName(name, createIfInvalid);
            if (TranslationLUT.IsValidIndex(idx))
            {
                return TranslationLUT[idx];
            }

            return null;
        }

        private GameplayCueTranslationNodeIndex GetTranslationIndexForName(string name, bool createIfInvalid = false)
        {
            GameplayCueTranslationNodeIndex idx = new();
            if (createIfInvalid)
            {
                if (!TranslationNameToIndexMap.TryGetValue(name, out GameplayCueTranslationNodeIndex mapIdx))
                {
                    mapIdx = new();
                    TranslationNameToIndexMap.Add(name, mapIdx);
                }

                idx = mapIdx;

                if (!TranslationLUT[idx].CachedIndex.IsValid)
                {
                    TranslationLUT[idx].CachedIndex = idx;
                    TranslationLUT[idx].CachedGameplayTag = TagManager.RequestGameplayTag(name, false);
                    TranslationLUT[idx].CachedGameplayTagName = name;
                }
            }
            else
            {
                TranslationNameToIndexMap.TryGetValue(name, out idx);
            }

#if UNITY_EDITOR
            if (idx != null && !TranslationLUT[idx].CachedGameplayTag.IsValid())
            {
                TranslationLUT[idx].CachedGameplayTag = TagManager.RequestGameplayTag(name, false);
            }
#endif

            return idx;
        }

        private void ResetTranslationLUT()
        {
            TranslationNameToIndexMap.Clear();
            TranslationLUT.Clear();
        }
    }

    public abstract class GameplayCueTranslator
    {
        public virtual void GetTranslationNameSpawns(List<GameplayCueTranslationNameSwap> swapList)
        {

        }

        public virtual int GameplayCueToTranslationIndex(in string tagName, GameObject targetActor, in GameplayCueParameters parameters)
        {
            return -1;
        }

        public virtual int Priority => 0;

        public virtual bool IsEnabled => true;

        public virtual bool ShouldShowInTopLevelFilterList => true;
    }
}

