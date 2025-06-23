using UnityEngine;

namespace GameplayAbilities
{
    public interface ITimerManager
    {
        void SetTimer(ref TimerHandle handle, in TimerDelegate @delegate, float rate, bool loop, float firstDelay = default);
        void ClearTimer(TimerHandle handle);
        void PauseTimer(TimerHandle handle);
        void UnPauseTimer(TimerHandle handle);
        bool IsTimerActive(TimerHandle handle);
        bool IsTimerPaused(TimerHandle handle);
        bool TimerExists(TimerHandle handle);
        float GetTimerRate(TimerHandle handle);
        float GetTimerElapsed(TimerHandle handle);
        float GetTimerRemaining(TimerHandle handle);
        float GetTimeSeconds();
    }
}