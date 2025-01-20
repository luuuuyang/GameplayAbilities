using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Events;

namespace GameplayAbilities
{
	public enum TimerStatus
	{
		Pending,
		Active,
		Paused,
		Executing,
		ActivePendingRemoval,
	}

	public delegate void TimerDelegate();

	public struct TimerUnifiedDelegate
	{
		public TimerDelegate FuncDelegate;
		public UnityAction FuncDynamicDelegate;
		public Action FuncCallback;

		public void Execute()
		{
			if (FuncDelegate != null)
			{
				FuncDelegate.Invoke();
			}
			else if (FuncDynamicDelegate != null)
			{
				FuncDynamicDelegate.Invoke();
			}
			else if (FuncCallback != null)
			{
				FuncCallback();
			}
		}

		public bool IsBound()
		{
			return FuncDelegate != null || FuncDynamicDelegate != null || FuncCallback != null;
		}

		public object GetBoundObject()
		{
			if (FuncDelegate != null)
			{
				return FuncDelegate.Target;
			}
			else if (FuncDynamicDelegate != null)
			{
				return FuncDynamicDelegate.Target;
			}
			return null;
		}
	}

	public struct TimerHandle
	{
		private ulong Handle;
		public const ushort IndexBits = 24;
		public const ushort SerialNumberBits = 40;
		public const int MaxIndex = 1 << IndexBits;
		public const ulong MaxSerialNumber = 1 << SerialNumberBits;

		public bool IsValid()
		{
			return Handle != 0;
		}

		public void Invalidate()
		{
			Handle = 0;
		}

		public void SetIndexAndSerialNumber(int index, ulong serialNumber)
		{
			Handle = (serialNumber << IndexBits) | (uint)index;
		}

		public int GetIndex()
		{
			return (int)(Handle & (MaxIndex - 1));
		}

		public ulong GetSerialNumber()
		{
			return Handle >> IndexBits;
		}

		public static bool operator ==(TimerHandle a, TimerHandle b)
		{
			return a.Handle == b.Handle;
		}

		public static bool operator !=(TimerHandle a, TimerHandle b)
		{
			return a.Handle != b.Handle;
		}

		public override int GetHashCode()
		{
			return Handle.GetHashCode();
		}
	}

	public class TimerData
	{
		public bool Loop;
		public bool RequiresDelegate;
		public TimerStatus Status;
		public float Rate;
		public double ExpireTime;
		public TimerUnifiedDelegate TimerDelegate;
		public TimerHandle Handle;
		public object TimerIndicesByObjectKey;
	}

	public class TimerManager : SingletonMonoBehaviour<TimerManager>
	{
		private Dictionary<int, TimerData> Timers = new();
		private PriorityQueue<TimerHandle, double> ActiveTimerHeap = new();
		private HashSet<TimerHandle> PausedTimerSet = new();
		private HashSet<TimerHandle> PendingTimerSet = new();
		private Dictionary<object, HashSet<TimerHandle>> ObjectToTimers = new();
		private float InternalTime;
		private TimerHandle CurrentlyExecutingTimer;
		private int LastTickedFrame;
		private static ulong LastAssignedSerialNumber;

		protected void Update()
		{
			Tick(Time.deltaTime);
		}

		public void SetTimer(ref TimerHandle handle, TimerDelegate @delegate, float rate, bool loop, float firstDelay = -1)
		{
			InternalSetTimer(ref handle, new TimerUnifiedDelegate { FuncDelegate = @delegate }, rate, loop, firstDelay);
		}

		public void SetTimer(ref TimerHandle handle, UnityAction @delegate, float rate, bool loop, float firstDelay = -1)
		{
			InternalSetTimer(ref handle, new TimerUnifiedDelegate { FuncDynamicDelegate = @delegate }, rate, loop, firstDelay);
		}

		public void SetTimer(ref TimerHandle handle, Action @delegate, float rate, bool loop, float firstDelay = -1)
		{
			InternalSetTimer(ref handle, new TimerUnifiedDelegate { FuncCallback = @delegate }, rate, loop, firstDelay);
		}

		public void SetTimer(ref TimerHandle handle, float rate, bool loop, float firstDelay = -1)
		{
			InternalSetTimer(ref handle, new TimerUnifiedDelegate { }, rate, loop, firstDelay);
		}

		public TimerHandle SetTimerForNextTick(in TimerDelegate @delegate)
		{
			return InternalSetTimerForNextTick(new TimerUnifiedDelegate { FuncDelegate = @delegate });
		}

		public TimerHandle SetTimerForNextTick(UnityAction @delegate)
		{
			return InternalSetTimerForNextTick(new TimerUnifiedDelegate { FuncDynamicDelegate = @delegate });
		}

		public TimerHandle SetTimerForNextTick(Action @delegate)
		{
			return InternalSetTimerForNextTick(new TimerUnifiedDelegate { FuncCallback = @delegate });
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
					ActiveTimerHeap.Remove(handle, out var _, out var _);
					break;
				case TimerStatus.Pending:
					PendingTimerSet.Remove(handle);
					break;
				case TimerStatus.Executing:
					if (CurrentlyExecutingTimer == handle)
					{
						CurrentlyExecutingTimer.Invalidate();
					}
					break;
				default:
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

				if (InternalTime > top.ExpireTime)
				{
					CurrentlyExecutingTimer = ActiveTimerHeap.Dequeue();
					top.Status = TimerStatus.Executing;

					int callCount = top.Loop ? (int)((InternalTime - top.ExpireTime) / top.Rate) + 1 : 1;

					for (int callIdx = 0; callIdx < callCount; callIdx++)
					{
						Assert.IsTrue(!WillRemoveTimerAssert(CurrentlyExecutingTimer), "RemoveTimer (CurrentlyExecutingTimer) - due to fail before Execute()");
						top.TimerDelegate.Execute();

						top = FindTimer(CurrentlyExecutingTimer);
						Assert.IsTrue(top == null || !WillRemoveTimerAssert(CurrentlyExecutingTimer), "RemoveTimer (CurrentlyExecutingTimer) - due to fail after Execute()");
						if (top == null || top.Status != TimerStatus.Executing)
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

		public TimerHandle GenerateHandle(int index)
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

		public TimerData GetTimer(in TimerHandle handle)
		{
			int index = handle.GetIndex();
			TimerData timer = Timers[index];
			return timer;
		}

		private TimerData FindTimer(in TimerHandle handle)
		{
			if (!handle.IsValid())
			{
				return null;
			}

			int index = handle.GetIndex();
			if (!Timers.TryGetValue(index, out TimerData timer))
			{
				return null;
			}

			if (timer.Handle != handle || timer.Status == TimerStatus.ActivePendingRemoval)
			{
				return null;
			}

			return timer;
		}

		private void InternalSetTimer(ref TimerHandle handle, TimerUnifiedDelegate @delegate, float rate, bool loop, float firstDelay)
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
					Loop = loop,

					ExpireTime = firstDelay
				};

				firstDelay = firstDelay >= 0 ? firstDelay : rate;

				TimerHandle newTimerHandle = new();
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
				TimerDelegate = @delegate,
				ExpireTime = InternalTime,
				Status = TimerStatus.Active
			};

			TimerHandle newTimerHandle = AddTimer(newTimerData);
			ActiveTimerHeap.Enqueue(newTimerHandle, newTimerData.ExpireTime);

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
						Assert.IsTrue(removed);
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
						Assert.IsTrue(removed);
						RemoveTimer(handle);
					}
					break;
				case TimerStatus.Executing:
					Assert.IsTrue(CurrentlyExecutingTimer == handle);
					CurrentlyExecutingTimer.Invalidate();
					RemoveTimer(handle);
					break;
				default:
					Assert.IsTrue(false);
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

			int newIndex = Timers.Count;
			Timers.Add(newIndex, timerData);

			TimerHandle result = GenerateHandle(newIndex);
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

			Timers.Remove(handle.GetIndex());
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