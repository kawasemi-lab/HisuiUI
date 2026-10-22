using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using LitMotion;
using UnityEngine;

namespace Hisui.UI.Core
{
    /// <summary>
    /// HisuiUI の全アニメーションコンポーネントの共通基底クラス。
    /// <para>
    /// <b>ライフサイクルと再生管理の方針：</b><br/>
    /// ・すべての <see cref="MotionHandle"/> は基底クラスのバッファで一元管理され、<see cref="OnDisable"/> および <see cref="OnDestroy"/> で確実に Cancel される。<br/>
    /// ・再生中に再度 <see cref="Play"/> / <see cref="PlayAsync"/> が呼ばれた場合、進行中のモーションは即座に Cancel され、新しいモーションで上書きされる。<br/>
    /// ・<see cref="PlayAsync"/> は正常完了時に完了し、<see cref="Stop"/> 呼び出し・上書き再生・<see cref="OnDisable"/>・<see cref="OnDestroy"/>・引数 <see cref="CancellationToken"/> のいずれかで中断された場合も
    ///   <b>例外（<see cref="OperationCanceledException"/>）をスローせず静かに完了（早期リターン）する</b>方針とする。
    ///   これにより UI 演出の await 側で毎回 try-catch や SuppressCancellationThrow を書く負担をなくす。
    ///   呼び出し側でキャンセルを判別したい場合は、引数に渡した <see cref="CancellationToken.IsCancellationRequested"/> または <see cref="IsPlaying"/> を参照できる。
    /// </para>
    /// </summary>
    public abstract class HisuiMotionBehaviour : MonoBehaviour
    {
        [Header("Hisui Common")]
        [Tooltip("自動再生のタイミング（None / OnEnable）")]
        [SerializeField]
        private PlayTrigger _playTrigger = PlayTrigger.None;

        [Tooltip("true の場合、Time.timeScale = 0（ポーズ中など）でもモーションを進行させる")]
        [SerializeField]
        private bool _ignoreTimeScale = true;

        /// <summary>
        /// 1コンポーネントあたり最大4つまでの同時 MotionHandle を配列アロケーションなしで保持する固定長スロット。
        /// 5つ以上必要な特殊コンポーネントが現れた場合のみ遅延確保する。
        /// </summary>
        private MotionHandle _slot0;
        private MotionHandle _slot1;
        private MotionHandle _slot2;
        private MotionHandle _slot3;
        private MotionHandle[] _extraSlots;
        private int _trackedCount;

        /// <summary>
        /// 再生世代番号。<see cref="Play"/> や <see cref="Stop"/> が呼ばれるたびにインクリメントし、古い非同期待機を安全に終了させる。
        /// </summary>
        private int _playVersion;

        /// <summary>
        /// 自動再生トリガー設定を取得・設定する。
        /// </summary>
        public PlayTrigger AutoPlayTrigger
        {
            get => _playTrigger;
            set => _playTrigger = value;
        }

        /// <summary>
        /// <c>Time.timeScale</c> を無視して動作するかどうかを取得・設定する。
        /// </summary>
        public bool IgnoreTimeScale
        {
            get => _ignoreTimeScale;
            set => _ignoreTimeScale = value;
        }

        /// <summary>
        /// 現在選んでいる <see cref="IgnoreTimeScale"/> に応じた LitMotion の <see cref="IMotionScheduler"/> を返す。
        /// </summary>
        protected IMotionScheduler CurrentScheduler =>
            _ignoreTimeScale ? MotionScheduler.UpdateIgnoreTimeScale : MotionScheduler.Update;

        /// <summary>
        /// 現在の再生世代番号（派生クラスで非同期シーケンスを組む際の世代チェック用）。
        /// </summary>
        protected int PlayVersion => _playVersion;

        /// <summary>
        /// 管理下のモーションが1つでも再生中（Scheduled / Delayed / Playing）かどうかを返す。
        /// </summary>
        public virtual bool IsPlaying
        {
            get
            {
                if (_trackedCount <= 0) return false;
                if (_slot0.IsActive()) return true;
                if (_trackedCount > 1 && _slot1.IsActive()) return true;
                if (_trackedCount > 2 && _slot2.IsActive()) return true;
                if (_trackedCount > 3 && _slot3.IsActive()) return true;
                if (_extraSlots != null)
                {
                    for (int i = 4; i < _trackedCount; i++)
                    {
                        if (_extraSlots[i - 4].IsActive()) return true;
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// Unity OnEnable イベント。設定に応じて自動再生を行う。
        /// </summary>
        protected virtual void OnEnable()
        {
            if (_playTrigger == PlayTrigger.OnEnable && Application.isPlaying)
            {
                Play();
            }
        }

        /// <summary>
        /// Unity OnDisable イベント。リークや残存再生を防ぐため、進行中の全モーションを確実に停止する。
        /// </summary>
        protected virtual void OnDisable()
        {
            Stop();
        }

        /// <summary>
        /// Unity OnDestroy イベント。進行中の全モーションを確実に停止する。
        /// </summary>
        protected virtual void OnDestroy()
        {
            Stop();
        }

        /// <summary>
        /// モーションを再生する。すでに再生中のモーションがある場合はキャンセルして上書き再生する。
        /// </summary>
        public void Play()
        {
            CancelTrackedMotionsAndIncrementVersion();
            OnPlayCore();
        }

        /// <summary>
        /// 進行中のすべてのモーションを即座にキャンセルして停止する。
        /// </summary>
        public virtual void Stop()
        {
            CancelTrackedMotionsAndIncrementVersion();
        }

        /// <summary>
        /// モーションを再生し、すべてのモーションが完了するまで待機する。
        /// <para>
        /// <b>キャンセル・中断時の挙動：</b><br/>
        /// ・再生中に再度 <see cref="Play"/> / <see cref="PlayAsync"/> が呼ばれた場合は、前のモーションをキャンセルして新しい再生で上書きし、古い <see cref="PlayAsync"/> は例外を投げずに静かに完了する。<br/>
        /// ・<see cref="Stop"/>、<see cref="OnDisable"/>、<see cref="OnDestroy"/>（<c>this.GetCancellationTokenOnDestroy()</c> 相当）、または引数 <paramref name="ct"/> のキャンセル要求が発生した場合は、
        ///   進行中のモーションを即座に Cancel し、<b><see cref="OperationCanceledException"/> をスローせず静かに戻る</b>。
        /// </para>
        /// </summary>
        /// <param name="ct">外部から中断するための <see cref="CancellationToken"/>。</param>
        public virtual UniTask PlayAsync(CancellationToken ct = default)
        {
            if (ct.IsCancellationRequested || this == null)
            {
                return UniTask.CompletedTask;
            }

            Play();
            return WaitTrackedMotionsAsync(_playVersion, ct);
        }

        /// <summary>
        /// 派生クラスで実際のモーションを構築し、<see cref="TrackMotion"/> で登録する処理を実装する。
        /// このメソッドが呼ばれる直前に、既存の管理下モーションはすべてキャンセル済みとなっている。
        /// </summary>
        protected abstract void OnPlayCore();

        /// <summary>
        /// 生成した <see cref="MotionHandle"/> を基底クラスの管理下に登録する。
        /// 登録されたハンドルは <see cref="Stop"/> / <see cref="OnDisable"/> / <see cref="OnDestroy"/> / 再再生時に自動で Cancel される。
        /// </summary>
        /// <param name="handle">登録する <see cref="MotionHandle"/>。</param>
        /// <returns>登録した <see cref="MotionHandle"/> 自身。</returns>
        protected MotionHandle TrackMotion(MotionHandle handle)
        {
            if (!handle.IsActive())
            {
                return handle;
            }

            switch (_trackedCount)
            {
                case 0:
                    _slot0 = handle;
                    break;
                case 1:
                    _slot1 = handle;
                    break;
                case 2:
                    _slot2 = handle;
                    break;
                case 3:
                    _slot3 = handle;
                    break;
                default:
                    int extraIndex = _trackedCount - 4;
                    if (_extraSlots == null)
                    {
                        _extraSlots = new MotionHandle[4];
                    }
                    else if (extraIndex >= _extraSlots.Length)
                    {
                        Array.Resize(ref _extraSlots, _extraSlots.Length * 2);
                    }
                    _extraSlots[extraIndex] = handle;
                    break;
            }

            _trackedCount++;
            return handle;
        }

        /// <summary>
        /// 再生世代番号のみを進め、待機中の古い非同期タスクを無効化する。
        /// </summary>
        protected int IncrementPlayVersion()
        {
            return ++_playVersion;
        }

        /// <summary>
        /// 管理下の全 <see cref="MotionHandle"/> をキャンセルしてスロットをクリアし、再生世代番号をインクリメントする。
        /// </summary>
        protected void CancelTrackedMotionsAndIncrementVersion()
        {
            _playVersion++;

            if (_trackedCount <= 0)
            {
                return;
            }

            _slot0.TryCancelAndClear();
            if (_trackedCount > 1) _slot1.TryCancelAndClear();
            if (_trackedCount > 2) _slot2.TryCancelAndClear();
            if (_trackedCount > 3) _slot3.TryCancelAndClear();

            if (_extraSlots != null)
            {
                for (int i = 4; i < _trackedCount; i++)
                {
                    _extraSlots[i - 4].TryCancelAndClear();
                }
            }

            _trackedCount = 0;
        }

        /// <summary>
        /// 現在管理下にあるすべてのモーションが完了するか、中断条件（世代変更 / <paramref name="ct"/> / Destroy）を満たすまで待機する。
        /// いかなる中断時も例外を投げずに戻る。
        /// </summary>
        protected async UniTask WaitTrackedMotionsAsync(int expectedVersion, CancellationToken ct)
        {
            if (_trackedCount == 0 || !IsPlaying)
            {
                return;
            }

            var destroyToken = this.GetCancellationTokenOnDestroy();

            while (this != null && _playVersion == expectedVersion && IsPlaying)
            {
                if (ct.IsCancellationRequested || destroyToken.IsCancellationRequested)
                {
                    if (_playVersion == expectedVersion)
                    {
                        CancelTrackedMotionsAndIncrementVersion();
                    }
                    return;
                }

                bool canceled = await UniTask.NextFrame(PlayerLoopTiming.Update, destroyToken).SuppressCancellationThrow();
                if (canceled)
                {
                    if (_playVersion == expectedVersion)
                    {
                        CancelTrackedMotionsAndIncrementVersion();
                    }
                    return;
                }

                if (ct.IsCancellationRequested && _playVersion == expectedVersion)
                {
                    CancelTrackedMotionsAndIncrementVersion();
                    return;
                }
            }
        }
    }

    /// <summary>
    /// <typeparamref name="TSettings"/> 構造体による一括設定と、型付きメソッドチェーン（Fluent API）を備えた共通ジェネリック基底クラス。
    /// </summary>
    /// <typeparam name="TSelf">自身のコンポーネント型（CRTP）。</typeparam>
    /// <typeparam name="TSettings"><c>[Serializable]</c> な Settings 構造体型。</typeparam>
    public abstract class HisuiMotionBehaviour<TSelf, TSettings> : HisuiMotionBehaviour
        where TSelf : HisuiMotionBehaviour<TSelf, TSettings>
        where TSettings : struct
    {
        [Header("Motion Settings")]
        [SerializeField]
        private TSettings _settings;

        /// <summary>
        /// 現在の Settings 構造体の参照（読み取り・直接変更用）。
        /// </summary>
        public ref TSettings SettingsRef => ref _settings;

        /// <summary>
        /// 現在の Settings 構造体のコピーを取得する。一括変更には <see cref="Configure(in TSettings)"/> を使用する。
        /// </summary>
        public TSettings CurrentSettings => _settings;

        /// <summary>
        /// Settings 構造体を一括で上書き設定する。
        /// </summary>
        /// <param name="settings">適用する新しい設定。</param>
        /// <returns>メソッドチェーン用の自身への参照。</returns>
        public TSelf Configure(in TSettings settings)
        {
            _settings = settings;
            OnSettingsChanged();
            return (TSelf)this;
        }

        /// <summary>
        /// デリゲートを介して現在の Settings を変更するヘルパー。
        /// </summary>
        /// <param name="modifier">Settings を書き換えるデリゲート。</param>
        /// <returns>メソッドチェーン用の自身への参照。</returns>
        public TSelf Configure(Func<TSettings, TSettings> modifier)
        {
            if (modifier != null)
            {
                _settings = modifier(_settings);
                OnSettingsChanged();
            }
            return (TSelf)this;
        }

        /// <summary>
        /// 自動再生トリガーを設定する（メソッドチェーン対応）。
        /// </summary>
        public TSelf SetAutoPlay(PlayTrigger trigger)
        {
            AutoPlayTrigger = trigger;
            return (TSelf)this;
        }

        /// <summary>
        /// <c>Time.timeScale</c> を無視するかどうかを設定する（メソッドチェーン対応）。
        /// </summary>
        public TSelf SetIgnoreTimeScale(bool ignoreTimeScale)
        {
            IgnoreTimeScale = ignoreTimeScale;
            return (TSelf)this;
        }

        /// <summary>
        /// <see cref="Configure(in TSettings)"/> などで設定が変更された際に呼ばれるフック。
        /// </summary>
        protected virtual void OnSettingsChanged()
        {
        }
    }
}
