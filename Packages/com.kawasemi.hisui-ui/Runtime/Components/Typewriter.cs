using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Hisui.UI.Core;
using LitMotion;
using TMPro;
using UnityEngine;

namespace Hisui.UI.Components
{
    /// <summary>
    /// TextMeshPro を対象とした文字送り（Typewriter）演出コンポーネント。
    /// <para>
    /// <b>特徴：</b><br/>
    /// ・TextMeshPro（<see cref="TMP_Text"/>）専用設計。<br/>
    /// ・空欄から1文字ずつテキスト送りで表示。<br/>
    /// ・テキスト表示スピード（毎秒何文字 / characters per second）、イージング（デフォルトは Linear）を柔軟に設定可能。<br/>
    /// ・スクリプトからの動的なテキスト流し込み（<see cref="SetText"/>, <see cref="Play(string)"/>, <see cref="PlayAsync(string, CancellationToken)"/>）に対応。<br/>
    /// ・<b>行・列の中央揃えガタつき防止（<see cref="Settings.preventJitter"/>）：</b><br/>
    ///   中央揃え（Center / Middle）設定時に文字を順次追加すると、文字数増加に伴い全体の幅・高さが変わり既存の文字座標が毎文字左右・上下にガタガタと揺れてしまいます。
    ///   本コンポーネントでは、あらかじめ全文のレイアウトとメッシュ情報を確定させた上で <see cref="TMP_Text.maxVisibleCharacters"/> を用いて可視文字数を補間するため、
    ///   中央揃えであってもレイアウトが一切ガタつかず、滑らかに文字送りを行えます。
    /// </para>
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("Hisui/UI/Typewriter")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class Typewriter : HisuiMotionBehaviour<Typewriter, Typewriter.Settings>
    {
        /// <summary>
        /// <see cref="Typewriter"/> の全設定を保持するシリアライズ可能構造体。
        /// </summary>
        [Serializable]
        public struct Settings : IEquatable<Settings>
        {
            [Header("Speed & Timing")]
            [Tooltip("テキスト表示スピード（毎秒何文字表示するか）")]
            [Min(0.1f)]
            public float charactersPerSecond;

            [Tooltip("再生開始までの待機時間（秒）")]
            [Min(0f)]
            public float startDelay;

            [Header("Easing")]
            [Tooltip("テキスト送りのイージング種別（デフォルトは Linear）")]
            public Ease ease;

            [Tooltip("Ease に CustomAnimationCurve を指定した場合に使用する AnimationCurve")]
            public AnimationCurve customCurve;

            [Header("Layout & Appearance")]
            [Tooltip("行・列の中央揃え設定時に、maxVisibleCharacters を用いてレイアウト幅・位置を固定し、文字送り中のガタつき（左右・上下の揺れ）を防止します")]
            public bool preventJitter;

            /// <summary>
            /// デフォルト設定（毎秒30文字、Linear、中央揃えガタつき防止 ON）。
            /// </summary>
            public static Settings Default => new Settings
            {
                charactersPerSecond = 30f,
                startDelay = 0f,
                ease = Ease.Linear,
                customCurve = null,
                preventJitter = true,
            };

            /// <summary>
            /// 指定したパラメータで <see cref="Settings"/> を生成する。
            /// </summary>
            public Settings(
                float charactersPerSecond = 30f,
                Ease ease = Ease.Linear,
                bool preventJitter = true,
                float startDelay = 0f,
                AnimationCurve customCurve = null)
            {
                this.charactersPerSecond = Mathf.Max(0.1f, charactersPerSecond);
                this.startDelay = Mathf.Max(0f, startDelay);
                this.ease = ease;
                this.customCurve = customCurve;
                this.preventJitter = preventJitter;
            }

            public bool Equals(Settings other)
            {
                return Mathf.Approximately(charactersPerSecond, other.charactersPerSecond)
                    && Mathf.Approximately(startDelay, other.startDelay)
                    && ease == other.ease
                    && preventJitter == other.preventJitter
                    && Equals(customCurve, other.customCurve);
            }

            public override bool Equals(object obj) => obj is Settings other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = charactersPerSecond.GetHashCode();
                    hash = (hash * 397) ^ startDelay.GetHashCode();
                    hash = (hash * 397) ^ (int)ease;
                    hash = (hash * 397) ^ preventJitter.GetHashCode();
                    if (customCurve != null) hash = (hash * 397) ^ customCurve.GetHashCode();
                    return hash;
                }
            }
        }

        [Header("Text Source")]
        [Tooltip("Inspector から事前設定するテキスト。空の場合は TMP_Text の既存文字列が使用されます")]
        [TextArea(3, 8)]
        [SerializeField]
        private string _defaultText;

        [SerializeField, HideInInspector]
        private TMP_Text _targetText;

        private string _currentText = string.Empty;
        private int _totalCharacterCount;
        private int _currentVisibleCount;
        private float _progress;
        private bool _isInitialized;

        private Action _onCompleteDelegate;

        private static readonly Action<float, Typewriter> s_tickCallback = static (t, self) =>
        {
            self.ApplyProgress(t);
        };

        /// <summary>
        /// 1文字新しく表示されるたびに発火するイベント。引数は新しく表示された可視文字インデックス（1始まり）。
        /// タイプライター音（SE）の再生などに使用できます。
        /// </summary>
        public event Action<int> OnCharacterRevealed;

        /// <summary>
        /// 文字送りが完了したときに発火するイベント。
        /// </summary>
        public event Action OnCompleted;

        /// <summary>
        /// 制御対象の <see cref="TMP_Text"/> コンポーネント。
        /// </summary>
        public TMP_Text TargetText
        {
            get
            {
                if (_targetText == null)
                {
                    _targetText = GetComponent<TMP_Text>();
                }
                return _targetText;
            }
        }

        /// <summary>
        /// 現在設定されている全文文字列を取得または設定する。
        /// </summary>
        public string Text
        {
            get => _currentText;
            set => SetText(value, play: false);
        }

        /// <summary>
        /// 現在表示されている文字数（可視文字数）。
        /// </summary>
        public int CurrentVisibleCharacters => _currentVisibleCount;

        /// <summary>
        /// 全文の総文字数（リッチテキストタグを除いた可視文字数）。
        /// </summary>
        public int TotalCharacters => _totalCharacterCount;

        /// <summary>
        /// 現在の文字送り進捗率（0.0 〜 1.0）。
        /// </summary>
        public float Progress => _progress;

        /// <summary>
        /// テキスト表示スピード（毎秒何文字か）。
        /// </summary>
        public float CharactersPerSecond
        {
            get => CurrentSettings.charactersPerSecond;
            set => Configure(s => { s.charactersPerSecond = Mathf.Max(0.1f, value); return s; });
        }

        /// <summary>
        /// イージング種別。
        /// </summary>
        public Ease Ease
        {
            get => CurrentSettings.ease;
            set => Configure(s => { s.ease = value; return s; });
        }

        /// <summary>
        /// 行・列の中央揃えガタつき防止（maxVisibleCharacters 方式）の有効フラグ。
        /// </summary>
        public bool PreventJitter
        {
            get => CurrentSettings.preventJitter;
            set => Configure(s => { s.preventJitter = value; return s; });
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void Reset()
        {
            _targetText = GetComponent<TMP_Text>();
            Configure(Settings.Default);
            if (_targetText != null)
            {
                _defaultText = _targetText.text;
            }
        }

        private void OnValidate()
        {
            if (_targetText == null)
            {
                _targetText = GetComponent<TMP_Text>();
            }
        }

        protected override void OnEnable()
        {
            EnsureInitialized();
            base.OnEnable();
        }

        private void EnsureInitialized()
        {
            if (_isInitialized) return;

            if (_targetText == null)
            {
                _targetText = GetComponent<TMP_Text>();
            }

            _onCompleteDelegate = OnMotionComplete;

            if (_targetText != null)
            {
                // ガイドライン 3.1: Canvas 初回構築前に ForceMeshUpdate を行いメッシュと頂点情報をウォームアップ
                if (!string.IsNullOrEmpty(_defaultText))
                {
                    _currentText = _defaultText;
                }
                else
                {
                    _currentText = _targetText.text;
                }

                _targetText.text = _currentText;
                _targetText.ForceMeshUpdate();
                _totalCharacterCount = _targetText.textInfo.characterCount;

                // 初期状態は常に全文字表示（シーン読み込み時やエディタ配置時に空白にならないようにする）
                _currentVisibleCount = _totalCharacterCount;
                _progress = 1f;
                _targetText.maxVisibleCharacters = int.MaxValue;
            }

            _isInitialized = true;
        }

        /// <summary>
        /// 文字送りモーションを開始する。
        /// </summary>
        protected override void OnPlayCore()
        {
            EnsureInitialized();

            if (_targetText == null)
            {
                return;
            }

            // テキストが未設定の場合は初期テキストを適用
            if (string.IsNullOrEmpty(_currentText))
            {
                _currentText = !string.IsNullOrEmpty(_defaultText) ? _defaultText : _targetText.text;
            }

            PrepareTextForPlayback(_currentText);

            if (_totalCharacterCount <= 0)
            {
                CompleteVisual();
                OnCompleted?.Invoke();
                return;
            }

            float cps = Mathf.Max(0.1f, CurrentSettings.charactersPerSecond);
            float duration = _totalCharacterCount / cps;

            var spec = new MotionSpec(duration, CurrentSettings.ease, CurrentSettings.startDelay, CurrentSettings.customCurve);

            var handle = LMotion.Create(0f, 1f, duration)
                .ApplySpec(in spec, IgnoreTimeScale)
                .WithOnComplete(_onCompleteDelegate)
                .Bind(this, s_tickCallback);

            TrackMotion(handle);
        }

        /// <summary>
        /// 指定した文字列をセットして文字送りアニメーションを再生する。
        /// </summary>
        /// <param name="text">表示するテキスト。</param>
        public void Play(string text)
        {
            SetText(text, play: true);
        }

        /// <summary>
        /// 指定した文字列をセットして文字送りアニメーションを再生し、完了まで待機する。
        /// </summary>
        /// <param name="text">表示するテキスト。</param>
        /// <param name="ct">キャンセル用トークン。</param>
        public UniTask PlayAsync(string text, CancellationToken ct = default)
        {
            if (ct.IsCancellationRequested || this == null)
            {
                return UniTask.CompletedTask;
            }

            Play(text);
            return WaitTrackedMotionsAsync(PlayVersion, ct);
        }

        /// <summary>
        /// テキストを動的にセットする。
        /// </summary>
        /// <param name="text">セットするテキスト文字列。</param>
        /// <param name="play">true の場合、即座に文字送りアニメーションを開始する。</param>
        public void SetText(string text, bool play = false)
        {
            EnsureInitialized();

            _currentText = text ?? string.Empty;

            if (play)
            {
                Play();
            }
            else
            {
                Stop();
                PrepareTextForPlayback(_currentText);
                CompleteVisual();
            }
        }

        /// <summary>
        /// 進行中の文字送りを即座にスキップし、すべての文字を完全表示状態にする。
        /// </summary>
        public void Complete()
        {
            if (!IsPlaying && _currentVisibleCount >= _totalCharacterCount)
            {
                return;
            }

            Stop();
            CompleteVisual();
            OnCompleted?.Invoke();
        }

        /// <summary>
        /// <see cref="Complete"/> のエイリアス（会話送りスキップ用）。
        /// </summary>
        public void Skip() => Complete();

        /// <summary>
        /// テキスト表示を空欄（0文字表示）にし、進行中のモーションを停止する。
        /// </summary>
        public void Clear()
        {
            Stop();
            ClearVisual();
        }

        /// <summary>
        /// 表示文字数を任意の値に直接設定する。
        /// </summary>
        /// <param name="visibleCount">表示する可視文字数。</param>
        public void SetVisibleCharacterCount(int visibleCount)
        {
            Stop();
            EnsureInitialized();

            visibleCount = Mathf.Clamp(visibleCount, 0, _totalCharacterCount);
            UpdateVisibleCharacters(visibleCount);
        }

        private void PrepareTextForPlayback(string text)
        {
            if (_targetText == null) return;

            _targetText.text = text;

            // 中央揃えガタつき防止の鍵：
            // 全文がセットされた状態で ForceMeshUpdate を行い、行・列・中央揃え（Horizontal/Vertical Alignment）の
            // 最終レイアウトおよび textInfo を確定させる。
            _targetText.ForceMeshUpdate();
            _totalCharacterCount = _targetText.textInfo.characterCount;

            ClearVisual();
        }

        private void ClearVisual()
        {
            _currentVisibleCount = 0;
            _progress = 0f;

            if (_targetText == null) return;

            if (CurrentSettings.preventJitter)
            {
                _targetText.maxVisibleCharacters = 0;
            }
            else
            {
                _targetText.maxVisibleCharacters = int.MaxValue;
                _targetText.text = string.Empty;
            }
        }

        private void CompleteVisual()
        {
            _currentVisibleCount = _totalCharacterCount;
            _progress = 1f;

            if (_targetText == null) return;

            if (CurrentSettings.preventJitter)
            {
                _targetText.maxVisibleCharacters = int.MaxValue;
            }
            else
            {
                _targetText.maxVisibleCharacters = int.MaxValue;
                _targetText.text = _currentText;
            }
        }

        private void ApplyProgress(float progress)
        {
            _progress = progress;

            int targetCount;
            if (progress >= 1f)
            {
                targetCount = _totalCharacterCount;
            }
            else
            {
                // イージングによってオーバーシュート（0未満または1超過）が発生した場合の保護（ガイドライン 3.3）
                float clampedProgress = Mathf.Clamp01(progress);
                targetCount = Mathf.Clamp(Mathf.FloorToInt(clampedProgress * _totalCharacterCount), 0, _totalCharacterCount);
            }

            if (targetCount != _currentVisibleCount)
            {
                int prevCount = _currentVisibleCount;
                UpdateVisibleCharacters(targetCount);

                // 新しく表示された文字ごとにコールバックを発火
                if (targetCount > prevCount && OnCharacterRevealed != null)
                {
                    for (int i = prevCount + 1; i <= targetCount; i++)
                    {
                        OnCharacterRevealed.Invoke(i);
                    }
                }
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
            }
#endif
        }

        private void UpdateVisibleCharacters(int count)
        {
            _currentVisibleCount = count;

            if (_targetText == null) return;

            if (CurrentSettings.preventJitter)
            {
                // maxVisibleCharacters 方式: 全文レイアウト（中央揃え座標）を保持したまま文字のみを可視化
                _targetText.maxVisibleCharacters = count;
            }
            else
            {
                // ガタつき防止 OFF の場合: 文字列そのものを順次追加（中央揃え設定時は幅が変わってガタつく従来の挙動）
                _targetText.maxVisibleCharacters = int.MaxValue;
                _targetText.text = GetSubstringForVisibleCount(_currentText, count);
            }
        }

        private string GetSubstringForVisibleCount(string source, int visibleCount)
        {
            if (string.IsNullOrEmpty(source) || visibleCount <= 0)
            {
                return string.Empty;
            }

            if (visibleCount >= _totalCharacterCount)
            {
                return source;
            }

            // TMP_TextInfo から指定可視文字数に対応するソース文字列インデックスを取得
            var textInfo = _targetText.textInfo;
            if (textInfo != null && textInfo.characterInfo != null && visibleCount <= textInfo.characterCount)
            {
                int charIndex = visibleCount - 1;
                if (charIndex >= 0 && charIndex < textInfo.characterInfo.Length)
                {
                    int stringIndex = textInfo.characterInfo[charIndex].index;
                    int length = Mathf.Clamp(stringIndex + 1, 0, source.Length);
                    return source.Substring(0, length);
                }
            }

            int fallbackLength = Mathf.Clamp(visibleCount, 0, source.Length);
            return source.Substring(0, fallbackLength);
        }

        private void OnMotionComplete()
        {
            CompleteVisual();
            OnCompleted?.Invoke();
        }

        #region Fluent API (Method Chaining)

        /// <summary>
        /// テキスト表示スピード（毎秒何文字か）を設定する。
        /// </summary>
        public Typewriter SetSpeed(float charactersPerSecond)
        {
            return Configure(s =>
            {
                s.charactersPerSecond = Mathf.Max(0.1f, charactersPerSecond);
                return s;
            });
        }

        /// <summary>
        /// イージング種別を設定する。
        /// </summary>
        public Typewriter SetEase(Ease ease)
        {
            return Configure(s =>
            {
                s.ease = ease;
                return s;
            });
        }

        /// <summary>
        /// カスタム AnimationCurve によるイージングを設定する。
        /// </summary>
        public Typewriter SetCustomCurve(AnimationCurve curve)
        {
            return Configure(s =>
            {
                s.ease = Ease.CustomAnimationCurve;
                s.customCurve = curve;
                return s;
            });
        }

        /// <summary>
        /// 行・列の中央揃えガタつき防止（maxVisibleCharacters 方式）の有効無効を設定する。
        /// </summary>
        public Typewriter SetPreventJitter(bool preventJitter)
        {
            return Configure(s =>
            {
                s.preventJitter = preventJitter;
                return s;
            });
        }

        /// <summary>
        /// 再生開始前の遅延時間（秒）を設定する。
        /// </summary>
        public Typewriter SetStartDelay(float startDelay)
        {
            return Configure(s =>
            {
                s.startDelay = Mathf.Max(0f, startDelay);
                return s;
            });
        }

        #endregion
    }
}
