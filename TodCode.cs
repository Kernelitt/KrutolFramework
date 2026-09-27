namespace KrutolFramework.Core
{
    public enum TodCurves
    {
        CURVE_CONSTANT,
        CURVE_LINEAR,
        CURVE_EASE_IN,
        CURVE_EASE_OUT,
        CURVE_EASE_IN_OUT,
        CURVE_EASE_IN_OUT_WEAK,
        CURVE_FAST_IN_OUT,
        CURVE_FAST_IN_OUT_WEAK,
        CURVE_WEAK_FAST_IN_OUT,
        CURVE_BOUNCE,
        CURVE_BOUNCE_FAST_MIDDLE,
        CURVE_BOUNCE_SLOW_MIDDLE,
        CURVE_SIN_WAVE,
        CURVE_EASE_SIN_WAVE
    }

    public static class TodCurveMath
    {
        // Вспомогательные функции деформации времени и главная функция вычисления TodCurveEvaluate на C#
        public static float TodCurveQuad(float t) => t * t;
        public static float TodCurveInvQuad(float t) => 1f - (1f - t) * (1f - t);
        public static float TodCurveS(float t) => 3f * t * t - 2f * t * t * t;
        public static float TodCurveInvQuadS(float t) => t <= 0.5f ? TodCurveS(t * 2f) * 0.5f : TodCurveS((t - 0.5f) * 2f) * 0.5f + 0.5f;
        public static float TodCurveBounce(float t) => t < 1f / 2.75f ? 7.5625f * t * t : t < 2f / 2.75f ? 7.5625f * (t -= 1.5f / 2.75f) * t + 0.75f : t < 2.5f / 2.75f ? 7.5625f * (t -= 2.25f / 2.75f) * t + 0.9375f : 7.5625f * (t -= 2.625f / 2.75f) * t + 0.984375f;

        public static float TodCurveEvaluate(float theTime, float thePositionStart, float thePositionEnd, TodCurves theCurve)
        {
            float aWarpedTime = theCurve switch
            {
                TodCurves.CURVE_CONSTANT => 0f,
                TodCurves.CURVE_LINEAR => theTime,
                TodCurves.CURVE_EASE_IN => TodCurveQuad(theTime),
                TodCurves.CURVE_EASE_OUT => TodCurveInvQuad(theTime),
                TodCurves.CURVE_EASE_IN_OUT => TodCurveS(TodCurveS(theTime)),
                TodCurves.CURVE_EASE_IN_OUT_WEAK => TodCurveS(theTime),
                TodCurves.CURVE_FAST_IN_OUT => TodCurveInvQuadS(TodCurveInvQuadS(theTime)),
                TodCurves.CURVE_FAST_IN_OUT_WEAK => TodCurveInvQuadS(theTime),
                TodCurves.CURVE_BOUNCE => TodCurveBounce(theTime),
                TodCurves.CURVE_BOUNCE_FAST_MIDDLE => TodCurveQuad(TodCurveBounce(theTime)),
                TodCurves.CURVE_BOUNCE_SLOW_MIDDLE => TodCurveInvQuad(TodCurveBounce(theTime)),
                TodCurves.CURVE_SIN_WAVE => MathF.Sin(2f * MathF.PI * theTime),
                TodCurves.CURVE_EASE_SIN_WAVE => MathF.Sin(2f * MathF.PI * TodCurveS(theTime)),
                _ => theTime
            };
            return (thePositionEnd - thePositionStart) * aWarpedTime + thePositionStart;
        }
    }
}