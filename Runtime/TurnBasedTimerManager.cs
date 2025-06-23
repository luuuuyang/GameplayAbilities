using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace GameplayAbilities
{
    using TimerFunction = Action;

    using TimerDynamicDelegate = UnityAction;

    public static class TimerHandleGenerator
    {
        private static ulong LastAssignedSerialNumber = 0;

        public static TimerHandle GenerateHandle(int index)
        {
            ulong newSerialNumber = ++LastAssignedSerialNumber;
            if (newSerialNumber == TimerHandle.MaxSerialNumber)
            {
                newSerialNumber = 1;
            }

            TimerHandle result = new();
            result.SetIndexAndSerialNumber(index, newSerialNumber);
            return result;
        }
    }

    public class TurnBasedTimerData
    {
        public bool Loop;
        public bool MaxOncePerTurn;
        public bool RequiresDelegate;
        public TimerStatus Status;
        public int Rate;
        public int ExpireTime;
        public TimerUnifiedDelegate TimerDelegate;
        public TimerHandle Handle;
        public object TimerIndicesByObjectKey;
    }

    public struct TurnBasedTimerManagerTimerParameters
    {
        public bool Loop;
        public bool MaxOncePerTurn;
        public int FirstDelay;

        public TurnBasedTimerManagerTimerParameters(bool loop, bool maxOncePerTurn, int firstDelay = -1)
        {
            Loop = loop;
            MaxOncePerTurn = maxOncePerTurn;
            FirstDelay = firstDelay;
        }
    };

    public class TurnBasedTimerManager : MonoBehaviour, ITimerManager
    {
        public static TurnBasedTimerManager Instance { get; private set; }

        [SerializeField] private bool dontDestroyOnLoad = true;

        private SparseArray<TurnBasedTimerData> Timers = new();
        private PriorityQueue<TimerHandle, int> ActiveTimerHeap = new();
        private HashSet<TimerHandle> PausedTimerSet = new();
        private Dictionary<object, HashSet<TimerHandle>> ObjectToTimers = new();

        private int InternalTime = 0;
        private TimerHandle CurrentlyExecutingTimer;

        protected void OnEnable()
		{
			if (Instance == null)
			{
				Instance = this;
				if (dontDestroyOnLoad)
				{
					DontDestroyOnLoad(gameObject);
				}
			}
			else if (Instance != this)
			{
				Destroy(gameObject);
			}
		}

        public void SetTimer(ref TimerHandle handle, in TimerDelegate @delegate, float rate, bool loop, float firstDelay = -1)
        {
            InternalSetTimer(ref handle, new TimerUnifiedDelegate(@delegate), (int)rate, loop, (int)firstDelay);
        }

        public void SetTimer(ref TimerHandle handle, in TimerDynamicDelegate @delegate, float rate, bool loop, float firstDelay = -1)
        {
            InternalSetTimer(ref handle, new TimerUnifiedDelegate(@delegate), (int)rate, loop, (int)firstDelay);
        }

        public void SetTimer(ref TimerHandle handle, in TimerFunction @delegate, float rate, bool loop, float firstDelay = -1)
        {
            InternalSetTimer(ref handle, new TimerUnifiedDelegate(@delegate), (int)rate, loop, (int)firstDelay);
        }

        public void SetTimer(ref TimerHandle handle, float rate, bool loop, float firstDelay = -1)
        {
            InternalSetTimer(ref handle, new TimerUnifiedDelegate(), (int)rate, loop, (int)firstDelay);
        }

        public void SetTimer(ref TimerHandle handle, in TimerDelegate @delegate, float rate, in TurnBasedTimerManagerTimerParameters parameters)
        {
            InternalSetTimer(ref handle, new TimerUnifiedDelegate(@delegate), (int)rate, parameters.Loop, parameters.FirstDelay);
        }

        public void SetTimer(ref TimerHandle handle, in TimerDynamicDelegate @delegate, float rate, in TurnBasedTimerManagerTimerParameters parameters)
        {
            InternalSetTimer(ref handle, new TimerUnifiedDelegate(@delegate), (int)rate, parameters.Loop, parameters.FirstDelay);
        }

        public void SetTimer(ref TimerHandle handle, in TimerFunction @delegate, float rate, in TurnBasedTimerManagerTimerParameters parameters)
        {
            InternalSetTimer(ref handle, new TimerUnifiedDelegate(@delegate), (int)rate, parameters.Loop, parameters.FirstDelay);
        }

        public void SetTimer(ref TimerHandle handle, float rate, in TurnBasedTimerManagerTimerParameters parameters)
        {
            InternalSetTimer(ref handle, new TimerUnifiedDelegate(), (int)rate, parameters.Loop, parameters.FirstDelay);
        }

        public void ClearTimer(TimerHandle handle)
        {
            TurnBasedTimerData timerData = FindTimer(handle);
            if (timerData != null)
            {
                InternalClearTimer(handle);
            }
            handle.Invalidate();
        }

        public float GetTimerElapsed(TimerHandle handle)
        {
            TurnBasedTimerData timerData = FindTimer(handle);
            return InternalGetTimerElapsed(timerData);
        }

        public float GetTimerRate(TimerHandle handle)
        {
            TurnBasedTimerData timerData = FindTimer(handle);
            return InternalGetTimerRate(timerData);
        }

        public float GetTimerRemaining(TimerHandle handle)
        {
            TurnBasedTimerData timerData = FindTimer(handle);
            return InternalGetTimerRemaining(timerData);
        }

        public bool IsTimerActive(TimerHandle handle)
        {
            TurnBasedTimerData timerData = FindTimer(handle);
            return timerData != null && timerData.Status != TimerStatus.Paused;
        }

        public bool IsTimerPaused(TimerHandle handle)
        {
            TurnBasedTimerData timerData = FindTimer(handle);
            return timerData != null && timerData.Status == TimerStatus.Paused;
        }

        public void PauseTimer(TimerHandle handle)
        {
            TurnBasedTimerData timerToPause = FindTimer(handle);
            if (timerToPause == null || timerToPause.Status == TimerStatus.Paused)
            {
                return;
            }

            TimerStatus previousStatus = timerToPause.Status;

            switch (previousStatus)
            {
                case TimerStatus.Active:
                    bool removed = ActiveTimerHeap.Remove(handle, out var _, out var _);
                    Debug.Assert(removed);
                    break;
                case TimerStatus.Executing:
                    Debug.Assert(CurrentlyExecutingTimer == handle);
                    CurrentlyExecutingTimer.Invalidate();
                    break;
                default:
                    Debug.Assert(false);
                    break;
            }

            if (previousStatus == TimerStatus.Executing && !timerToPause.Loop)
            {
                RemoveTimer(handle);
            }
            else
            {
                PausedTimerSet.Add(handle);

                timerToPause.Status = TimerStatus.Paused;

                if (previousStatus != TimerStatus.Pending)
                {
                    timerToPause.ExpireTime -= InternalTime;
                }
            }
        }

        public void UnPauseTimer(TimerHandle handle)
        {
            TurnBasedTimerData timerToUnPause = FindTimer(handle);
            if (timerToUnPause == null || timerToUnPause.Status != TimerStatus.Paused)
            {
                return;
            }

            timerToUnPause.ExpireTime += InternalTime;
            timerToUnPause.Status = TimerStatus.Active;
            ActiveTimerHeap.Enqueue(handle, timerToUnPause.ExpireTime);

            PausedTimerSet.Remove(handle);
        }

        public void Tick(int deltaTime)
        {
            InternalTime += deltaTime;

            while (ActiveTimerHeap.Count > 0)
            {
                TimerHandle topHandle = ActiveTimerHeap.Peek();

                int topIndex = topHandle.GetIndex();
                TurnBasedTimerData top = Timers[topIndex];

                if (InternalTime >= top.ExpireTime)
                {
                    CurrentlyExecutingTimer = ActiveTimerHeap.Dequeue();
                    top.Status = TimerStatus.Executing;

                    int callCount = top.Loop ? (int)((InternalTime - top.ExpireTime) / top.Rate) + 1 : 1;

                    for (int callIdx = 0; callIdx < callCount; callIdx++)
                    {
                        Debug.Assert(!WillRemoveTimerAssert(CurrentlyExecutingTimer), "RemoveTimer (CurrentlyExecutingTimer) - due to fail before Execute()");
                        top.TimerDelegate.Execute();

                        top = FindTimer(CurrentlyExecutingTimer);
                        Debug.Assert(top == null || !WillRemoveTimerAssert(CurrentlyExecutingTimer), "RemoveTimer (CurrentlyExecutingTimer) - due to fail after Execute()");
                        if (top == null || top.Status != TimerStatus.Executing || top.MaxOncePerTurn)
                        {
                            break;
                        }
                    }

                    if (top != null)
                    {
                        if (top.Loop && (!top.RequiresDelegate || top.TimerDelegate.IsBound()))
                        {
                            top.ExpireTime += callCount * top.Rate;
                            top.Status = TimerStatus.Active;
                            ActiveTimerHeap.Enqueue(CurrentlyExecutingTimer, top.ExpireTime);
                        }
                        else
                        {
                            RemoveTimer(CurrentlyExecutingTimer);
                        }

                        CurrentlyExecutingTimer.Invalidate();
                    }
                }
                else
                {
                    break;
                }
            }
        }

        public float GetTimeSeconds()
		{
			return InternalTime;
		}

        public bool TimerExists(TimerHandle handle)
        {
            return FindTimer(handle) != null;
        }

        public void ClearAllTimersForObject(object @object)
		{
			if (@object != null)
			{
				InternalClearAllTimers(@object);
			}
		}

        private void InternalClearAllTimers(object @object)
		{
			if (@object == null)
			{
				return;
			}

			if (ObjectToTimers.TryGetValue(@object, out HashSet<TimerHandle> timersToRemove))
			{
				foreach (TimerHandle timerToRemove in timersToRemove)
				{
					InternalClearTimer(timerToRemove);
				}
			}
		}

        private void InternalSetTimer(ref TimerHandle handle, TimerUnifiedDelegate @delegate, int rate, bool loop, int firstDelay)
        {
            if (FindTimer(handle) != null)
            {
                InternalClearTimer(handle);
            }

            if (rate > 0)
            {
                TurnBasedTimerData newTimerData = new()
                {
                    TimerDelegate = @delegate,
                    Rate = rate,
                    Loop = loop,
                    RequiresDelegate = @delegate.IsBound(),
                    ExpireTime = InternalTime + firstDelay,
                    Status = TimerStatus.Active
                };

                TimerHandle newHandle = AddTimer(newTimerData);
                ActiveTimerHeap.Enqueue(newHandle, newTimerData.ExpireTime);
                handle = newHandle;
            }
            else
            {
                handle.Invalidate();
            }
        }

        private void InternalClearTimer(TimerHandle handle)
        {
            TurnBasedTimerData data = FindTimer(handle);
            switch (data.Status)
            {
                case TimerStatus.Active:
                    RemoveTimer(handle);
                    break;
                case TimerStatus.Paused:
                    {
                        bool removed = PausedTimerSet.Remove(handle);
                        Debug.Assert(removed);
                        RemoveTimer(handle);
                    }
                    break;
                case TimerStatus.Executing:
                    Debug.Assert(CurrentlyExecutingTimer == handle);
                    CurrentlyExecutingTimer.Invalidate();
                    RemoveTimer(handle);
                    break;
                default:
                    Debug.Assert(false);
                    break;
            }
        }

        private int InternalGetTimerRate(TurnBasedTimerData timerData)
        {
            if (timerData != null)
            {
                return timerData.Rate;
            }

            return -1;
        }

        private int InternalGetTimerElapsed(TurnBasedTimerData timerData)
        {
            if (timerData != null)
            {
                switch (timerData.Status)
                {
                    case TimerStatus.Active:
                    case TimerStatus.Executing:
                        return timerData.Rate - (timerData.ExpireTime - InternalTime);
                    default:
                        return timerData.Rate - timerData.ExpireTime;
                }
            }

            return -1;
        }

        private int InternalGetTimerRemaining(TurnBasedTimerData timerData)
        {
            if (timerData != null)
            {
                switch (timerData.Status)
                {
                    case TimerStatus.Active:
                        return timerData.ExpireTime - InternalTime;
                    case TimerStatus.Executing:
                        return 0;
                    default:
                        return timerData.ExpireTime;
                }
            }

            return -1;
        }

        private TimerHandle AddTimer(TurnBasedTimerData timerData)
        {
            object timerKey = timerData.TimerDelegate.GetBoundObject();
            timerData.TimerIndicesByObjectKey = timerKey;

            int newIndex = Timers.Add(timerData);
            TimerHandle result = TimerHandleGenerator.GenerateHandle(newIndex);
            Timers[newIndex].Handle = result;

            if (timerKey != null)
            {
                if (!ObjectToTimers.TryGetValue(timerKey, out HashSet<TimerHandle> handleSet))
                {
                    handleSet = new();
                    ObjectToTimers.Add(timerKey, handleSet);
                }
                handleSet.Add(result);
            }

            return result;
        }

        private void RemoveTimer(TimerHandle handle)
        {
            TurnBasedTimerData data = GetTimer(handle);

            object timerKey = data.TimerIndicesByObjectKey;
            if (timerKey != null && ObjectToTimers.TryGetValue(timerKey, out HashSet<TimerHandle> timersForObject))
            {
                timersForObject.Remove(handle);
                if (timersForObject.Count == 0)
                {
                    ObjectToTimers.Remove(timerKey);
                }
            }

            Timers.RemoveAt(handle.GetIndex());
        }

        private TurnBasedTimerData GetTimer(TimerHandle handle)
        {
            int index = handle.GetIndex();
            return Timers[index];
        }

        private TurnBasedTimerData FindTimer(in TimerHandle handle)
        {
            if (!handle.IsValid())
            {
                return null;
            }

            int index = handle.GetIndex();
            if (!Timers.IsValidIndex(index))
            {
                return null;
            }

            TurnBasedTimerData timer = Timers[index];

            if (timer.Handle != handle)
            {
                return null;
            }

            return timer;
        }

        private bool WillRemoveTimerAssert(TimerHandle handle)
        {
            TurnBasedTimerData data = GetTimer(handle);

            object timerIndicesByObjectKey = data.TimerIndicesByObjectKey;
            if (timerIndicesByObjectKey != null)
            {
                if (!ObjectToTimers.TryGetValue(timerIndicesByObjectKey, out HashSet<TimerHandle> timersForObject))
                {
                    return true;
                }

                if (!timersForObject.TryGetValue(handle, out TimerHandle found))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
