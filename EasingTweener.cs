using System;
using OpenTK.Mathematics;


namespace KrutolFramework
{
    public enum EasingType { Linear, QuadIn, QuadOut, QuadInOut, CubicOut }

    public class Tweener2D
    {
        public Vector2 CurrentPosition { get; private set; }

        private Vector2 _start;
        private Vector2 _end;
        private float _duration;
        private float _currentTime;
        private EasingType _easing;

        public bool IsActive { get; private set; }

        public void StartTween(Vector2 start, Vector2 end, float duration, EasingType easing)
        {
            _start = start;
            _end = end;
            _duration = duration;
            _easing = easing;
            _currentTime = 0f;
            CurrentPosition = start;
            IsActive = true;
        }

        public void Update(float deltaTime)
        {
            if (!IsActive) return;

            _currentTime += deltaTime;
            float t = Math.Clamp(_currentTime / _duration, 0f, 1f);

            // Вычисляем коэффициент на основе формулы
            float easeT = ApplyEasing(_easing, t);

            CurrentPosition = Vector2.Lerp(_start, _end, easeT);

            if (_currentTime >= _duration)
            {
                CurrentPosition = _end;
                IsActive = false;
            }
        }

        private static float ApplyEasing(EasingType type, float t)
        {
            return type switch
            {
                EasingType.Linear => t,
                EasingType.QuadIn => t * t,
                EasingType.QuadOut => t * (2f - t),
                EasingType.QuadInOut => t < 0.5f ? 2f * t * t : -1f + (4f - 2f * t) * t,
                EasingType.CubicOut => --t * t * t + 1f,
                _ => t,
            };
        }
    }
}


