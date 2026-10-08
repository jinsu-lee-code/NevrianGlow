using System;

namespace NevrianGlowFarm.Logic.Player
{
    /// <summary>1인칭 시점 · 헤드밥 계산 (노션 GDD 12.3). 엔진 없이 테스트할 수 있게 각도는 도(°) 단위 float로 다룬다.</summary>
    public static class FirstPersonMath
    {
        /// <summary>
        /// 마우스 이동량(px) × 감도(°/px)로 yaw · pitch를 바꾼다. 마우스를 위로 올리면(deltaY &gt; 0) 위를 본다(pitch 감소).
        /// yaw는 0~360으로 감고, pitch는 ±pitchLimit로 자른다.
        /// </summary>
        public static void ApplyLook(ref float yaw, ref float pitch, float deltaX, float deltaY, float sensitivity, float pitchLimit)
        {
            yaw = WrapDegrees(yaw + deltaX * sensitivity);
            pitch = Clamp(pitch - deltaY * sensitivity, -pitchLimit, pitchLimit);
        }

        /// <summary>헤드밥 위상(0~1 순환)을 진행시킨다. 움직이지 않으면 위상을 0으로 되돌린다.</summary>
        public static float AdvanceBobPhase(float phase, float frequencyHz, float speedRatio, float deltaTime, bool isMoving)
        {
            if (!isMoving)
                return 0f;
            float next = phase + frequencyHz * speedRatio * deltaTime;
            return next - (float)Math.Floor(next);
        }

        /// <summary>헤드밥 높이 변화 (m). 위상 0 · 0.5 · 1에서 0.</summary>
        public static float BobOffset(float phase, float amplitude)
        {
            return amplitude * (float)Math.Sin(phase * 2.0 * Math.PI);
        }

        private static float WrapDegrees(float degrees)
        {
            float wrapped = degrees % 360f;
            return wrapped < 0f ? wrapped + 360f : wrapped;
        }

        private static float Clamp(float value, float min, float max)
        {
            return value < min ? min : value > max ? max : value;
        }
    }
}
