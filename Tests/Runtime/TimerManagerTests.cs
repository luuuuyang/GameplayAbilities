using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameplayAbilities.Tests
{
    public class TimerManagerTests
    {
        private class Dummy
        {
            public byte Count;

            public void Callback() => Count++;
            public void Reset() => Count = 0;
        }

        [Test]
        public void Test_InvalidTimers()
        {
            TimerManager timerManager = TimerManager.Instance;
            TimerHandle handle = new();

            Assert.IsFalse(timerManager.TimerExists(handle), "TimerExists called with an invalid handle");
            Assert.IsFalse(timerManager.IsTimerActive(handle), "IsTimerActive called with an invalid handle");
            Assert.IsFalse(timerManager.IsTimerPaused(handle), "IsTimerPaused called with an invalid handle");
            Assert.IsTrue(timerManager.GetTimerRate(handle) == -1f, "GetTimerRate called with an invalid handle");
            Assert.IsTrue(timerManager.GetTimerElapsed(handle) == -1f, "GetTimerElapsed called with an invalid handle");
            Assert.IsTrue(timerManager.GetTimerRemaining(handle) == -1f, "GetTimerRemaining called with an invalid handle");

            timerManager.PauseTimer(handle);
            timerManager.UnPauseTimer(handle);
            timerManager.ClearTimer(handle);
        }

        [Test]
        public void Test_MissingTimers()
        {
            TimerManager timerManager = TimerManager.Instance;
            TimerHandle handle = timerManager.GenerateHandle(123);

            Assert.IsFalse(timerManager.TimerExists(handle), "TimerExists called with a invalid timer");
            Assert.IsFalse(timerManager.IsTimerActive(handle), "IsTimerActive called with a invalid timer");
            Assert.IsFalse(timerManager.IsTimerPaused(handle), "IsTimerPaused called with a invalid timer");
            Assert.IsTrue(timerManager.GetTimerRate(handle) == -1f, "GetTimerRate called with a invalid timer");
            Assert.IsTrue(timerManager.GetTimerElapsed(handle) == -1f, "GetTimerElapsed called with a invalid timer");
            Assert.IsTrue(timerManager.GetTimerRemaining(handle) == -1f, "GetTimerRemaining called with a invalid timer");

            timerManager.PauseTimer(handle);
            timerManager.UnPauseTimer(handle);
            timerManager.ClearTimer(handle);
        }

        [UnityTest]
        public IEnumerator Test_ValidTimer_HandleWithDelegate()
        {
            TimerManager timerManager = TimerManager.Instance;
            Dummy dummy = new();
            TimerDelegate @delegate = new(dummy.Callback);
            TimerHandle handle = new();
            const float rate = 1.5f;

            timerManager.SetTimer(ref handle, @delegate, rate, false);

            Assert.IsTrue(handle.IsValid(), "Handle should be valid after calling SetTimer");
            Assert.IsTrue(timerManager.TimerExists(handle), "TimerExists called with a pending timer");
            Assert.IsTrue(timerManager.IsTimerActive(handle), "IsTimerActive called with a pending timer");
            Assert.IsFalse(timerManager.IsTimerPaused(handle), "IsTimerPaused called with a pending timer");
            Assert.IsTrue(timerManager.GetTimerRate(handle) == rate, "GetTimerRate called with a pending timer");
            Assert.IsTrue(timerManager.GetTimerElapsed(handle) == 0f, "GetTimerElapsed called with a pending timer");
            Assert.IsTrue(timerManager.GetTimerRemaining(handle) == rate, "GetTimerRemaining called with a pending timer");

            yield return null;

            Assert.IsTrue(timerManager.TimerExists(handle), "TimerExists called with an active timer");
            Assert.IsTrue(timerManager.IsTimerActive(handle), "IsTimerActive called with an active timer");
            Assert.IsFalse(timerManager.IsTimerPaused(handle), "IsTimerPaused called with an active timer");

            yield return new WaitForSeconds(1f);

            Assert.IsTrue(MathfExtensions.IsNearlyEqual(timerManager.GetTimerElapsed(handle), 1f, 1E-02f), "GetTimerElapsed called with an active timer after one step");
            Assert.IsTrue(MathfExtensions.IsNearlyEqual(timerManager.GetTimerRemaining(handle), rate - 1f, 1E-02f), "GetTimerRemaining called with an active timer after one step");

            timerManager.PauseTimer(handle);

            Assert.IsTrue(timerManager.TimerExists(handle), "TimerExists called with a paused timer");
            Assert.IsFalse(timerManager.IsTimerActive(handle), "IsTimerActive called with a paused timer");
            Assert.IsTrue(timerManager.IsTimerPaused(handle), "IsTimerPaused called with a paused timer");

            yield return new WaitForSeconds(1f);

            Assert.IsTrue(timerManager.TimerExists(handle), "TimerExists called with a paused timer");
            Assert.IsFalse(timerManager.IsTimerActive(handle), "IsTimerActive called with a paused timer");
            Assert.IsTrue(timerManager.IsTimerPaused(handle), "IsTimerPaused called with a paused timer");

            Assert.IsTrue(MathfExtensions.IsNearlyEqual(timerManager.GetTimerElapsed(handle), 1f, 1E-02f), "GetTimerElapsed called with a paused timer after one step");
            Assert.IsTrue(MathfExtensions.IsNearlyEqual(timerManager.GetTimerRemaining(handle), rate - 1f, 1E-02f), "GetTimerRemaining called with a paused timer after one step");

            timerManager.UnPauseTimer(handle);

            Assert.IsTrue(timerManager.TimerExists(handle), "TimerExists called with a pending timer");
            Assert.IsTrue(timerManager.IsTimerActive(handle), "IsTimerActive called with a pending timer");
            Assert.IsFalse(timerManager.IsTimerPaused(handle), "IsTimerPaused called with a pending timer");

            yield return new WaitForSeconds(1f);

            Assert.IsFalse(timerManager.TimerExists(handle), "TimerExists called with a completed timer");
            Assert.IsTrue(dummy.Count == 1, "Count of callback executions");

            timerManager.SetTimer(ref handle, @delegate, rate, false);
            timerManager.SetTimer(ref handle, 0, false);

            Assert.IsFalse(timerManager.TimerExists(handle), "TimerExists called with a reset timer");

            dummy.Reset();
            timerManager.SetTimer(ref handle, @delegate, rate, true);
            yield return null;

            yield return new WaitForSeconds(2f);

            Assert.IsTrue(timerManager.TimerExists(handle), "TimerExists called with a looping timer");
            Assert.IsTrue(timerManager.IsTimerActive(handle), "IsTimerActive called with a looping timer");

            Assert.IsTrue(dummy.Count == 1, "Count of callback executions for looping timer");
            Assert.IsTrue(MathfExtensions.IsNearlyEqual(timerManager.GetTimerElapsed(handle), 2f - rate * dummy.Count, 1E-02f), "GetTimerElapsed called with a looping timer");
            Assert.IsTrue(MathfExtensions.IsNearlyEqual(timerManager.GetTimerRemaining(handle), rate * (dummy.Count + 1) - 2f, 1E-02f), "GetTimerRemaining called with a looping timer");

            yield return new WaitForSeconds(2f);

            Assert.IsTrue(dummy.Count == 2, "Count of callback executions for looping timer");
            Assert.IsTrue(MathfExtensions.IsNearlyEqual(timerManager.GetTimerElapsed(handle), 4f - rate * dummy.Count, 1E-02f), "GetTimerElapsed called with a looping timer");
            Assert.IsTrue(MathfExtensions.IsNearlyEqual(timerManager.GetTimerRemaining(handle), rate * (dummy.Count + 1) - 4f, 1E-02f), "GetTimerRemaining called with a looping timer");

            timerManager.SetTimer(ref handle, 0, false);

            Assert.IsFalse(timerManager.TimerExists(handle), "TimerExists called with a reset looping timer");
        }

        public class LoopingTestFunc
        {
            public static TimerManager TimerManager;
            public static TimerHandle Handle;
            public static int TimerCalled;
            public static float NewTime = 1f;

            public static void TimerExecute()
            {
                TimerCalled++;
                if (TimerCalled == 1)
                {
                    TimerManager.SetTimer(ref Handle, new TimerDelegate(TimerExecute), NewTime, true);
                }
                else
                {
                    TimerManager.ClearTimer(Handle);
                }
            }
        }

        [UnityTest]
        public IEnumerator TestValidTimer_HandleLoopingSetDuringExecute()
        {
            TimerManager timerManager = TimerManager.Instance;
            TimerHandle handle = new();
            const float rate = 3f;

            LoopingTestFunc.TimerManager = timerManager;
            LoopingTestFunc.Handle = handle;
            LoopingTestFunc.TimerCalled = 0;

            Assert.IsTrue(LoopingTestFunc.TimerCalled == 0, "Timer called count starts at 0");

            timerManager.SetTimer(ref handle, new TimerDelegate(LoopingTestFunc.TimerExecute), rate, true);

            yield return null;

            yield return new WaitForSeconds(3f);
            Assert.IsTrue(LoopingTestFunc.TimerCalled == 1, "Timer was called first time");
            Assert.IsTrue(timerManager.IsTimerActive(handle), "Timer was readded");
            Assert.IsTrue(MathfExtensions.IsNearlyEqual(timerManager.GetTimerRemaining(handle), LoopingTestFunc.NewTime, 1E-02f), "Timer was readded with correct time");

            yield return new WaitForSeconds(1.1f);
            Assert.IsTrue(LoopingTestFunc.TimerCalled == 2, "Timer was called second time");
            Assert.IsFalse(timerManager.IsTimerActive(handle), "Timer handle no longer active");
        }

        [UnityTest]
        public IEnumerator Test_LoopingTimers_DifferentHandles()
        {
            TimerManager timerManager = TimerManager.Instance;
            TimerHandle handleOne = new();
            TimerHandle handleTwo = new();

            int callCount = 0;
            void Func() { callCount++; }

            TimerDelegate @delegate = new(Func);

            TimerHandle handle = new();
            timerManager.SetTimer(ref handle, @delegate, 1f, false);
            yield return null;
            Debug.Log(Time.deltaTime);

            Assert.IsTrue(MathfExtensions.IsNearlyEqual(timerManager.GetTimerRemaining(handle), 1f, 1E-02f), "First delegate time remaining is 1.0f");

            timerManager.SetTimer(ref handle, @delegate, 5f, false);
            yield return null;
            Debug.Log(Time.deltaTime);
            Assert.IsTrue(MathfExtensions.IsNearlyEqual(timerManager.GetTimerRemaining(handle), 5f, 1E-02f), "Reset delegate time remaining is 5.0f");

            timerManager.SetTimer(ref handleOne, @delegate, 1f, true);
            timerManager.SetTimer(ref handleTwo, @delegate, 1.5f, true);
            yield return null;
            Debug.Log(Time.deltaTime);

            Assert.IsTrue(timerManager.IsTimerActive(handleOne), "Handle One is active");
            Assert.IsTrue(timerManager.IsTimerActive(handleTwo), "Handle Two is active");

            yield return new WaitForSeconds(1f);

            Assert.IsTrue(timerManager.IsTimerActive(handleOne), "Handle One is active after tick");
            Assert.IsTrue(timerManager.IsTimerActive(handleTwo), "Handle Two is active after tick");

            Assert.IsTrue(MathfExtensions.IsNearlyEqual(timerManager.GetTimerRemaining(handleOne), 0f, 1E-02f), "Handle One has 0 seconds remaining after tick");
            Assert.IsTrue(MathfExtensions.IsNearlyEqual(timerManager.GetTimerRemaining(handleTwo), 0.5f, 1E-02f), "Handle Two has 0.5 seconds remaining after tick");
        }
    }
}

