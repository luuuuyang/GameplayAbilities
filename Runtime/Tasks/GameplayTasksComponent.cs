using System.Collections.Generic;
using UnityEngine;

namespace GameplayAbilities
{
    public enum GameplayTaskEvent : byte
    {
        Add,
        Remove,
    }

    public enum GameplayTaskRunResult : byte
    {
        Error,
        Failed,
        SuccessPaused,
        SuccessActive,
        SuccessFinished,
    }

    public struct GameplayTaskEventData
    {
        public GameplayTaskEvent Event;
        public GameplayTask RelatedTask;

        public GameplayTaskEventData(GameplayTaskEvent @event, GameplayTask relatedTask)
        {
            Event = @event;
            RelatedTask = relatedTask;
        }
    }

    public delegate void OnClaimedResourcesChangedSignature(GameplayResourceSet newClaimedSet);

    public class GameplayTasksComponent : MonoBehaviour, IGameplayTaskOwnerInterface
    {
        public OnClaimedResourcesChangedSignature OnClaimedResourcesChanged;

        protected List<GameplayTask> taskPriorityQueue = new List<GameplayTask>();
        protected List<GameplayTaskEventData> taskEvents = new List<GameplayTaskEventData>();
        protected List<GameplayTask> tickingTasks = new List<GameplayTask>();
        protected List<GameplayTask> knownTasks = new List<GameplayTask>();

        public virtual bool ShouldTick => tickingTasks.Count > 0;

        private bool CanProcessEvents => !InEventProcessingInProgress && EventLockCounter == 0;

        private struct EventLock
        {
            private GameplayTasksComponent Owner;

            public EventLock(GameplayTasksComponent owner)
            {
                Owner = owner;
            }
        }

        private int EventLockCounter;
        private bool InEventProcessingInProgress;
        protected byte TopActivePriority;
        protected GameplayResourceSet CurrentlyClaimedResources;

        public GameObject GetGameplayTaskOwner(in GameplayTask task)
        {
            throw new System.NotImplementedException();
        }

        public GameplayTasksComponent GetGameplayTasksComponent(in GameplayTask task)
        {
            throw new System.NotImplementedException();
        }

        public virtual void OnGameplayTaskInitialized(GameplayTask task)
        {

        }

        public virtual void OnGameplayTaskActivated(GameplayTask task)
        {
            knownTasks.Add(task);

            if (task.IsTickingTask)
            {
                tickingTasks.Add(task);

                if (tickingTasks.Count == 1)
                {
                    UpdateShouldTick();
                }
            }

            IGameplayTaskOwnerInterface taskOwner = task.TaskOwner;
            if (!task.IsOwnedByTasksComponent && taskOwner != null)
            {
                taskOwner.OnGameplayTaskActivated(task);
            }
        }

        public virtual void OnGameplayTaskDeactivated(GameplayTask task)
        {
            bool IsFinished = task.TaskState == GameplayTaskState.Finished;

            if (task.ChildTask != null && IsFinished)
            {
                if (task.HasOwnerFinished)
                {
                    task.ChildTask.TaskOwnerEnded();
                }
                else
                {
                    task.ChildTask.EndTask();
                }
            }

            if (task.IsTickingTask)
            {
                tickingTasks.Remove(task);
            }

            if (IsFinished)
            {
                knownTasks.Remove(task);
            }

            if (task.RequiresPriorityOrResourceManagement && IsFinished)
            {
                OnTaskEnded(task);
            }

            IGameplayTaskOwnerInterface taskOwner = task.TaskOwner;
            if (!task.IsOwnedByTasksComponent && taskOwner != null)
            {
                taskOwner.OnGameplayTaskDeactivated(task);
            }

            UpdateShouldTick();
        }

        private void OnTaskEnded(GameplayTask task)
        {
            RemoveResourceConsumingTask(task);
        }

        public void RemoveResourceConsumingTask(GameplayTask task)
        {
            taskEvents.Add(new GameplayTaskEventData(GameplayTaskEvent.Remove, task));
            if (taskEvents.Count == 1 && CanProcessEvents)
            {
                ProcessTaskEvents();
            }
        }

        protected void ProcessTaskEvents()
        {
            const int MaxIterations = 16;
            InEventProcessingInProgress = true;

            int iterCounter = 0;
            while (taskEvents.Count > 0)
            {
                iterCounter++;
                if (iterCounter > MaxIterations)
                {
                    Debug.LogError("GameplayTasksComponent: ProcessTaskEvents: Max iterations reached");
                    taskEvents.Clear();
                    break;
                }

                for (int eventIndex = 0; eventIndex < taskEvents.Count; eventIndex++)
                {
                    if (taskEvents[eventIndex].RelatedTask == null)
                    {
                        RemoveTaskFromPriorityQueue(taskEvents[eventIndex].RelatedTask);
                        continue;
                    }

                    switch (taskEvents[eventIndex].Event)
                    {
                        case GameplayTaskEvent.Add:
                            if (taskEvents[eventIndex].RelatedTask.TaskState != GameplayTaskState.Finished)
                            {
                                AddTaskToPriorityQueue(taskEvents[eventIndex].RelatedTask);
                            }
                            else
                            {
                                Debug.LogError("GameplayTasksComponent: ProcessTaskEvents: Task is finished, but was added to the priority queue");
                            }
                            break;
                        case GameplayTaskEvent.Remove:
                            RemoveTaskFromPriorityQueue(taskEvents[eventIndex].RelatedTask);
                            break;
                        default:
                            break;
                    }
                }

                taskEvents.Clear();
                UpdateTaskActivation();
            }

            InEventProcessingInProgress = false;
        }

        protected void UpdateTaskActivation()
        {
            GameplayResourceSet resourcesClaimed = new();
            bool hasNulls = false;

            if (taskPriorityQueue.Count > 0)
            {
                List<GameplayTask> activationList = new List<GameplayTask>(taskPriorityQueue.Count);

                GameplayResourceSet resourcesBlocked = new();
                for (int taskIndex = 0; taskIndex < taskPriorityQueue.Count; taskIndex++)
                {
                    if (taskPriorityQueue[taskIndex] != null)
                    {
                        GameplayResourceSet requiredResources = taskPriorityQueue[taskIndex].RequiredResources;
                        GameplayResourceSet claimedResources = taskPriorityQueue[taskIndex].ClaimedResources;
                        if (requiredResources.GetOverlap(resourcesBlocked).IsEmpty)
                        {
                            activationList.Add(taskPriorityQueue[taskIndex]);
                            resourcesClaimed.AddSet(claimedResources);
                        }
                        else
                        {
                            taskPriorityQueue[taskIndex].PauseInTaskQueue();
                        }

                        resourcesBlocked.AddSet(claimedResources);
                    }
                    else
                    {
                        hasNulls = true;
                        Debug.LogWarning("GameplayTasksComponent: UpdateTaskActivation: Task is null in the priority queue");
                    }
                }

                for (int idx = 0; idx < activationList.Count; idx++)
                {
                    if (activationList[idx] != null && !activationList[idx].IsFinished)
                    {
                        activationList[idx].ActivateInTaskQueue();
                    }
                }

                SetCurrentClaimedResources(resourcesClaimed);

                if (hasNulls)
                {
                    taskPriorityQueue.RemoveAll(task => task == null);
                }
            }
        }

        protected void SetCurrentClaimedResources(GameplayResourceSet newClaimedSet)
        {
            if (CurrentlyClaimedResources != newClaimedSet)
            {
                
                CurrentlyClaimedResources = newClaimedSet;
                OnClaimedResourcesChanged(CurrentlyClaimedResources);
            }
        }

        protected void UpdateShouldTick()
        {
            bool shouldTick = ShouldTick;
            if (gameObject.activeSelf != shouldTick)
            {
                gameObject.SetActive(shouldTick);
            }
        }

        public void AddTaskReadyForActivation(GameplayTask newTask)
        {
            taskEvents.Add(new GameplayTaskEventData(GameplayTaskEvent.Add, newTask));

            if (taskEvents.Count == 1 && CanProcessEvents)
            {
                ProcessTaskEvents();
            }
        }


        protected virtual void Update()
        {
            int numTickingTasks = tickingTasks.Count;
            int numActuallyTicked = 0;

            switch (numTickingTasks)
            {
                case 0:
                    break;
                case 1:
                    {
                        GameplayTask tickingTask = tickingTasks[0];
                        if (tickingTask != null)
                        {
                            tickingTask.TickTask(Time.deltaTime);
                            numActuallyTicked++;
                        }
                    }
                    break;
                default:
                    {
                        List<GameplayTask> localTickingTasks = new List<GameplayTask>(tickingTasks);
                        foreach (GameplayTask tickingTask in localTickingTasks)
                        {
                            if (tickingTask != null)
                            {
                                tickingTask.TickTask(Time.deltaTime);
                                numActuallyTicked++;
                            }
                        }
                    }
                    break;
            }

            if (numActuallyTicked == 0)
            {
                tickingTasks.Clear();
                UpdateShouldTick();
            }
        }

        private void AddTaskToPriorityQueue(GameplayTask newTask)
        {
            if (newTask.ResourceOverlapPolicy == TaskResourceOverlapPolicy.RequestCancelAndStartOnTop
                || newTask.ResourceOverlapPolicy == TaskResourceOverlapPolicy.RequestCancelAndStartAtEnd)
            {
                GameplayResourceSet newClaimedResources = newTask.ClaimedResources;
                List<GameplayTask> cancelList = new List<GameplayTask>();

                foreach (GameplayTask task in taskPriorityQueue)
                {
                    if (task != null && task.Priority <= newTask.Priority && task.ClaimedResources.HasAnyId(newClaimedResources))
                    {
                        cancelList.Add(task);
                    }
                }

                foreach (GameplayTask task in cancelList)
                {
                    task.ExternalCancel();
                }
            }

            bool startOnTopOfSamePriority = newTask.ResourceOverlapPolicy == TaskResourceOverlapPolicy.StartOnTop
                                            || newTask.ResourceOverlapPolicy == TaskResourceOverlapPolicy.RequestCancelAndStartOnTop;
            int insertionPoint = -1;

            for (int idx = 0; idx < taskPriorityQueue.Count; idx++)
            {
                if (taskPriorityQueue[idx] == null)
                {
                    continue;
                }

                if (startOnTopOfSamePriority && taskPriorityQueue[idx].Priority <= newTask.Priority
                    || !startOnTopOfSamePriority && taskPriorityQueue[idx].Priority < newTask.Priority)
                {
                    taskPriorityQueue.Insert(idx, newTask);
                    insertionPoint = idx;
                    break;
                }
            }

            if (insertionPoint == -1)
            {
                taskPriorityQueue.Add(newTask);
            }
        }

        private void RemoveTaskFromPriorityQueue(GameplayTask task)
        {
            int removedTaskIndex = taskPriorityQueue.IndexOf(task);
            if (removedTaskIndex != -1)
            {
                taskPriorityQueue.RemoveAt(removedTaskIndex);
            }
            else
            {
                Debug.LogWarning("GameplayTasksComponent: RemoveTaskFromPriorityQueue: Task not found in the priority queue");
            }
        }

        public static GameplayTaskRunResult RunGameplayTask(IGameplayTaskOwnerInterface taskOwner, GameplayTask task, byte priority, GameplayResourceSet additionalRequiredResources, GameplayResourceSet additionalClaimedResources)
        {
            if (task.TaskState == GameplayTaskState.Paused || task.TaskState == GameplayTaskState.Active)
            {
                return task.TaskOwner == taskOwner
                    ? (task.TaskState == GameplayTaskState.Paused ? GameplayTaskRunResult.SuccessPaused : GameplayTaskRunResult.SuccessActive)
                    : GameplayTaskRunResult.Error;
            }

            if (task.TaskState == GameplayTaskState.Uninitialized)
            {
                task.InitTask(taskOwner, priority);
            }

            task.AddRequiredResources(additionalRequiredResources);
            task.AddClaimedResources(additionalClaimedResources);
            task.ReadyForActivation();

            switch (task.TaskState)
            {
                case GameplayTaskState.AwaitingActivation:
                case GameplayTaskState.Paused:
                    return GameplayTaskRunResult.SuccessPaused;
                case GameplayTaskState.Active:
                    return GameplayTaskRunResult.SuccessActive;
                case GameplayTaskState.Finished:
                    return GameplayTaskRunResult.SuccessActive;
            }

            return GameplayTaskRunResult.Error;
        }

    }
}