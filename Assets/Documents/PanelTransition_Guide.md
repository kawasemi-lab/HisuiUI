# PanelTransition 使い方ガイド・リファレンス

`PanelTransition` は、ダイアログやウィンドウなどのUIパネルの開閉演出を、フェード・スケール・スライドを組み合わせて滑らかに制御するコンポーネントです。

---

## 1. 主な機能と特徴

- **多彩な開閉演出の組み合わせ**:
  - **フェード**: `CanvasGroup.alpha` の制御。
  - **スケール**: `RectTransform.localScale` の伸縮。
  - **スライド**: 上下左右（`Up`, `Down`, `Left`, `Right`）またはカスタムベクトル（`Custom`）。移動量は「パネルサイズ比（`RelativeToPanelSize`）」または「絶対座標（`Absolute`）」から選択可能。
- **入力・レイキャスト（CanvasGroup）の自動管理**:
  - 開く演出中・閉じる演出中のレイキャスト遮断（`blocksRaycasts`）やボタン操作（`interactable`）の切り替えタイミングを自動管理。
- **途中反転（Reverse）のスムーズ対応**:
  - 開いている途中に閉じる（あるいは閉じている途中に開く）操作が入った場合、現在位置から残り距離に応じて Duration をスケールしつつ滑らかに反転します。
- **初回表示・非アクティブ復帰の最適化**:
  - 初期状態が Closed の場合でも、起動時にメッシュ・マテリアル・レイアウトを内部で安全にウォームアップするため、初回オープン時のチラつきや描画抜け、Burstスパイクによるスキップが起きません。
- **DeactivateOnClose 対応**:
  - 閉完了時に `GameObject.SetActive(false)` にし、開く際に自動で `SetActive(true)` に復帰する設定を選択可能。

---

## 2. 推奨GameObject階層構成

通常、ダイアログやポップアップのルートオブジェクトにアタッチします。

```text
[GameObject] DialogPanel             ← PanelTransition, CanvasGroup, RectTransform
  ├─ [Image] WindowBackground        ← ウィンドウ背景
  ├─ [TextMeshProUGUI] TitleText     ← タイトルテキスト
  └─ [Button] CloseButton            ← 閉じるボタン
```

※ `PanelTransition` を追加すると、同オブジェクトに `CanvasGroup` と `RectTransform` が必須コンポーネントとして自動参照されます。

---

## 3. インスペクタ設定項目

| 項目 | 型 | 説明 |
|---|---|---|
| **Initial State** | `enum` | 起動時の初期状態（`Open`: 開いて開始、`Closed`: 閉じて開始）。 |
| **Interactable Timing** | `enum` | 開く演出時、ボタン入力を有効にするタイミング（`OnOpenStart`: 開き始め、`OnOpenComplete`: 開ききった後）。 |
| **Deactivate On Close** | `bool` | true の場合、閉完了時に `gameObject.SetActive(false)` にします。 |
| **Scale Duration On Reverse** | `bool` | 開閉の途中で反転した際、残り距離に応じて演出時間を短縮するかどうか。 |
| **Open Motion** | `MotionSpec` | 開く演出の時間、イージング（`Ease.OutBack` 等推奨）、ディレイ。 |
| **Close Motion** | `MotionSpec` | 閉じる演出の時間、イージング（`Ease.InCubic` 等推奨）、ディレイ。 |
| **Use Fade** | `bool` | フェード演出を有効にするか。開時・閉時の Alpha 値も指定可能。 |
| **Use Scale** | `bool` | スケール演出を有効にするか。閉状態の LocalScale を指定可能。 |
| **Use Slide** | `bool` | スライド演出を有効にするか。方向・距離（パネル比/絶対値）を指定可能。 |

---

## 4. スクリプトからの使用例

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;
using Hisui.UI.Components;
using Hisui.UI.Core;
using LitMotion;
using UnityEngine;

public class PanelTransitionExample : MonoBehaviour
{
    [SerializeField] private PanelTransition _panel;

    private async void Start()
    {
        // 1. コードからの動的設定（Fluent API / メソッドチェーン）
        _panel.SetInitialState(PanelTransition.InitialStateMode.Closed)
              .SetDeactivateOnClose(true) // 閉じたら自動で SetActive(false)
              .SetOpenMotion(duration: 0.25f, ease: Ease.OutBack) // ポップアップ感のある開き方
              .SetCloseMotion(duration: 0.20f, ease: Ease.InCubic)
              .SetFade(enabled: true, closedAlpha: 0f, openAlpha: 1f)
              .SetScale(enabled: true, uniformClosedScale: 0.9f)
              .SetSlide(enabled: true, direction: PanelTransition.SlideDirection.Down, distance: 0.1f)
              .SetIgnoreTimeScale(true); // ポーズ中でも開閉できるようにする

        await UniTask.Delay(1000);

        // 2. パネルを開く演出（非同期 await で完了待機）
        CancellationToken ct = this.GetCancellationTokenOnDestroy();
        Debug.Log("パネルを開きます...");
        await _panel.OpenAsync(ct);
        Debug.Log("パネルが開き、操作可能になりました！");

        await UniTask.Delay(2000);

        // 3. パネルを閉じる演出
        Debug.Log("パネルを閉じます...");
        await _panel.CloseAsync(ct);
        Debug.Log("パネルが閉じました（非アクティブ化完了）");
    }

    // UIボタン等から同期的に開閉を切り替える例
    public void OnToggleRequested()
    {
        _panel.Toggle();
    }

    // 演出をスキップして即座に開閉状態を変更する例
    public void ForceCloseImmediate()
    {
        _panel.SetOpenImmediate(false);
    }
}
```
