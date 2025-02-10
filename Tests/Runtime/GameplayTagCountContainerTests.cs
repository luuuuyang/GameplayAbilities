using GameplayTags;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameplayAbilities.Tests
{
    public class GameplayTagCountContainerTests
    {
        static NativeGameplayTag TestsDotGenericTag = new NativeGameplayTag("GameplayTags", "Tests.GenericTag");
        static NativeGameplayTag TestsDotGenericTagDotOne = new NativeGameplayTag("GameplayTags", "Tests.GenericTag.One");
        static NativeGameplayTag TestsDotGenericTagDotTwo = new NativeGameplayTag("GameplayTags", "Tests.GenericTag.Two");

        [Test]
        public void TestTagCountContainer()
        {
            GameplayTagCountContainer tagCountContainer = new GameplayTagCountContainer();
            tagCountContainer.SetTagCount(TestsDotGenericTagDotOne, 1);

            GameplayTagContainer containerOne = new GameplayTagContainer(TestsDotGenericTagDotOne);
            Assert.IsTrue(tagCountContainer.HasAllMatchingGameplayTags(containerOne));
            Assert.IsTrue(tagCountContainer.HasAnyMatchingGameplayTags(containerOne));
            Assert.IsTrue(tagCountContainer.HasMatchingGameplayTag(TestsDotGenericTagDotOne));
            Assert.IsTrue(tagCountContainer.GetTagCount(TestsDotGenericTagDotOne) == 1);

            GameplayTagContainer containerTwo = new GameplayTagContainer(TestsDotGenericTagDotTwo);
            Assert.IsFalse(tagCountContainer.HasAllMatchingGameplayTags(containerTwo));
            Assert.IsFalse(tagCountContainer.HasAnyMatchingGameplayTags(containerTwo));
            Assert.IsFalse(tagCountContainer.HasMatchingGameplayTag(TestsDotGenericTagDotTwo));

            tagCountContainer.SetTagCount(TestsDotGenericTagDotTwo, 2);
            Assert.IsTrue(tagCountContainer.GetTagCount(TestsDotGenericTagDotOne) == 1);
            Assert.IsTrue(tagCountContainer.GetTagCount(TestsDotGenericTagDotTwo) == 2);
            Assert.IsTrue(tagCountContainer.GetTagCount(TestsDotGenericTag) == 3);

            tagCountContainer.UpdateTagCount(TestsDotGenericTagDotOne, -1);
            Assert.IsFalse(tagCountContainer.HasAllMatchingGameplayTags(containerOne));
            Assert.IsFalse(tagCountContainer.HasAnyMatchingGameplayTags(containerOne));
            Assert.IsTrue(tagCountContainer.GetTagCount(TestsDotGenericTagDotOne) == 0);
            Assert.IsTrue(tagCountContainer.GetTagCount(TestsDotGenericTagDotTwo) == 2);
            Assert.IsTrue(tagCountContainer.GetTagCount(TestsDotGenericTag) == 2);

            Assert.IsTrue(tagCountContainer.GetExplicitTagCount(TestsDotGenericTag) == 0);
            Assert.IsTrue(tagCountContainer.GetExplicitTagCount(TestsDotGenericTagDotOne) == 0);
            Assert.IsTrue(tagCountContainer.GetExplicitTagCount(TestsDotGenericTagDotTwo) == 2);

            GameplayTagContainer explicitTags = tagCountContainer.ExplicitTags;
            Assert.IsFalse(explicitTags.HasTagExact(TestsDotGenericTag));
            Assert.IsFalse(explicitTags.HasTagExact(TestsDotGenericTagDotOne));
            Assert.IsTrue(explicitTags.HasTagExact(TestsDotGenericTagDotTwo));
        }
    }
}