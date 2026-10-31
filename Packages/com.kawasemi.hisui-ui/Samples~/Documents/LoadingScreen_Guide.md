# LoadingScreen 使い方ガイド・リファレンス

`LoadingScreen` は、画面全体を覆うローディング表示をフェードイン/アウトとスピナー演出で制御するコンポーネントです。  
**シーン遷移（`LoadSceneAsync`）** と **同一シーン内の処理ブロック（`RunAsync`）** の両方を同一のコンポーネントで扱えます。

---

## 1. 主な機能と役割分担

### 1.1 役割分担（重要）
- **色やスプライトには一切触れません**:
  - 背景色やスピナーの見た目は、プレファブ側の `Image` 等で直接設定します（uPalette を使用したい場合も、プレファブの子要素に uPalette の Synchronizer 等を付与してください）。
  - `LoadingScreen` が担当するのは **フェード・スピナーの回転/拡縮・表示制御・シーン遷移フロー** のみです。
  - `Hisui.UI.Palette` や `uPalette` には一切依存しません。

### 1.2 主な特徴
- **2つの利用形態**:
  - **`LoadSceneAsync`**: `SceneManager.LoadSceneAsync` でロード画面を出したままシーン遷移。新シーンの描画チラつき（`Awake` / `Start` の一巡）を待ってからフェードアウト。
  - **`RunAsync`**: 任意の非同期処理（ステージ再構築、パネル切替、ワープ演出等）をロード画面でラップ。参照カウント対応により複数箇所の同時実行でも破棄タイミングを自動調停。
- **最低表示時間（`MinDisplayTime`）**:
  - 一瞬で処理が終わって画面がチラつくのを防止。計算起点は「フェードイン完了時点」。
- **スピナーアニメーション**:
  - `Rotate`: 連続回転、N分割ステップ回転。
  - `Dots`: 子要素群のスケール周期変化（色には触れず拡縮のみ）。
  - Hide 完了時に回転・スケールを初期姿勢へ自動リセット。
- **シーン間永続化（`PersistAcrossScenes`）**:
  - 有効時はルート Canvas ごと `DontDestroyOnLoad` に登録され、`LoadingScreen.Instance`（static）からアクセス可能。
  - 重複生成された場合は後から来たインスタンスを自動破棄。

---

## 2. 推奨 GameObject 階層構成

`Canvas` モードは **Screen Space - Overlay** を強く推奨します。  
（`Screen Space - Camera` はシーン遷移時にアサインされている Camera が破棄されると表示が崩れる原因となるため、永続化用途では非推奨です）

```text
[GameObject] LoadingScreenCanvas         ← Canvas (Screen Space - Overlay), CanvasScaler, GraphicRaycaster
  └─ [GameObject] LoadingScreen          ← LoadingScreen, CanvasGroup, RectTransform
       ├─ [Image] Background             ← 背景（全画面ストレッチ、黒やテーマ色等）
       └─ [GameObject] SpinnerRoot       ← スピナー親（画面中央配置）
            └─ [Image] SpinnerIcon       ← LoadingScreen._spinner に割り当て（Rotate用）
               (または Dots用の子要素群)
```

> [!NOTE]
> ルートの `CanvasGroup` は `LoadingScreen` にインスペクタから割り当てます（未割り当て時は自動で `GetComponent<CanvasGroup>()` されます）。
> スピナーは `Rotate` の場合は対象の `RectTransform` を、`Dots` の場合は `Settings.dots` に子要素配列を割り当てます。

> [!WARNING]
> 新しいシーン側に別の `EventSystem` が配置されている場合、シーン遷移によって `EventSystem` が複数存在することになります。Unity の仕様により警告や入力不具合の原因となるため、新シーン側で重複 EventSystem を削除する等の対処を行ってください。

---

## 3. インスペクタ設定項目

| カテゴリ | 項目名 | 型 | 説明 |
| :--- | :--- | :--- | :--- |
| **Targets** | `Canvas Group` | `CanvasGroup` | フェードおよび `blocksRaycasts` 制御対象（任意） |
| | `Spinner` | `RectTransform` | `Rotate` モード時の回転対象 Transform（任意） |
| **Fade** | `Show Motion` | `MotionSpec` | 表示時のフェードイン設定（Duration, Ease, Delay） |
| | `Hide Motion` | `MotionSpec` | 非表示時のフェードアウト設定（Duration, Ease, Delay） |
| | `Hide Mode` | `HideMode` | フェードアウト完了後の挙動（`CanvasGroupOnly` / `DeactivateGameObject`） |
| **Timing** | `Min Display Time` | `float` | 最低表示時間（秒）。フェードイン完了時点からの経過時間 |
| | `Wait Frames After Load`| `int` | シーン有効化完了後に新シーンの初期化完了を待つフレーム数（既定: 2） |
| **Scene / Canvas** | `Persist Across Scenes` | `bool` | `DontDestroyOnLoad` でシーン間永続化し `Instance` を有効にするか |
| | `Apply Sorting Order` | `bool` | ルート Canvas の `sortingOrder` を上書きするか |
| | `Sorting Order` | `int` | ルート Canvas に適用するソートオーダー（既定: 1000） |
| | `Initial Visible` | `bool` | 起動直後にロード画面を表示状態にするか |
| **Spinner** | `Spinner Type` | `SpinnerType` | スピナー種別（`None` / `Rotate` / `Dots`） |
| | `Rotate Speed` | `float` | 回転速度（deg/sec） |
| | `Rotate Clockwise` | `bool` | 時計回りに回転するか |
| | `Rotate Step Count` | `int` | ステップ回転分割数（0または1で連続回転、2以上でカクカク回転） |
| | `Dots` | `RectTransform[]` | Dots モードで拡縮させる子要素リスト |
| | `Dots Cycle Duration` | `float` | Dots 拡縮の1周期（秒） |
| | `Dots Phase Step` | `float` | 要素ごとの位相ずらし幅（0以下で 1/要素数） |
| | `Dots Min / Max Scale` | `float` | 拡縮の最小・最大スケール倍率 |

---

## 4. コードからの利用例

### 4.1 シーン遷移（`LoadSceneAsync`）

```csharp
using Cysharp.Threading.Tasks;
using Hisui.UI.Components;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TitleController : MonoBehaviour
{
    public async UniTask OnStartGameButtonClicked()
    {
        // LoadingScreen.Instance 経由でシーン遷移を実行
        await LoadingScreen.Instance.LoadSceneAsync(
            sceneName: "InGameScene",
            mode: LoadSceneMode.Single,
            afterLoad: async ct =>
            {
                // 新シーンの描画前に初期化処理を実行できる（画面は覆われたまま）
                var gameManager = FindFirstObjectByType<InGameManager>();
                if (gameManager != null)
                {
                    await gameManager.InitializeAsync(ct);
                }
            });
    }
}
```

### 4.2 同一シーン内での処理ブロック（`RunAsync`）

```csharp
using Cysharp.Threading.Tasks;
using Hisui.UI.Components;
using UnityEngine;

public class StageManager : MonoBehaviour
{
    [SerializeField] private LoadingScreen _loadingScreen;

    public async UniTask RebuildStageAsync()
    {
        // 画面が完全に覆われてから work が実行され、終了後に自動で Hide される
        await _loadingScreen.RunAsync(async ct =>
        {
            ClearCurrentStage();
            await BuildNextStageAsync(ct);
        }, minDisplayTimeOverride: 1.0f); // 今回だけ演出として最低1秒表示
    }

    public async UniTask<PlayerData> FetchDataAsync()
    {
        // 戻り値のある処理も可能
        return await _loadingScreen.RunAsync(async ct =>
        {
            return await ApiClient.GetPlayerDataAsync(ct);
        });
    }
}
```

### 4.3 手動での表示・非表示（`ShowAsync` / `HideAsync`）

```csharp
// 即座にフェードイン開始（blocksRaycasts は開始時点で true になる）
await loadingScreen.ShowAsync();

// 最低表示時間を待機してからフェードアウト
await loadingScreen.HideAsync();

// 最低表示時間をスキップして即座にフェードアウト
await loadingScreen.HideAsync(skipMinTime: true);

// アニメーションなしで即座に非表示状態にする
loadingScreen.SetShownImmediate(false);
```

### 4.4 メソッドチェーン（Fluent API）

```csharp
loadingScreen
    .SetMinDisplayTime(0.8f)
    .SetShowMotion(0.3f, Ease.OutQuad)
    .SetHideMotion(0.2f, Ease.InQuad)
    .SetRotate(720f, clockwise: true, stepCount: 8);
```
