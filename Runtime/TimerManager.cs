using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace GameplayAbilities
{
	using TimerFunction = Action;

	using TimerDynamicDelegate = UnityAction;

	public delegate void TimerDelegate();

	public enum TimerStatus
	{
		Pending,
		Active,
		Paused,
		Executing,
		ActivePendingRemoval,
	}

	public struct TimerUnifiedDelegate
	{
		public object VariantDelegate;

		public TimerUnifiedDelegate(in TimerDelegate d)
		{
			VariantDelegate = d;
		}

		public TimerUnifiedDelegate(in TimerDynamicDelegate d)
		{
			VariantDelegate = d;
		}

		public TimerUnifiedDelegate(TimerFunction callback)
		{
			VariantDelegate = callback;
		}

		public readonly void Execute()
		{
			switch (VariantDelegate)
			{
				case TimerDelegate funcDelegate:
					funcDelegate?.Invoke();
					break;
				case TimerDynamicDelegate funcDynDelegate:
					funcDynDelegate?.Invoke();
					break;
				case TimerFunction timerFunction:
					timerFunction?.Invoke();
					break;
				default:
					break;
			}
		}

		public readonly bool IsBound()
		{
			switch (VariantDelegate)
			{
				case TimerDelegate funcDelegate:
					return funcDelegate.Target != null;
				case TimerDynamicDelegate funcDynDelegate:
					return funcDynDelegate.Target != null;
				case TimerFunction timerFunction:
					return timerFunction.Target != null;
				default:
					return false;
			}
		}

		public readonly object GetBoundObject()
		{
			switch (VariantDelegate)
			{
				case TimerDelegate funcDelegate:
					return funcDelegate.Target;
				case TimerDynamicDelegate funcDynDelegate:
					return funcDynDelegate.Target;
				case TimerFunction timerFunction:
					return timerFunction.Target;
				default:
					return null;
			}
		}
	}

	public class TimerData
	{
		public bool Loop;
		public bool MaxOncePerFrame;
		public bool RequiresDelegate;
		public TimerStatus Status;
		public float Rate;
		public double ExpireTime;
		public TimerUnifiedDelegate TimerDelegate;
		public TimerHandle Handle;
		public object TimerIndicesByObjectKey;
	}

	public struct TimerManagerTimerParameters
	{
		public bool Loop;
		public bool MaxOncePerFrame;
		public float FirstDelay;

		public TimerManagerTimerParameters(bool loop, bool maxOncePerFrame, float firstDelay = -1f)
		{
			Loop = loop;
			MaxOncePerFrame = maxOncePerFrame;
			FirstDelay = firstDelay;
		}
	};

	public class TimerManager : MonoBehaviour, ITimerManager
	{
		public static TimerManager Instance { get; private set; }

		[SerializeField] private bool dontDestroyOnLoad = true;
		private SparseArray<TimerData> Timers = new();
		private PriorityQueue<TimerHandle, double> ActiveTimerHeap = new();
		private HashSet<TimerHandle> PausedTimerSet = new();
		private HashSet<TimerHandle> PendingTimerSet = new();
		private Dictionary<object, HashSet<TimerHandle>> ObjectToTimers = new();
		private float InternalTime;
		private TimerHandle CurrentlyExecutingTimer;
		private int LastTickedFrame;
		private static int GuaranteeEngineTickDelay = 0;

		public void SetTimer(ref TimerHandle handle, in TimerDelegate @delegate, float rate, bool loop, float firstDelay = -1)
		{
			InternalSetTimer(ref handle, new TimerUnifiedDelegate(@delegate), rate, loop, firstDelay);
		}

		public void SetTimer(ref TimerHandle handle, in TimerDynamicDelegate @delegate, float rate, bool loop, float firstDelay = -1)
		{
			InternalSetTimer(ref handle, new TimerUnifiedDelegate(@delegate), rate, loop, firstDelay);
		}

		public void SetTimer(ref TimerHandle handle, in TimerFunction @delegate, float rate, bool loop, float firstDelay = -1)
		{
			InternalSetTimer(ref handle, new TimerUnifiedDelegate(@delegate), rate, loop, firstDelay);
		}

		public void SetTimer(ref TimerHandle handle, float rate, bool loop, float firstDelay = -1)
		{
			InternalSetTimer(ref handle, new TimerUnifiedDelegate(), rate, loop, firstDelay);
		}

		public void SetTimer(ref TimerHandle handle, in TimerDelegate @delegate, float rate, in TimerManagerTimerParameters parameters)
		{
			InternalSetTimer(ref handle, new TimerUnifiedDelegate(@delegate), rate, parameters.Loop, parameters.FirstDelay);
		}

		public void SetTimer(ref TimerHandle handle, in TimerDynamicDelegate @delegate, float rate, in TimerManagerTimerParameters parameters)
		{
			InternalSetTimer(ref handle, new TimerUnifiedDelegate(@delegate), rate, parameters.Loop, parameters.FirstDelay);
		}

		public void SetTimer(ref TimerHandle handle, in TimerFunction @delegate, float rate, in TimerManagerTimerParameters parameters)
		{
			InternalSetTimer(ref handle, new TimerUnifiedDelegate(@delegate), rate, parameters.Loop, parameters.FirstDelay);
		}

		public void SetTimer(ref TimerHandle handle, float rate, in TimerManagerTimerParameters parameters)
		{
			InternalSetTimer(ref handle, new TimerUnifiedDelegate(), rate, parameters.Loop, parameters.FirstDelay);
		}

		public TimerHandle SetTimerForNextTick(in TimerDelegate @delegate)
		{
			return InternalSetTimerForNextTick(new TimerUnifiedDelegate(@delegate));
		}

		public TimerHandle SetTimerForNextTick(in TimerDynamicDelegate @delegate)
		{
			return InternalSetTimerForNextTick(new TimerUnifiedDelegate(@delegate));
		}

		public TimerHandle SetTimerForNextTick(in TimerFunction @delegate)
		{
			return InternalSetTimerForNextTick(new TimerUnifiedDelegate(@delegate));
		}

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

		protected void Update()
		{
			Tick(Time.deltaTime);
		}

		public void ClearTimer(TimerHandle handle)
		{
			TimerData timerData = FindTimer(handle);
			if (timerData != null)
			{
				InternalClearTimer(handle);
			}
			handle.Invalidate();
		}

		public void PauseTimer(TimerHandle handle)
		{
			TimerData timerToPause = FindTimer(handle);
			if (timerToPause == null || timerToPause.Status == TimerStatus.Paused)
			{
				return;
			}

			TimerStatus previousStatus = timerToPause.Status;

			switch (previousStatus)
			{
				case TimerStatus.ActivePendingRemoval:
					break;
				case TimerStatus.Active:
					bool removed = ActiveTimerHeap.Remove(handle, out var _, out var _);
					Debug.Assert(removed);
					break;
				case TimerStatus.Pending:
					removed = PendingTimerSet.Remove(handle);
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
			TimerData timerToUnPause = FindTimer(handle);
			if (timerToUnPause == null || timerToUnPause.Status != TimerStatus.Paused)
			{
				return;
			}

			if (HasBeenTickedThisFrame())
			{
				timerToUnPause.ExpireTime += InternalTime;
				timerToUnPause.Status = TimerStatus.Active;
				ActiveTimerHeap.Enqueue(handle, timerToUnPause.ExpireTime);
			}
			else
			{
				timerToUnPause.Status = TimerStatus.Pending;
				PendingTimerSet.Add(handle);
			}

			PausedTimerSet.Remove(handle);
		}

		public float GetTimerRate(TimerHandle handle)
		{
			TimerData timerData = FindTimer(handle);
			return InternalGetTimerRate(timerData);
		}

		public bool IsTimerActive(TimerHandle handle)
		{
			TimerData timerData = FindTimer(handle);
			return timerData != null && timerData.Status != TimerStatus.Paused;
		}

		public bool IsTimerPaused(TimerHandle handle)
		{
			TimerData timerData = FindTimer(handle);
			return timerData != null && timerData.Status == TimerStatus.Paused;
		}

		public bool IsTimerPending(TimerHandle handle)
		{
			TimerData timerData = FindTimer(handle);
			return timerData != null && timerData.Status == TimerStatus.Pending;
		}

		public bool TimerExists(TimerHandle handle)
		{
			return FindTimer(handle) != null;
		}

		public float GetTimerElapsed(TimerHandle handle)
		{
			TimerData timerData = FindTimer(handle);
			return InternalGetTimerElapsed(timerData);
		}

		public float GetTimerRemaining(TimerHandle handle)
		{
			TimerData timerData = FindTimer(handle);
			return InternalGetTimerRemaining(timerData);
		}

		public float GetTimeSeconds()
		{
			return InternalTime;
		}

		public void Tick(float deltaTime)
		{
			if (HasBeenTickedThisFrame())
			{
				return;
			}

			InternalTime += deltaTime;

			while (ActiveTimerHeap.Count > 0)
			{
				TimerHandle topHandle = ActiveTimerHeap.Peek();

				int topIndex = topHandle.GetIndex();
				TimerData top = Timers[topIndex];

				if (top.Status == TimerStatus.ActivePendingRemoval)
				{
					topHandle = ActiveTimerHeap.Dequeue();
					RemoveTimer(topHandle);
					continue;
				}

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
						if (top == null || top.Status != TimerStatus.Executing || top.MaxOncePerFrame)
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

			LastTickedFrame = UnityEngine.Time.frameCount;

			if (PendingTimerSet.Count > 0)
			{
				foreach (TimerHandle handle in PendingTimerSet)
				{
					TimerData timerToActivate = GetTimer(handle);

					timerToActivate.ExpireTime += InternalTime;
					timerToActivate.Status = TimerStatus.Active;
					ActiveTimerHeap.Enqueue(handle, timerToActivate.ExpireTime);
				}
				PendingTimerSet.Clear();
			}
		}

		public bool HasBeenTickedThisFrame()
		{
			return LastTickedFrame == UnityEngine.Time.frameCount;
		}

		public TimerData GetTimer(in TimerHandle handle)
		{
			int index = handle.GetIndex();
			TimerData timer = Timers[index];
			return timer;
		}

		public void ClearAllTimersForObject(object @object)
		{
			if (@object != null)
			{
				InternalClearAllTimers(@object);
			}
		}

		private TimerData FindTimer(in TimerHandle handle)
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

			TimerData timer = Timers[index];

			if (timer.Handle != handle || timer.Status == TimerStatus.ActivePendingRemoval)
			{
				return null;
			}

			return timer;
		}

		private void InternalSetTimer(ref TimerHandle handle, TimerUnifiedDelegate @delegate, float rate, bool loop, float firstDelay)
		{
			InternalSetTimer(ref handle, @delegate, rate, new TimerManagerTimerParameters(loop, false, firstDelay));
		}

		private void InternalSetTimer(ref TimerHandle handle, TimerUnifiedDelegate @delegate, float rate, in TimerManagerTimerParameters timerParameters)
		{
			if (FindTimer(handle) != null)
			{
				InternalClearTimer(handle);
			}

			if (rate > 0)
			{
				TimerData newTimerData = new()
				{
					TimerDelegate = @delegate,
					Rate = rate,
					Loop = timerParameters.Loop,
					MaxOncePerFrame = timerParameters.MaxOncePerFrame,
					RequiresDelegate = @delegate.IsBound(),
				};

				float firstDelay = timerParameters.FirstDelay >= 0 ? timerParameters.FirstDelay : rate;

				TimerHandle newTimerHandle;
				if (HasBeenTickedThisFrame())
				{
					newTimerData.ExpireTime = InternalTime + firstDelay;
					newTimerData.Status = TimerStatus.Active;
					newTimerHandle = AddTimer(newTimerData);
					ActiveTimerHeap.Enqueue(newTimerHandle, newTimerData.ExpireTime);
				}
				else
				{
					newTimerData.ExpireTime = firstDelay;
					newTimerData.Status = TimerStatus.Pending;
					newTimerHandle = AddTimer(newTimerData);
					PendingTimerSet.Add(newTimerHandle);
				}

				handle = newTimerHandle;
			}
			else
			{
				handle.Invalidate();
			}
		}

		private TimerHandle InternalSetTimerForNextTick(TimerUnifiedDelegate @delegate)
		{
			TimerData newTimerData = new()
			{
				Rate = 0,
				Loop = false,
				RequiresDelegate = true,
				TimerDelegate = @delegate,
				ExpireTime = InternalTime,
			};

			bool queueForCurrentFrame = GuaranteeEngineTickDelay == 0 || HasBeenTickedThisFrame();

			TimerHandle newTimerHandle;
			if (queueForCurrentFrame)
			{
				newTimerData.Status = TimerStatus.Active;
				newTimerHandle = AddTimer(newTimerData);
				ActiveTimerHeap.Enqueue(newTimerHandle, newTimerData.ExpireTime);
			}
			else
			{
				newTimerData.Status = TimerStatus.Pending;
				newTimerData.ExpireTime = 0;
				newTimerHandle = AddTimer(newTimerData);
				PendingTimerSet.Add(newTimerHandle);
			}

			return newTimerHandle;
		}

		private void InternalClearTimer(TimerHandle handle)
		{
			TimerData data = FindTimer(handle);
			switch (data.Status)
			{
				case TimerStatus.Pending:
					{
						bool removed = PendingTimerSet.Remove(handle);
						Debug.Assert(removed);
						RemoveTimer(handle);
					}
					break;
				case TimerStatus.Active:
					data.Status = TimerStatus.ActivePendingRemoval;
					break;
				case TimerStatus.ActivePendingRemoval:
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

		private float InternalGetTimerElapsed(TimerData timerData)
		{
			if (timerData != null)
			{
				switch (timerData.Status)
				{
					case TimerStatus.Active:
					case TimerStatus.Executing:
						return (float)(timerData.Rate - (timerData.ExpireTime - InternalTime));
					default:
						return (float)(timerData.Rate - timerData.ExpireTime);
				}
			}

			return -1;
		}

		private float InternalGetTimerRemaining(TimerData timerData)
		{
			if (timerData != null)
			{
				switch (timerData.Status)
				{
					case TimerStatus.Active:
						return (float)(timerData.ExpireTime - InternalTime);
					case TimerStatus.Executing:
						return 0;
					default:
						return (float)timerData.ExpireTime;
				}
			}

			return -1;
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

		private float InternalGetTimerRate(TimerData timerData)
		{
			if (timerData != null)
			{
				return timerData.Rate;
			}

			return -1;
		}

		private TimerHandle AddTimer(TimerData timerData)
		{
			object timerIndicesByObjectKey = timerData.TimerDelegate.GetBoundObject();
			timerData.TimerIndicesByObjectKey = timerIndicesByObjectKey;

			int newIndex = Timers.Add(timerData);

			TimerHandle result = TimerHandleGenerator.GenerateHandle(newIndex);
			Timers[newIndex].Handle = result;

			if (timerIndicesByObjectKey != null)
			{
				if (!ObjectToTimers.TryGetValue(timerIndicesByObjectKey, out HashSet<TimerHandle> handleSet))
				{
					handleSet = new();
					ObjectToTimers.Add(timerIndicesByObjectKey, handleSet);
				}

				bool alreadyExists = !handleSet.Add(result);
				if (alreadyExists)
				{
					Debug.LogError("Timer already exists for object: " + timerIndicesByObjectKey);
				}
			}

			return result;
		}

		private void RemoveTimer(TimerHandle handle)
		{
			TimerData data = GetTimer(handle);

			object timerIndicesByObjectKey = data.TimerIndicesByObjectKey;
			if (timerIndicesByObjectKey != null)
			{
				HashSet<TimerHandle> timersForObject = ObjectToTimers[timerIndicesByObjectKey];
				Debug.Assert(timersForObject != null, $"Removed timer was bound to an object which is not tracked by ObjectToTimers! ({GetTimerDataSafely(data)})");

				bool removed = timersForObject.Remove(handle);
				Debug.Assert(removed, $"Removed timer was not found in ObjectToTimers! ({GetTimerDataSafely(data)})");

				if (timersForObject.Count == 0)
				{
					ObjectToTimers.Remove(timerIndicesByObjectKey);
				}
			}

			Timers.RemoveAt(handle.GetIndex());
		}

		private void DescribeTimerDataSafely(in TimerData timerData)
		{
			Debug.Log($"TimerData: {timerData}: Loop= {timerData.Loop}, Status= {timerData.Status}, Rate= {timerData.Rate}, ExpireTime= {timerData.ExpireTime}, Delegate= {timerData.TimerDelegate}");
		}

		private string GetTimerDataSafely(in TimerData timerData)
		{
			return $"TimerData: {timerData}: Loop= {timerData.Loop}, Status= {timerData.Status}, Rate= {timerData.Rate}, ExpireTime= {timerData.ExpireTime}, Delegate= {timerData.TimerDelegate}";
		}

		private void OnCrash()
		{
			Debug.LogWarning($"TimerManager {this} on crashing delegate called, dumping extra information");

			Debug.Log($"{ActiveTimerHeap.Count} Active Timers (including expired)");
			int expiredActiveTimerCount = 0;
			foreach ((TimerHandle handle, double _) in ActiveTimerHeap.UnorderedItems)
			{
				TimerData timer = GetTimer(handle);
				if (timer.Status == TimerStatus.ActivePendingRemoval)
				{
					expiredActiveTimerCount++;
				}
				else
				{
					DescribeTimerDataSafely(timer);
				}
			}

			Debug.Log($"{expiredActiveTimerCount} Expired Active Timers");

			Debug.Log($"{PausedTimerSet.Count} Paused Timers");
			foreach (TimerHandle handle in PausedTimerSet)
			{
				TimerData timer = GetTimer(handle);
				DescribeTimerDataSafely(timer);
			}

			Debug.Log($"{PendingTimerSet.Count} Pending Timers");
			foreach (TimerHandle handle in PendingTimerSet)
			{
				TimerData timer = GetTimer(handle);
				DescribeTimerDataSafely(timer);
			}

			Debug.Log($"{PendingTimerSet.Count + PausedTimerSet.Count + ActiveTimerHeap.Count - expiredActiveTimerCount} Total Timers");

			Debug.LogWarning($"TimerManager {this} dump ended");
		}

		private bool WillRemoveTimerAssert(TimerHandle handle)
		{
			TimerData data = GetTimer(handle);

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