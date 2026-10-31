# HisuiUI 設計書・開発ガイドライン

Unity向けの軽量・ゼロアロケーション志向 UIアニメーションライブラリ「HisuiUI」の設計方針および開発知見のまとめです。  
新規コンポーネント（ButtonMotion 等）を追加する際は、必ず本設計書の流儀に沿って実装してください。

---

## 1. 全体アーキテクチャ・命名規則

### 1.1 パッケージ仕様
- **パッケージ名**: `com.kawasemi.hisui-ui`
- **対象環境**: Unity 6.3 LTS 以降（Unity 6000.3+）
- **対象UIシステム**: uGUI（Canvas / RectTransform / CanvasGroup / Image 等）
- **メニューパス**: `Component/Hisui/UI/...`（各コンポーネントに `[AddComponentMenu]` で付与）

### 1.2 アセンブリ定義（asmdef）と依存方向
依存関係は必ず一方向とし、相互参照や循環参照を起こさない構成とします。
```
Hisui.UI.Core       ← Hisui.UI.Palette       ← Hisui.UI.Components
(uPalette非依存)       (uPalette連携)            (個別UIコンポーネント)
```
- **`Hisui.UI.Core`**:
  - 共通基底クラス（`HisuiMotionBehaviour`）、基本モーション仕様（`MotionSpec`）、LitMotion拡張メソッド、共通列挙型など。
  - **uPalette には一切依存させない**。
- **`Hisui.UI.Palette`**:
  - `uPalette` 連携レイヤー。`HisuiColor`、`HisuiColorBinder` など色のテーマ管理機能を提供。
- **`Hisui.UI.Components`**:
  - 具体的な演出コンポーネント（`DelayGauge`, `PanelTransition` 等）。
  - `Core` および `Palette` を参照。

---

## 2. コア設計原則（最重要）

### 2.1 共通基底クラス (`HisuiMotionBehaviour<TSelf, TSettings>`)
すべてのコンポーネントは `HisuiMotionBehaviour<TSelf, TSettings>` を継承して作成します。
- **再生APIの統一**:
  - 同期再生: `Play()`
  - 停止: `Stop()`
  - 非同期再生: `UniTask PlayAsync(CancellationToken ct = default)`
- **再生上書きルール**:
  - 同一コンポーネントで再生が呼び直された場合、進行中のモーションは即座にキャンセルされ、**新しい再生で上書き**する。
- **例外スロー方針**:
  - `PlayAsync` は、正常完了時に終了する。
  - `Stop()` 呼び出し、上書き再生、`OnDisable`、`OnDestroy`、または引数 `CancellationToken` による中断時も、**例外（`OperationCanceledException`）をスローせず静かに早期リターンする**（UI演出の呼び出し側で try-catch や SuppressCancellationThrow を都度書く負担を排除する）。
- **MotionHandle の一元管理とリーク防止**:
  - 生成した `MotionHandle` は基底の `TrackMotion(handle)` に登録する。
  - `OnDisable()` および `OnDestroy()` で必ず登録済みハンドルを全キャンセルする。
  - アロケーション抑制のため、4スロットまでは配列アロケーションなしで管理する。
- **自動再生トリガー**:
  - `PlayTrigger.None` / `PlayTrigger.OnEnable` を Inspector およびコードから選択可能にする。
  - `[ExecuteAlways]` コンポーネントの場合、Edit モードの OnEnable で誤作動しないよう `Application.isPlaying` を考慮する。
- **TimeScale 無視**:
  - ポーズ中（`Time.timeScale = 0`）でも動作可能にする `IgnoreTimeScale`（`bool`）をサポート。

### 2.2 設定は `Settings` 構造体で保持
- 各コンポーネントは `[Serializable]` な `Settings` 構造体を持ち、Inspectorで編集可能とする。
- **コードからすべて設定可能にする（Inspector専有項目を作らない）**:
  - `Configure(in Settings settings)` による一括上書き。
  - メソッドチェーン（Fluent API: 例 `gauge.SetDamageDelay(...)`）の提供。
- 共通モーション設定部品として `MotionSpec`（duration, ease, delay, customCurve）を活用する。

### 2.3 uPalette 連携とカラーモード
- 色を持つ項目は `HisuiColor` 構造体を用いて「直接色指定（Direct）」と「パレット参照（Palette）」をシームレスに切り替えられるようにする。
- `HisuiColorBinder` を介してバインドすることで、uPalette のテーマ切替イベントを購読し、テーマ切替時のクロスフェードまたは即時切替を自動処理する。
- `HisuiColorBinder` は `IObserver<Color>` を直接実装し、購読ごとのデリゲート生成アロケーションを抑える。

---

## 3. 開発から得られた重要知見・実装ノウハウ

### 3.1 Unity ライフサイクルと初回描画（Canvas 初期化タイミング）
- **`Awake` / `OnEnable` / `Start` はすべてフレーム 0 の Canvas 初回ビルド（`PostLateUpdate`）より前に走る**:
  - 初期状態を非表示（`CanvasGroup.alpha = 0`）にしたいコンポーネントであっても、`Awake` 時点から `alpha = 0` や `SetActive(false)` をしてしまうと、子要素（`Image` / `TextMeshProUGUI` / `uPalette` Synchronizer）の初回メッシュ構築やマテリアル初期化が透明カリング（`m_CullTransparentMesh: 1`）されたままとなり、その後の初回表示演出時にメッシュが描画されない不具合が発生する。
  - **対策**:
    非表示化や `SetActive(false)` の前、あるいは閉状態からの `Open` 開始直前に、一時的に可視状態のコンテキストで `Canvas.ForceUpdateCanvases()` を呼び出し、確実にレイアウト・頂点・マテリアルを一度構築（ウォームアップ）してから非表示化する。

### 3.2 LitMotion の事前ウォームアップ
- 初回のアニメーション呼び出し時に LitMotion の `Storage` / `UpdateRunner` 生成および `MotionUpdateJob` の Burst / JobReflection 初期化が走ると、大きなフレームスパイク（数百ms）が発生する。
- `IgnoreTimeScale = true`（`UnscaledTime`）の場合、`Time.maximumDeltaTime` のクランプが効かないため、このスパイクによって次フレームの `unscaledDeltaTime` が激増し、短いアニメーションが一瞬で完了してしまう。
- **対策**:
  `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]` にて、使用する型（`float`, `Color` 等）のストレージ容量確保およびダミーモーションの即時キャンセルを実行してウォームアップを完了させておく。

### 3.3 イージングのオーバーシュート / アンダースhoot の保護
- `Ease.OutBack` や `Ease.InBack`、`Elastic` などのイージングは、計算途中で `0.0` 未満や `1.0` 超過の値を返す。
- `ApplyVisualProgress(float progress)` の引数 `progress` を安易に `Mathf.Clamp01(progress)` して `Vector3.LerpUnclamped` や `Vector2.LerpUnclamped` に渡すと、イージングの反動・跳ね返り演出がクリップされて平坦化してしまう。
- **対策**:
  アルファ値などのクランプが必須なパラメータのみ個別にクランプし、スケールや位置などの補間計算にはクランプ前の `progress` をそのまま渡す。

### 3.4 uGUI Slider 方式のアンカー制御と 9-Slice 対応
- ゲージ表現において `Image.fillAmount`（`Type.Filled`）を使用すると、9-Slice（`Type.Sliced`）が使えず、枠線の伸縮や角丸が歪む。
- **対策**:
  uGUI 標準の `Slider` と同様に「Area オブジェクト → 子の Fill オブジェクト」の階層構造をとり、子の `RectTransform` の `anchorMin` / `anchorMax`（`LeftToRight` の場合は `anchorMax.x`）を値と同期させる。
- `DrivenRectTransformTracker`（`DrivenTransformProperties.Anchors`）を適用し、Inspectorでの誤操作防止と不要なシーンDirtyフラグの発生を防ぐ。

### 3.5 `[ExecuteAlways]` と Edit モードプレビュー
- インスペクタ上でスライダーを操作した際にエディタ上でアニメーションを再生するには、コンポーネントに `[ExecuteAlways]` を付与する。
- **注意点**:
  - `OnValidate()` のコールスタック内で直接 `RectTransform.anchorMin` / `anchorMax` を書き換えると、Unity の `SendMessage cannot be called during OnValidate` 警告が発生する。
  - **対策**: `OnValidate()` では変更フラグのみを立て、直後の `Update()` や `EditorApplication.QueuePlayerLoopUpdate()` 経由で安全に値変更・モーション開始を行う。
  - Edit モード中もフレームごとの描画更新を行わせるため、モーション値の更新コールバック内で `#if UNITY_EDITOR` による `EditorApplication.QueuePlayerLoopUpdate()` を発行する。

### 3.6 インスペクタの条件付き項目出し分け（`AnimBool` + `BeginFadeGroup`）
- **Unity 標準（`Button.Transition` 等）に準拠した滑らかなアコーディオン開閉**:
  - `bool` トグル（例: `ApplySortingOrder`）や `enum` 選択（例: `SpinnerType`, `SlideDirection`）によって関連項目の表示／非表示を切り替える場合、エディタ拡張（`CustomEditor`）で `UnityEditor.AnimatedValues.AnimBool` と `EditorGUILayout.BeginFadeGroup` を使用して実装する。
  - 単純な `if` による即時トグル切り替えではなく、Unity 標準コンポーネントと同様の滑らかなフェード・伸縮アニメーションを提供することで、Inspector 操作の体験と可読性を向上させる。
- **実装パターン**:
  - `OnEnable` で `new AnimBool(初期値)` を作成し、`animBool.valueChanged.AddListener(Repaint)` を登録（`OnDisable` で解除）。
  - `OnInspectorGUI` 内で `animBool.target = 条件式;` を更新。
  - `if (EditorGUILayout.BeginFadeGroup(animBool.faded))` で囲み、必要に応じて `EditorGUI.indentLevel` を調整して対象プロパティを描画後、`EditorGUILayout.EndFadeGroup()` を呼ぶ。
- **見出しと `[Header]` 属性の共存**:
  - `CustomEditor` 側で手動の `EditorGUILayout.LabelField` による見出しを乱立させず、コンポーネント側の `[Header]` 属性による見出し描画に委ねる（二重見出しの防止）。
- **不要な基底プロパティの非表示**:
  - 当該コンポーネントで使用しない基底のプロパティ（例: `LoadingScreen` における `_playTrigger`）は Inspector から除外して表示を整理する。
