using System;
using Hisui.UI.Core;
using LitMotion;
using UnityEngine;
using UnityEngine.UI;

namespace Hisui.UI.Palette
{
    /// <summary>
    /// <see cref="HisuiColor"/> を対象の <see cref="Graphic"/>（<see cref="Image"/> 等）へバインドし、
    /// uPalette のテーマ切替時に自動で色を追従（即時切替またはクロスフェード）させるバインダー。
    /// <para>
    /// <see cref="IObserver{Color}"/> を直接実装することで、毎回のラムダや Observer インスタンス生成による GC アロケーションを防ぐ。
    /// </para>
    /// </summary>
    public sealed class HisuiColorBinder : IObserver<Color>, IDisposable
    {
        private Graphic _target;
        private IDisposable _subscription;
        private MotionHandle _crossfadeHandle;
        private float _crossfadeDuration;
        private bool _ignoreTimeScale = true;
        private bool _isInitializing;
        private Color _currentTargetColor = Color.white;

        private static readonly Action<Color, Graphic> s_setGraphicColor = static (color, graphic) =>
        {
            if (graphic != null)
            {
                graphic.color = color;
            }
        };

        /// <summary>
        /// 現在解決されている目標色（テーマ切替や設定反映後の最終色）。
        /// </summary>
        public Color CurrentTargetColor => _currentTargetColor;

        /// <summary>
        /// 対象の <see cref="Graphic"/> と <see cref="HisuiColor"/> をバインドし、初期色を適用したうえで uPalette のテーマ変更購読を開始する。
        /// </summary>
        /// <param name="target">色を適用する <see cref="Graphic"/>（null 可）。</param>
        /// <param name="hisuiColor">適用する <see cref="HisuiColor"/> 設定。</param>
        /// <param name="crossfadeDuration">テーマ切替時のクロスフェード秒数（0 以下で即時切替）。</param>
        /// <param name="ignoreTimeScale">クロスフェード時に <c>Time.timeScale</c> を無視するかどうか。</param>
        public void Bind(Graphic target, in HisuiColor hisuiColor, float crossfadeDuration = 0.15f, bool ignoreTimeScale = true)
        {
            Unbind();

            _target = target;
            _crossfadeDuration = Mathf.Max(0f, crossfadeDuration);
            _ignoreTimeScale = ignoreTimeScale;

            if (hisuiColor.TryGetObservableProperty(out var observableProperty))
            {
                _isInitializing = true;
                _subscription = observableProperty.Subscribe(this);
                _isInitializing = false;
            }
            else
            {
                ApplyImmediate(hisuiColor.directColor);
            }
        }

        /// <summary>
        /// 購読および進行中のクロスフェードモーションを解除する。
        /// </summary>
        public void Unbind()
        {
            _crossfadeHandle.TryCancelAndClear();
            if (_subscription != null)
            {
                _subscription.Dispose();
                _subscription = null;
            }
        }

        /// <summary>
        /// 現在バインド中のターゲットに対して、即座に指定色を適用する（進行中のクロスフェードはキャンセルされる）。
        /// </summary>
        public void ApplyImmediate(Color color)
        {
            _crossfadeHandle.TryCancelAndClear();
            _currentTargetColor = color;
            if (_target != null)
            {
                _target.color = color;
            }
        }

        /// <inheritdoc />
        void IObserver<Color>.OnNext(Color value)
        {
            _currentTargetColor = value;
            if (_target == null)
            {
                return;
            }

            _crossfadeHandle.TryCancelAndClear();

            // 初回購読時・非再生中・duration <= 0 の場合は即時反映
            if (_isInitializing || !Application.isPlaying || _crossfadeDuration <= 0f || !_target.isActiveAndEnabled)
            {
                _target.color = value;
                return;
            }

            var scheduler = _ignoreTimeScale ? MotionScheduler.UpdateIgnoreTimeScale : MotionScheduler.Update;
            _crossfadeHandle = LMotion.Create(_target.color, value, _crossfadeDuration)
                .WithEase(Ease.OutQuad)
                .WithScheduler(scheduler)
                .Bind(_target, s_setGraphicColor);
        }

        /// <inheritdoc />
        void IObserver<Color>.OnError(Exception error)
        {
        }

        /// <inheritdoc />
        void IObserver<Color>.OnCompleted()
        {
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Unbind();
            _target = null;
        }
    }
}
