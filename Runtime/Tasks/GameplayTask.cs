using System;

namespace GameplayAbilities
{
    public enum GameplayTaskState : byte
    {
        Uninitialized,
        AwaitingActivation,
        Paused,
        Active,
        Finished,
    }

    public enum TaskResourceOverlapPolicy : byte
    {
        StartOnTop,
        StartAtEnd,
        RequestCancelAndStartOnTop,
        RequestCancelAndStartAtEnd,
    }

    public struct GameplayResourceSet
    {
        public ushort Flags { get; private set; }
        public readonly bool IsEmpty => Flags == 0;
        public GameplayResourceSet AddId(byte resourceId)
        {
            Flags |= (ushort)(1 << resourceId);
            return this;
        }

        public GameplayResourceSet RemoveId(byte resourceId)
        {
            Flags &= (ushort)~(1 << resourceId);
            return this;
        }

        public bool HasId(byte resourceId)
        {
            return (Flags & (ushort)(1 << resourceId)) != 0;
        }

        public GameplayResourceSet AddSet(GameplayResourceSet other)
        {
            Flags |= other.Flags;
            return this;
        }

        public GameplayResourceSet RemoveSet(GameplayResourceSet other)
        {
            Flags &= (ushort)~other.Flags;
            return this;
        }

        public void Clear()
        {
            Flags = 0;
        }

        public bool HasAllId(GameplayResourceSet other)
        {
            return (Flags & other.Flags) == other.Flags;
        }

        public bool HasAnyId(GameplayResourceSet other)
        {
            return (Flags & other.Flags) != 0;
        }

        public GameplayResourceSet GetOverlap(GameplayResourceSet other)
        {
            return new GameplayResourceSet { Flags = (ushort)(Flags & other.Flags) };
        }

        public GameplayResourceSet GetUnion(GameplayResourceSet other)
        {
            return new GameplayResourceSet { Flags = (ushort)(Flags | other.Flags) };
        }

        public GameplayResourceSet GetDifference(GameplayResourceSet other)
        {
            return new GameplayResourceSet { Flags = (ushort)(Flags & ~other.Flags) };
        }

        public static bool operator ==(GameplayResourceSet a, GameplayResourceSet b)
        {
            return a.Flags == b.Flags;
        }

        public static bool operator !=(GameplayResourceSet a, GameplayResourceSet b)
        {
            return a.Flags != b.Flags;
        }

        public override bool Equals(object obj)
        {
            return obj is GameplayResourceSet set && Flags == set.Flags;
        }

        public static GameplayResourceSet AllResources => new GameplayResourceSet { Flags = 0xFFFF };
        public static GameplayResourceSet NoResources => new GameplayResourceSet { Flags = 0 };

        public static GameplayResourceSet operator &(GameplayResourceSet a, GameplayResourceSet b)
        {
            return new GameplayResourceSet { Flags = (ushort)(a.Flags & b.Flags) };
        }

    }

    public class GameplayTask
    {
        public const byte DefaultPriority = 127;
        public const byte ScriptedPriority = 192;

        public string InstanceName { get; protected set; }
        public byte Priority { get; protected set; }
        public GameplayTaskState TaskState { get; protected set; }
        public TaskResourceOverlapPolicy ResourceOverlapPolicy { get; protected set; }
        public bool IsTickingTask { get; protected set; }
        public IGameplayTaskOwnerInterface TaskOwner
        {
            get
            {
                if (taskOwner.TryGetTarget(out IGameplayTaskOwnerInterface owner))
                {
                    return owner;
                }
                return null;
            }
        }
        public GameplayResourceSet RequiredResources { get; protected set; }
        public GameplayResourceSet ClaimedResources { get; protected set; }
        public bool IsActive => TaskState == GameplayTaskState.Active;
        public bool IsPaused => TaskState == GameplayTaskState.Paused;
        public bool IsFinished => TaskState == GameplayTaskState.Finished;
        protected WeakReference<IGameplayTaskOwnerInterface> taskOwner;
        protected WeakReference<GameplayTasksComponent> TasksComponent;
        public GameplayTask ChildTask { get; protected set; }
        public bool IsPausable { get; protected set; }
        public bool RequiresPriorityOrResourceManagement => CaresAboutPriority || !RequiredResources.IsEmpty || !ClaimedResources.IsEmpty;
        public bool HasOwnerFinished { get; protected set; }
        public bool IsOwnedByTasksComponent { get; protected set; }
        protected bool CaresAboutPriority;
        protected bool ClaimRequiredResources;

        public static T NewTask<T>(IGameplayTaskOwnerInterface taskOwner, string instanceName) where T : GameplayTask, new()
        {
            T newTask = new()
            {
                InstanceName = instanceName
            };
            newTask.InitTask(taskOwner, taskOwner.GameplayTaskDefaultPriority);
            return newTask;
        }

        public void TaskOwnerEnded()
        {
            if (TaskState != GameplayTaskState.Finished)
            {
                HasOwnerFinished = true;
                if (this != null)
                {
                    OnDestroy(true);
                }
                else
                {
                    TaskState = GameplayTaskState.Finished;
                }
            }
        }

        public virtual void TickTask(float deltaTime)
        {

        }

        public void EndTask()
        {
            if (TaskState != GameplayTaskState.Finished)
            {
                if (this != null)
                {
                    OnDestroy(false);
                }
                else
                {
                    TaskState = GameplayTaskState.Finished;
                }
            }
        }

        protected virtual void Activate()
        {

        }

        internal void InitTask(IGameplayTaskOwnerInterface taskOwner, byte priority)
        {
            Priority = priority;
            // TaskOwner = taskOwner;  
            TaskState = GameplayTaskState.AwaitingActivation;

            if (ClaimRequiredResources)
            {
                ClaimedResources.AddSet(RequiredResources);
            }

            taskOwner.OnGameplayTaskInitialized(this);

            GameplayTasksComponent GTComponent = taskOwner.GetGameplayTasksComponent(this);
            TasksComponent = new WeakReference<GameplayTasksComponent>(GTComponent);
            IsOwnedByTasksComponent = TaskOwner.GetGameplayTasksComponent(this) == GTComponent;

            if (GTComponent != null && !IsOwnedByTasksComponent)
            {
                GTComponent.OnGameplayTaskInitialized(this);
            }
        }

        public virtual void ExternalConfirm(bool endTask)
        {
            if (endTask)
            {
                EndTask();
            }
        }

        public virtual void ExternalCancel()
        {
            EndTask();
        }

        protected virtual void OnDestroy(bool ownerFinished)
        {
            TaskState = GameplayTaskState.Finished;

            if (TasksComponent.TryGetTarget(out GameplayTasksComponent tasksComponent))
            {
                tasksComponent.OnGameplayTaskDeactivated(this);
            }
        }

        protected virtual void Pause()
        {
            TaskState = GameplayTaskState.Paused;

            if (TasksComponent.TryGetTarget(out GameplayTasksComponent tasksComponent))
            {
                tasksComponent.OnGameplayTaskDeactivated(this);
            }
        }

        protected virtual void Resume()
        {
            TaskState = GameplayTaskState.Active;

            if (TasksComponent.TryGetTarget(out GameplayTasksComponent tasksComponent))
            {
                tasksComponent.OnGameplayTaskActivated(this);
            }
        }

        internal void ActivateInTaskQueue()
        {
            switch (TaskState)
            {
                case GameplayTaskState.Uninitialized:
                    break;
                case GameplayTaskState.AwaitingActivation:
                    PerformActivation();
                    break;
                case GameplayTaskState.Paused:
                    Resume();
                    break;
                case GameplayTaskState.Active:
                    break;
                case GameplayTaskState.Finished:
                    PerformActivation();
                    break;
                default:
                    break;
            }
        }

        internal void PauseInTaskQueue()
        {
            switch (TaskState)
            {
                case GameplayTaskState.Uninitialized:
                    break;
                case GameplayTaskState.AwaitingActivation:
                    break;
                case GameplayTaskState.Paused:
                    break;
                case GameplayTaskState.Active:
                    Pause();
                    break;
                case GameplayTaskState.Finished:
                    break;
                default:
                    break;
            }
        }

        private void PerformActivation()
        {
            if (TaskState == GameplayTaskState.Active)
            {
                return;
            }

            TaskState = GameplayTaskState.Active;

            Activate();

            if (!IsFinished)
            {
                if (TasksComponent.TryGetTarget(out GameplayTasksComponent tasksComponent))
                {
                    tasksComponent.OnGameplayTaskActivated(this);
                }
            }
        }

        public void AddRequiredResources(GameplayResourceSet requiredResourceSet)
        {
            RequiredResources.AddSet(requiredResourceSet);
        }

        public void AddClaimedResources(GameplayResourceSet claimedResourceSet)
        {
            ClaimedResources.AddSet(claimedResourceSet);
        }

        public void ReadyForActivation()
        {
            if (TasksComponent.TryGetTarget(out GameplayTasksComponent tasksComponent))
            {
                if (!RequiresPriorityOrResourceManagement)
                {
                    PerformActivation();
                }
                else
                {
                    tasksComponent.AddTaskReadyForActivation(this);
                }
            }
            else
            {
                EndTask();
            }
        }
    }
}
