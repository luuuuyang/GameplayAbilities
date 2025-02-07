using UnityEngine;

namespace GameplayAbilities
{
    public static class MathfExtensions
    {
        public const float SMALL_NUMBER = 1E-08f;
        public const float KINDA_SMALL_NUMBER = 1E-04f;

        public static bool IsNearlyEqual(float a, float b, float errorTolerance = SMALL_NUMBER)
        {
            return Mathf.Abs(a - b) <= errorTolerance;
        }
    }
}

