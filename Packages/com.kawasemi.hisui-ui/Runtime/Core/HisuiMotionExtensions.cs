using System.Runtime.CompilerServices;
using LitMotion;
using LitMotion.Adapters;
using UnityEngine;

namespace Hisui.UI.Core
{
    /// <summary>
    /// LitMotion の <see cref="MotionBuilder{TValue, TOptions, TAdapter}"/> および <see cref="MotionHandle"/> に対する共通拡張メソッド群。
    /// </summary>
    public static class HisuiMotionExtensions
    {
        /// <summary>
        /// <see cref="MotionSpec"/>（Ease / Delay / CustomAnimationCurve）と TimeScale 無視設定を一括で <see cref="MotionBuilder{TValue, TOptions, TAdapter}"/> に適用する。
        /// </summary>
        /// <remarks>
        /// LitMotion の <c>WithEase(Ease.CustomAnimationCurve)</c> は直接呼ぶと <see cref="System.ArgumentException"/> を投げる仕様のため、
        /// <see cref="Ease.CustomAnimationCurve"/> が選択されている場合は <c>WithEase(AnimationCurve)</c> オーバーロードへ振り分ける。
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MotionBuilder<TValue, TOptions, TAdapter> ApplySpec<TValue, TOptions, TAdapter>(
            this MotionBuilder<TValue, TOptions, TAdapter> builder,
            in MotionSpec spec,
            bool ignoreTimeScale)
            where TValue : unmanaged
            where TOptions : unmanaged, IMotionOptions
            where TAdapter : unmanaged, IMotionAdapter<TValue, TOptions>
        {
            if (spec.ease == Ease.CustomAnimationCurve)
            {
                if (spec.customCurve != null && spec.customCurve.length > 0)
                {
                    builder = builder.WithEase(spec.customCurve);
                }
                else
                {
                    builder = builder.WithEase(Ease.Linear);
                }
            }
            else
            {
                builder = builder.WithEase(spec.ease);
            }

            if (spec.delay > 0f)
            {
                builder = builder.WithDelay(spec.delay);
            }

            builder = builder.WithScheduler(
                ignoreTimeScale ? MotionScheduler.UpdateIgnoreTimeScale : MotionScheduler.Update);

            return builder;
        }

        /// <summary>
        /// <see cref="MotionSpec"/> に基づいて <see cref="float"/> のモーションを生成・設定するヘルパー。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MotionBuilder<float, NoOptions, FloatMotionAdapter> CreateFloat(
            float from,
            float to,
            in MotionSpec spec,
            bool ignoreTimeScale)
        {
            return LMotion.Create(from, to, Mathf.Max(0f, spec.duration))
                .ApplySpec(in spec, ignoreTimeScale);
        }

        /// <summary>
        /// <see cref="MotionSpec"/> に基づいて <see cref="Color"/> のモーションを生成・設定するヘルパー。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MotionBuilder<Color, NoOptions, ColorMotionAdapter> CreateColor(
            Color from,
            Color to,
            in MotionSpec spec,
            bool ignoreTimeScale)
        {
            return LMotion.Create(from, to, Mathf.Max(0f, spec.duration))
                .ApplySpec(in spec, ignoreTimeScale);
        }

        /// <summary>
        /// ハンドルが現在有効かつ再生中（Scheduled / Delayed / Playing）であればキャンセルし、ハンドルを初期化する。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void TryCancelAndClear(ref this MotionHandle handle)
        {
            if (handle.IsActive())
            {
                handle.Cancel();
            }
            handle = default;
        }

        /// <summary>
        /// シーン開始時に LitMotion のストレージ・UpdateRunner・Burst Job を事前ウォームアップし、
        /// 初回アニメーション再生時の初期化スパイクや UnscaledDeltaTime のジャンプを防止する。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void WarmupLitMotion()
        {
            MotionDispatcher.EnsureStorageCapacity<float, NoOptions, FloatMotionAdapter>(8);
            MotionDispatcher.EnsureStorageCapacity<Color, NoOptions, ColorMotionAdapter>(8);

            var updateFloat = LMotion.Create(0f, 1f, 0.01f).WithScheduler(MotionScheduler.Update).RunWithoutBinding();
            updateFloat.Cancel();

            var updateColor = LMotion.Create(Color.black, Color.white, 0.01f).WithScheduler(MotionScheduler.Update).RunWithoutBinding();
            updateColor.Cancel();

            var manualFloat = LMotion.Create(0f, 1f, 0.01f).WithScheduler(MotionScheduler.Manual).RunWithoutBinding();
            var manualColor = LMotion.Create(Color.black, Color.white, 0.01f).WithScheduler(MotionScheduler.Manual).RunWithoutBinding();
            ManualMotionDispatcher.Default.Update(0.001);
            if (manualFloat.IsActive())
            {
                manualFloat.Cancel();
            }
            if (manualColor.IsActive())
            {
                manualColor.Cancel();
            }
            ManualMotionDispatcher.Default.Reset();
        }
    }
}
