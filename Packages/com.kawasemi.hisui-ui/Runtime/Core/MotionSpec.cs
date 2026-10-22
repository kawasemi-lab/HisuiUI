using System;
using LitMotion;
using UnityEngine;

namespace Hisui.UI.Core
{
    /// <summary>
    /// 単一モーションの基本パラメータ（Duration / Ease / Delay / CustomCurve）を保持する共通設定構造体。
    /// 各コンポーネントの Settings 構造体内で部品として再利用する。
    /// </summary>
    [Serializable]
    public struct MotionSpec : IEquatable<MotionSpec>
    {
        /// <summary>再生時間（秒）。0 以下の場合は即時完了として扱う。</summary>
        [Tooltip("再生時間（秒）")]
        [Min(0f)]
        public float duration;

        /// <summary>イージング種別。<see cref="Ease.CustomAnimationCurve"/> を選んだ場合は <see cref="customCurve"/> が使われる。</summary>
        [Tooltip("イージング種別（CustomAnimationCurve 選択時は Custom Curve を使用）")]
        public Ease ease;

        /// <summary>開始までの遅延時間（秒）。</summary>
        [Tooltip("開始までの遅延時間（秒）")]
        [Min(0f)]
        public float delay;

        /// <summary><see cref="ease"/> が <see cref="Ease.CustomAnimationCurve"/> のときに使用するカーブ。</summary>
        [Tooltip("Ease に CustomAnimationCurve を指定した場合に使用する AnimationCurve")]
        public AnimationCurve customCurve;

        /// <summary>
        /// 指定したパラメータで <see cref="MotionSpec"/> を生成する。
        /// </summary>
        /// <param name="duration">再生時間（秒）。</param>
        /// <param name="ease">イージング種別。</param>
        /// <param name="delay">遅延時間（秒）。</param>
        /// <param name="customCurve">カスタムカーブ（任意）。</param>
        public MotionSpec(float duration, Ease ease = Ease.OutCubic, float delay = 0f, AnimationCurve customCurve = null)
        {
            this.duration = Mathf.Max(0f, duration);
            this.ease = ease;
            this.delay = Mathf.Max(0f, delay);
            this.customCurve = customCurve;
        }

        /// <summary>
        /// デフォルトのモーション設定（0.25秒, OutCubic, Delay 0）。
        /// </summary>
        public static MotionSpec Default => new MotionSpec(0.25f, Ease.OutCubic, 0f);

        /// <summary>
        /// 再生時間を変更した新しい <see cref="MotionSpec"/> を返す。
        /// </summary>
        public MotionSpec WithDuration(float newDuration)
        {
            var copy = this;
            copy.duration = Mathf.Max(0f, newDuration);
            return copy;
        }

        /// <summary>
        /// イージングを変更した新しい <see cref="MotionSpec"/> を返す。
        /// </summary>
        public MotionSpec WithEase(Ease newEase)
        {
            var copy = this;
            copy.ease = newEase;
            return copy;
        }

        /// <summary>
        /// カスタム AnimationCurve を設定した新しい <see cref="MotionSpec"/> を返す。
        /// </summary>
        public MotionSpec WithCustomCurve(AnimationCurve curve)
        {
            var copy = this;
            copy.ease = Ease.CustomAnimationCurve;
            copy.customCurve = curve;
            return copy;
        }

        /// <summary>
        /// 遅延時間を変更した新しい <see cref="MotionSpec"/> を返す。
        /// </summary>
        public MotionSpec WithDelay(float newDelay)
        {
            var copy = this;
            copy.delay = Mathf.Max(0f, newDelay);
            return copy;
        }

        /// <inheritdoc />
        public bool Equals(MotionSpec other)
        {
            return Mathf.Approximately(duration, other.duration)
                && ease == other.ease
                && Mathf.Approximately(delay, other.delay)
                && Equals(customCurve, other.customCurve);
        }

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is MotionSpec other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine(duration, (int)ease, delay, customCurve);
    }
}
