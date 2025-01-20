using GameplayTags;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using Unity.PerformanceTesting;

namespace GameplayAbilities.Tests
{
    public class GameplayTagQueryTests
    {
        private const int NumDebugTags = 4;
        private const int NumTestScenarios = 1024;
        private const int DefaultNumTestsPerScenario = 1024;

        [Test, Performance]
        public void PerformanceTest()
        {
            int numTestsPerScenario = DefaultNumTestsPerScenario;

            // 统计数据
            uint numMetRequirements = 0;
            long metRequirementsTicks = 0;

            uint numQueryMatches = 0;
            long queryMatchesTicks = 0;

            // 获取随机标签
            List<GameplayTag> possibleTags = new();
            {
                GameplayTagContainer allGameplayTags = new();
                GameplayTagsManager.Instance.RequestAllGameplayTags(allGameplayTags, true);

                Assert.That(allGameplayTags.Num >= NumDebugTags,
                    $"There are only {allGameplayTags.Num} defined tags in the Project. We need at least {NumDebugTags} to run this test.");

                int step = allGameplayTags.Num / NumDebugTags;
                for (int index = 0; index < NumDebugTags; index++)
                {
                    GameplayTag randomGameplayTag = allGameplayTags.GetByIndex(index * step);
                    possibleTags.Add(randomGameplayTag);
                }
            }

            // 计算所有可能的标签组合
            List<GameplayTagContainer> possibleTagContainers = new();
            for (int index = 0; index < NumDebugTags; index++)
            {
                GameplayTagContainer container = new();
                container.AddTag(possibleTags[index]);

                for (int innerIndex = index; innerIndex < NumDebugTags; innerIndex++)
                {
                    container.AddTag(possibleTags[innerIndex]);
                    possibleTagContainers.Add(container);
                }
            }
            int numPossibleContainers = possibleTagContainers.Count;

            // 测试多个场景
            for (int scenarioNum = 0; scenarioNum < NumTestScenarios; scenarioNum++)
            {
                // 构建标签要求
                GameplayTagRequirements tagReqs = new();
                {
                    for (int tagNum = 0; tagNum < NumDebugTags; tagNum++)
                    {
                        GameplayTag gameplayTag = possibleTags[tagNum];
                        switch (Random.Range(0, 3))
                        {
                            case 0:
                                tagReqs.RequireTags.AddTag(gameplayTag);
                                break;
                            case 1:
                                tagReqs.IgnoreTags.AddTag(gameplayTag);
                                break;
                            case 2:
                            default:
                                break;
                        }
                    }
                }

                // 测试标签要求
                using (Measure.Scope("TagRequirements Test"))
                {
                    for (int i = 0; i < numTestsPerScenario; i++)
                    {
                        GameplayTagContainer testAgainstTags = possibleTagContainers[Random.Range(0, numPossibleContainers)];
                        if (tagReqs.RequirementsMet(testAgainstTags))
                        {
                            numMetRequirements++;
                        }
                    }
                }

                // 测试标签查询
                using (Measure.Scope("TagQuery Test"))
                {
                    GameplayTagQuery tagQuery = tagReqs.ConvertTagFieldsToTagQuery();
                    for (int i = 0; i < numTestsPerScenario; i++)
                    {
                        GameplayTagContainer testAgainstTags = possibleTagContainers[Random.Range(0, numPossibleContainers)];
                        if (tagQuery.Matches(testAgainstTags))
                        {
                            numQueryMatches++;
                        }
                    }
                }
            }

            // 输出结果
            Debug.Log($"Ran {NumTestScenarios * numTestsPerScenario} tests");
            Debug.Log($"TagRequirements took {metRequirementsTicks} ticks and gave {numMetRequirements} matches");
            Debug.Log($"TagQuery took {queryMatchesTicks} ticks and gave {numQueryMatches} matches");
        }
    }
}
