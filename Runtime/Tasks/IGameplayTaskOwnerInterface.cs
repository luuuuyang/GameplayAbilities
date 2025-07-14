using UnityEngine;

namespace GameplayAbilities
{
    public interface IGameplayTaskOwnerInterface
    {
        abstract GameplayTasksComponent GetGameplayTasksComponent(in GameplayTask task);
        abstract GameObject GetGameplayTaskOwner(in GameplayTask task);

        virtual GameObject GetGameplayTaskAvatar(in GameplayTask task)
        {
            return GetGameplayTaskOwner(task);
        }

        public virtual byte GameplayTaskDefaultPriority => GameplayTask.DefaultPriority;

        virtual void OnGameplayTaskInitialized(GameplayTask task)
        {

        }

        virtual void OnGameplayTaskActivated(GameplayTask task)
        {

        }

        virtual void OnGameplayTaskDeactivated(GameplayTask task)
        {

        }
    }
}
