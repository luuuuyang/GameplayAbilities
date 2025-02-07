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
        private class TestCallbacks
        {
            public bool ReceivedAbilityActivated;
            public bool ReceivedAbilityCommitted;
            public bool ReceivedAbilityFailed;
            public bool ReceivedAbilityEnded;

            private readonly AbilitySystemComponent abilitySystemComponent;
            private readonly GameplayAbility expectedAbility;

            public TestCallbacks(AbilitySystemComponent asc, GameplayAbility expected)
            {
                abilitySystemComponent = asc;
                expectedAbility = expected;

                // abilitySystemComponent.OnAbilityActivated.AddListener(OnAbilityActivated);
                // abilitySystemComponent.OnAbilityCommitted.AddListener(OnAbilityCommitted);
                // abilitySystemComponent.OnAbilityFailed.AddListener(OnAbilityFailed);
                // abilitySystemComponent.OnAbilityEnded.AddListener(OnAbilityEnded);
            }

            ~TestCallbacks()
            {
                // abilitySystemComponent.OnAbilityActivated.RemoveListener(OnAbilityActivated);
                // abilitySystemComponent.OnAbilityCommitted.RemoveListener(OnAbilityCommitted);
                // abilitySystemComponent.OnAbilityFailed.RemoveListener(OnAbilityFailed);
                // abilitySystemComponent.OnAbilityEnded.RemoveListener(OnAbilityEnded);
            }

            private void OnAbilityActivated(GameplayAbility ability)
            {
                Assert.AreEqual(expectedAbility, ability, "AbilityActivated with Expected GameplayAbility Instance");
                ReceivedAbilityActivated = true;
            }

            private void OnAbilityCommitted(GameplayAbility ability)
            {
                Assert.AreEqual(expectedAbility, ability, "AbilityCommitted with Expected GameplayAbility Instance");
                ReceivedAbilityCommitted = true;
            }

            private void OnAbilityFailed(GameplayAbility ability, GameplayTagContainer tags)
            {
                Assert.AreEqual(expectedAbility, ability, "AbilityFailed with Expected GameplayAbility Instance");
                ReceivedAbilityFailed = true;
            }

            private void OnAbilityEnded(GameplayAbility ability)
            {
                Assert.AreEqual(expectedAbility, ability, "AbilityEnded with Expected GameplayAbility Instance");
                ReceivedAbilityEnded = true;
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
            var abilitySpec = new GameplayAbilitySpec(new GameplayAbility(), 1, sourceGO);
            var givenHandle = sourceASC.GiveAbility(abilitySpec);
            var foundSpec = sourceASC.FindAbilitySpecFromHandle(givenHandle);

            // Verify ability was given
            var allAbilities = new List<GameplayAbilitySpecHandle>();
            sourceASC.GetAllAbilities(allAbilities);
            Assert.IsTrue(allAbilities.Count > 0, "GiveAbility");
            Assert.AreEqual(givenHandle, allAbilities[0], "GiveAbility - Correct handle");

            // Verify activatable abilities match
            var activatableAbilities = sourceASC.GetActivatableAbilities();
            Assert.AreEqual(allAbilities.Count, activatableAbilities.Count, "GetAllAbilities() count matches GetActivatableAbilities()");
            Assert.AreEqual(allAbilities[0], activatableAbilities[0].Handle, "GetAllAbilities() handle matches GetActivatableAbilities()");

            // Test activation flow
            var callbacks = new TestCallbacks(sourceASC, foundSpec.Ability);

            bool activated = sourceASC.TryActivateAbility(givenHandle);
            Assert.IsTrue(activated, "TryActivateAbility executes successfully");
            Assert.IsTrue(foundSpec.IsActive(), "AbilitySpec is active after activation");
            Assert.IsTrue(callbacks.ReceivedAbilityActivated, "Received AbilityActivated callback");
            Assert.IsFalse(callbacks.ReceivedAbilityCommitted, "No AbilityCommitted callback yet");
            Assert.IsFalse(callbacks.ReceivedAbilityEnded, "No AbilityEnded callback yet");
            Assert.IsFalse(callbacks.ReceivedAbilityFailed, "No AbilityFailed callback");

            // Test cancellation
            sourceASC.CancelAbilityHandle(givenHandle);
            Assert.IsTrue(callbacks.ReceivedAbilityEnded, "Received AbilityEnded after cancel");
            Assert.IsFalse(foundSpec.IsActive(), "AbilitySpec is inactive after cancel");

            yield return null;
        }

        [UnityTest]
        public IEnumerator Test_FailedAbilityFlow()
        {
            // Give ability to source
            var abilitySpec = new GameplayAbilitySpec(new GameplayAbility(), 1, sourceGO);
            var givenHandle = sourceASC.GiveAbility(abilitySpec);
            var foundSpec = sourceASC.FindAbilitySpecFromHandle(givenHandle);

            // Setup callbacks
            var callbacks = new TestCallbacks(sourceASC, foundSpec.Ability);

            // Inhibit activation
            sourceASC.SetUserAbilityActivationInhibited(true);

            // Try to activate
            bool activated = sourceASC.TryActivateAbility(givenHandle);
            Assert.IsFalse(activated, "TryActivateAbility fails when inhibited");
            Assert.IsFalse(foundSpec.IsActive(), "AbilitySpec remains inactive");
            Assert.IsFalse(callbacks.ReceivedAbilityActivated, "No AbilityActivated callback");
            Assert.IsFalse(callbacks.ReceivedAbilityCommitted, "No AbilityCommitted callback");
            Assert.IsFalse(callbacks.ReceivedAbilityEnded, "No AbilityEnded callback");
            Assert.IsTrue(callbacks.ReceivedAbilityFailed, "Received AbilityFailed callback");

            yield return null;
        }
    }
}
