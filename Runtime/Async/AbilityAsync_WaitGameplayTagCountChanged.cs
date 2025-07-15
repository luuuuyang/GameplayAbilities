using GameplayTags;
using UnityEngine;

namespace GameplayAbilities
{
    public class AbilityAsync_WaitGameplayTagCountChanged : AbilityAsync
    {
        public delegate void AsyncWaitGameplayTagCountDelegate(int tagCount);
        public AsyncWaitGameplayTagCountDelegate TagCountChanged;
        protected GameplayTag Tag;

        protected override void Activate()
        {
            base.Activate();

            AbilitySystemComponent asc = AbilitySystemComponent;
            if (asc != null && ShouldBroadcastDelegates)
            {
                asc.RegisterGameplayTagEvent(Tag, GameplayTagEventType.AnyCountChange).AddListener(GameplayTagCallback);
            }
            else
            {
                EndAction();
            }
        }

        public override void EndAction()
        {
            AbilitySystemComponent asc = AbilitySystemComponent;
            if (asc != null)
            {
                asc.UnregisterGameplayTagEvent(GameplayTagCallback, Tag, GameplayTagEventType.AnyCountChange);
            }

            base.EndAction();
        }

        protected virtual void GameplayTagCallback(GameplayTag tag, int newCount)
        {
            if (ShouldBroadcastDelegates)
            {
                TagCountChanged?.Invoke(newCount);
            }
            else
            {
                EndAction();
            }
        }

        public static AbilityAsync_WaitGameplayTagCountChanged WaitGameplayTagCountChanged(GameObject targetActor, GameplayTag tag, int targetCount)
        {
            AbilityAsync_WaitGameplayTagCountChanged async = new()
            {
                AbilityActor = targetActor,
                Tag = tag,
            };

            return async;
        }
    }
}
