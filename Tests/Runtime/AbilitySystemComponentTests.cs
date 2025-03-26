using System.Collections;
using System.Collections.Generic;
using GameplayTags;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameplayAbilities.Tests
{
    public class AbilitySystemComponentTests
    {
        private class TestAllAbilitySystemComponentCallbacks
        {
            public bool ReceivedAbilityActivated;
            public bool ReceivedAbilityCommitted;
            public bool ReceivedAbilityFailed;
            public bool ReceivedAbilityEnded;

            private readonly AbilitySystemComponent AbilitySystemComponent;

            public TestAllAbilitySystemComponentCallbacks(AbilitySystemComponent abilitySystemComponent, GameplayAbility expectedAbility)
            {
                AbilitySystemComponent = abilitySystemComponent;

                abilitySystemComponent.AbilityActivatedCallbacks += (GameplayAbility ability) =>
                {
                    bool isCorrectAbility = IsSameAbility(ability, expectedAbility);
                    Assert.IsTrue(isCorrectAbility, " AbilityActivatedCallbacks with Expected GameplayAbility Instance");
                    ReceivedAbilityActivated = true;
                };
                
                abilitySystemComponent.AbilityCommittedCallbacks += (GameplayAbility ability) =>
                {
                    bool isCorrectAbility = IsSameAbility(ability, expectedAbility);
                    Assert.IsTrue(isCorrectAbility, " AbilityCommittedCallbacks with Expected GameplayAbility Instance");
                    ReceivedAbilityCommitted = true;
                };

                abilitySystemComponent.AbilityFailedCallbacks += (in GameplayAbility ability, in GameplayTagContainer tags) =>
                {
                    bool isCorrectAbility = IsSameAbility(ability, expectedAbility);
                    Assert.IsTrue(isCorrectAbility, " AbilityFailedCallbacks with Expected GameplayAbility Instance");
                    ReceivedAbilityFailed = true;
                };

                abilitySystemComponent.AbilityEndedCallbacks += (in GameplayAbility ability) =>
                {
                    bool isCorrectAbility = IsSameAbility(ability, expectedAbility);
                    Assert.IsTrue(isCorrectAbility, " AbilityEndedCallbacks with Expected GameplayAbility Instance");
                    ReceivedAbilityEnded = true;
                };
            }

            public static bool IsSameAbility(GameplayAbility ability, GameplayAbility expectedAbility)
            {
                if (expectedAbility.InstancingPolicy == GameplayAbilityInstancingPolicy.NonInstanced)
                {
                    return ability == expectedAbility;
                }
                return true;
            }
        }

        private GameObject sourceGO;
        private GameObject destGO;
        private AbilitySystemComponent sourceASC;
        private AbilitySystemComponent destASC;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            // Setup source
            sourceGO = new GameObject("Source");
            sourceASC = sourceGO.AddComponent<AbilitySystemComponent>();

            // Setup destination
            destGO = new GameObject("Destination");
            destASC = destGO.AddComponent<AbilitySystemComponent>();

            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Teardown()
        {
            UnityEngine.Object.Destroy(sourceGO);
            UnityEngine.Object.Destroy(destGO);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Test_ActivateAbilityFlow()
        {
            // Give ability to source
            var tempAbilitySpec = new GameplayAbilitySpec(ScriptableObject.CreateInstance<GameplayAbility>(), 1);
            var givenAbilitySpecHandle = sourceASC.GiveAbility(tempAbilitySpec);
            var abilitySpec = sourceASC.FindAbilitySpecFromHandle(givenAbilitySpecHandle);

            // Verify ability was given
            sourceASC.GetAllAbilities(out List<GameplayAbilitySpecHandle> gameplayAbilitySpecHandles);
            bool hasAbility = gameplayAbilitySpecHandles.Count > 0;
            bool hasCorrectAbility = hasAbility && gameplayAbilitySpecHandles[0] == givenAbilitySpecHandle;
            Assert.IsTrue(hasCorrectAbility, "GiveAbility");

            // Verify activatable abilities match
            var activatableAbilities = sourceASC.GetActivatableAbilities();
            bool hasActivatableAbilities = activatableAbilities.Count > 0;
            bool bothArraysMatch = hasAbility && hasActivatableAbilities && gameplayAbilitySpecHandles[0] == activatableAbilities[0].Handle;
            Assert.IsTrue(bothArraysMatch, "GetAllAbilities() == GetActivatableAbilities().Handle");

            // Test activation flow
            var testCallbacks = new TestAllAbilitySystemComponentCallbacks(sourceASC, abilitySpec.Ability);

            bool localActivation = sourceASC.TryActivateAbility(givenAbilitySpecHandle);
            Assert.IsTrue(localActivation, "TryActivateAbility executes successfully (using FGameplayAbilitySpecHandle)");
            Assert.IsTrue(abilitySpec.IsActive, " AbilitySpec.IsActive() after TryActivateAbility (using FGameplayAbilitySpecHandle)");
            Assert.IsTrue(testCallbacks.ReceivedAbilityActivated, " AbilityActivated after TryActivateAbility (using FGameplayAbilitySpecHandle)");
            Assert.IsFalse(testCallbacks.ReceivedAbilityCommitted, " AbilityCommitted after TryActivateAbility (using FGameplayAbilitySpecHandle)");
            Assert.IsFalse(testCallbacks.ReceivedAbilityEnded, " AbilityEnded (prematurely) after TryActivateAbility (using FGameplayAbilitySpecHandle)");
            Assert.IsFalse(testCallbacks.ReceivedAbilityFailed, " AbilityFailed after TryActivateAbility (with an Ability that should succeed)");
            // Test cancellation
            sourceASC.CancelAbilityHandle(givenAbilitySpecHandle);
            Assert.IsTrue(testCallbacks.ReceivedAbilityEnded, " AbilityEnded (after CancelAbilityHandle)");
            Assert.IsFalse(abilitySpec.IsActive, " AbilitySpec.IsActive() (after CancelAbilityHandle)");

            yield return null;
        }

        [UnityTest]
        public IEnumerator Test_FailedAbilityFlow()
        {
            // Give ability to source
            var tempAbilitySpec = new GameplayAbilitySpec(ScriptableObject.CreateInstance<GameplayAbility>(), 1);
            var givenAbilitySpecHandle = sourceASC.GiveAbility(tempAbilitySpec);
            var abilitySpec = sourceASC.FindAbilitySpecFromHandle(givenAbilitySpecHandle);

            sourceASC.GetAllAbilities(out List<GameplayAbilitySpecHandle> gameplayAbilitySpecHandles);
            bool hasAbility = gameplayAbilitySpecHandles.Count > 0;
            bool hasCorrectAbility = hasAbility && gameplayAbilitySpecHandles[0] == givenAbilitySpecHandle;
            Assert.IsTrue(hasCorrectAbility, "GiveAbility");

            var activatableAbilities = sourceASC.GetActivatableAbilities();
            bool hasActivatableAbilities = activatableAbilities.Count > 0;
            bool bothArraysMatch = hasAbility && hasActivatableAbilities && gameplayAbilitySpecHandles[0] == activatableAbilities[0].Handle;
            Assert.IsTrue(bothArraysMatch, "GetAllAbilities() == GetActivatableAbilities().Handle");
            // Setup callbacks
            var testCallbacks = new TestAllAbilitySystemComponentCallbacks(sourceASC, abilitySpec.Ability);

            // Inhibit activation
            sourceASC.SetUserAbilityActivationInhibited(true);

            // Try to activate
            bool activated = sourceASC.TryActivateAbility(givenAbilitySpecHandle);
            Assert.IsFalse(activated, "TryActivateAbility fails (using FGameplayAbilitySpecHandle)");
            Assert.IsFalse(abilitySpec.IsActive, " AbilitySpec.IsActive() after TryActivateAbility (using FGameplayAbilitySpecHandle)");
            Assert.IsFalse(testCallbacks.ReceivedAbilityActivated, " AbilityActivated after TryActivateAbility (using FGameplayAbilitySpecHandle)");
            Assert.IsFalse(testCallbacks.ReceivedAbilityCommitted, " AbilityCommitted after TryActivateAbility (using FGameplayAbilitySpecHandle)");
            Assert.IsFalse(testCallbacks.ReceivedAbilityEnded, " AbilityEnded (prematurely) after TryActivateAbility (using FGameplayAbilitySpecHandle)");
            Assert.IsTrue(testCallbacks.ReceivedAbilityFailed, " AbilityFailed after TryActivateAbility (with an Ability that should fail)");

            yield return null;
        }
    }
}
