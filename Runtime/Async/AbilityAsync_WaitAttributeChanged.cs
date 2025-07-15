using UnityEngine;

namespace GameplayAbilities
{
    public class AbilityAsync_WaitAttributeChange : AbilityAsync
    {
        public delegate void AsyncWaitAttributeChangedDelegate(GameplayAttribute attribute, float newValue, float oldValue);
        public AsyncWaitAttributeChangedDelegate Changed;
        protected GameplayAttribute Attribute;
        protected bool OnlyTriggerOnce;

        protected override void Activate()
        {
            base.Activate();

            AbilitySystemComponent asc = AbilitySystemComponent;
            if (asc != null)
            {
                asc.GetGameplayAttributeValueChangeDelegate(Attribute).AddListener(OnAttributeChanged);
            }
            else
            {
                EndAction();
            }
        }

        public void OnAttributeChanged(OnAttributeChangeData changeData)
        {
            if (ShouldBroadcastDelegates)
            {
                Changed?.Invoke(Attribute, changeData.NewValue, changeData.OldValue);

                if (OnlyTriggerOnce)
                {
                    EndAction();
                }
            }
            else
            {
                EndAction();
            }
        }

        public static AbilityAsync_WaitAttributeChange WaitForAttributeChanged(GameObject targetActor, GameplayAttribute attribute, bool onlyTriggerOnce = true)
        {
            AbilityAsync_WaitAttributeChange async = new()
            {
                AbilityActor = targetActor,
                Attribute = attribute,
                OnlyTriggerOnce = onlyTriggerOnce,
            };

            return async;
        }

        public override void EndAction()
        {
            AbilitySystemComponent asc = AbilitySystemComponent;
            if (asc != null)
            {
                asc.GetGameplayAttributeValueChangeDelegate(Attribute).RemoveListener(OnAttributeChanged);
            }

            base.EndAction();
        }
    }
}
