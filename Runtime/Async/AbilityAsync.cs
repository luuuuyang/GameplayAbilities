using System;
using System.Threading;
using UnityEngine;

namespace GameplayAbilities
{
    public abstract class AbilityAsync : IDisposable
    {
        public CancellationToken CancellationToken => CancellationTokenSource.Token;
        protected CancellationTokenSource CancellationTokenSource;
        private WeakReference<AbilitySystemComponent> abilitySystemComponent;

        protected virtual void Activate()
        {
            CancellationTokenSource = new CancellationTokenSource();
        }

        public virtual void Cancel()
        {
            EndAction();
            CancellationTokenSource?.Cancel();
        }

        public virtual void EndAction()
        {
            abilitySystemComponent = null;

            OnDestroy();
        }

        protected virtual void OnDestroy()
        {
            CancellationTokenSource?.Dispose();
            CancellationTokenSource = null;
        }

        public void Dispose()
        {
            Cancel();
        }

        public virtual bool ShouldBroadcastDelegates
        {
            get
            {
                if (abilitySystemComponent != null)
                {
                    return true;
                }

                return false;
            }
        }

        public virtual AbilitySystemComponent AbilitySystemComponent
        {
            get
            {
                if (abilitySystemComponent != null)
                {
                    return abilitySystemComponent.TryGetTarget(out AbilitySystemComponent asc) ? asc : null;
                }

                return null;
            }
            set
            {
                abilitySystemComponent = new WeakReference<AbilitySystemComponent>(value);
            }
        }

        public virtual GameObject AbilityActor
        {
            set
            {
                AbilitySystemComponent = AbilitySystemGlobals.GetAbilitySystemComponentFromActor(value);
            }
        }
    }

}
