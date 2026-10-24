# Popup & PopupGroup ガイド

UI 要素を**初期位置からのオフセットスライド・拡大縮小・フェード**を組み合わせて登場（In）させ、時間経過・指定 Button クリック・スクリプト呼び出しで退場（Out）させるコンポーネント。  
単体用の `Popup` と、複数要素を束ねて時間差（Stagger）再生や一括退場を制御する `PopupGroup` で構成されます。

* `Component/Hisui/UI/Popup`
* `Component/Hisui/UI/Popup Group`

| | PanelTransition | EdgeSlideIn | Popup / PopupGroup |
|---|---|---|---|
| 主な役割 | 全画面・ダイアログの開閉 | 画面外からの出入り | **その場でのポップアップ出現 / 退場** |
| 移動基準 | パネルサイズ基準 or 絶対値 | **画面端（外側）の自動計算** | **初期基準位置からのオフセット** |
| 複数要素 | 単体向け | 複数 Target を 1 コンポーネントで管理 | **単体 `Popup` を `PopupGroup` で束ねて管理** |
| 個別退場 | - | - | **各要素のボタンや時間経過で独立退場可能** |

---

## 1. コンポーネント概要

### 1.1 `Popup`（単体コンポーネント）
* 対象の `RectTransform`（未指定時は自身）に対してアニメーションを実行。
* **フェード・スケール・オフセットスライド**を任意に組み合わせ可能。
* 登場（In）と退場（Out）で**別々のオフセット位置・スケール・アルファ**を指定可能（例：下から登場して上へ抜けて退場、拡大して登場し等倍のままフェード退場など）。
* 登録した `CloseButton` のクリック、または In 完了後の時間経過（`Auto Out Delay`）により自動退場可能。

### 1.2 `PopupGroup`（グループ管理コンポーネント）
* 配下の複数の `Popup` をリスト管理し、時間差（Stagger）を伴う一括登場・退場を制御。
* インスペクタに **「子階層の Popup を検索して登録」ボタン** を搭載（Prefab 編集や階層変更時もワンクリックで再登録可能）。
* 個別の `Popup` がボタン等で退場した場合でも状態を追跡し、グループからの退場時は**現在表示中の要素のみ**を対象に Stagger を計算。
* 全要素が退場完了した際にグループ自身の GameObject を自動で非アクティブ化するオプション（`Deactivate Group On All Closed`）に対応。

---

## 2. 設定項目

### 2.1 `Popup` の設定

* **Target & Trigger**:
  * `Target`: 対象 `RectTransform`（空なら自身）。
  * `Close Button`: クリック時に自身の `PlayOut()` を呼ぶ `Button`（任意）。
* **Initial & Lifecycle**:
  * `Start Hidden`（既定 ON）: 再生前は非表示状態で待機。`Start()` 時点で Canvas Warmup を行い、初回登場時のメッシュ欠落を防止。
  * `Deactivate On Close`（既定 OFF）: 退場完了時に Target（および自身）の `GameObject.SetActive(false)` を自動実行。
  * `Start Delay`: 自動再生トリガーが `OnEnable` のときの開始遅延（秒）。
  * `Auto Out Delay`: In 完了後に自動退場するまでの待機秒数（`-1` で無効、`0` 以上で有効）。
  * `Scale Duration On Reverse`（既定 ON）: 途中反転時、残り進捗に応じて再生時間を短縮。
* **In / Out Motion**:
  * `In Motion`: 登場のアニメーション仕様（duration / ease / delay）。`Ease.OutBack` 等のオーバーシュートにも対応。
  * `Out Motion`: 退場のアニメーション仕様。
* **Fade**:
  * `Use Fade`: Target の `CanvasGroup`、無ければ `Graphic` の alpha を制御。
  * `In Start Alpha`: 登場開始時のアルファ（到着時は初期基準値）。
  * `Separate Out Alpha / Out End Alpha`: 退場終了時のアルファを個別に指定。
* **Scale**:
  * `Use Scale`: 拡大縮小演出。
  * `In Start Scale`: 登場開始時のスケール（到着時は初期基準値）。
  * `Separate Out Scale / Out End Scale`: 退場終了時のスケールを個別に指定。
* **Slide (Offset from Rest Position)**:
  * `Use Slide`: 初期位置からのオフセットによる移動演出。
  * `Offset Unit`: オフセットの単位（`Absolute`: ピクセル絶対値 / `RelativeToSize`: Target のサイズ比）。
  * `In Offset`: 登場開始時の初期位置からのオフセット（例: `(0, -40)` で 40px 下から登場）。
  * `Separate Out Offset / Out Offset`: 退場先のオフセットを個別に指定（例: `(0, 40)` で上へ抜けて退場）。

> [!NOTE]
> `OutBack` や `Elastic` などのオーバーシュートする Ease を使用した際、位置やスケールはクリップされず自然な反動演出が維持されます（Alpha のみ 0〜1 に安全にクランプされます）。

---

### 2.2 `PopupGroup` の設定

* **Target Popups**:
  * 管理対象の `Popup` リスト。
  * インスペクタの **`[子階層の Popup を検索して登録 (Collect Popups in Children)]` ボタン** で子オブジェクトから自動収集可能（実行時もリストが空なら Awake で自動検索）。
* **In Stagger / Out Stagger**:
  * `In Stagger Interval / Order`: 登場の時間差間隔と順序（`Forward` / `Reverse` / `FromCenter`）。
  * `Out Stagger Interval / Order`: 退場の時間差間隔と順序。
* **Auto Out Settings**:
  * `Auto Out Mode`:
    * `UseEachPopupSetting`: 各 `Popup` 自身の `Auto Out Delay` 設定に従う。
    * `EachPopupOnInComplete`: グループ側の `Auto Out Delay` 秒数を使い、各 `Popup` の In 完了から個別に順次退場（トースト通知のような順次消去）。
    * `AllAfterLastInComplete`: 全要素の In 完了から指定秒数待機し、残っている要素を Stagger 付きで一括退場。
    * `Disabled`: 時間経過による自動退場を行わない（手動またはボタン退場のみ）。
  * `Auto Out Delay`: 上記モードで使用する待機秒数。
* **Initial & Lifecycle**:
  * `Start Delay`: グループ全体の `OnEnable` 自動再生時の開始遅延（秒）。
  * `Deactivate Group On All Closed`: 配下の全 `Popup` が退場完了した時点で（個別のボタンで1つずつ閉じられた場合も含む）、`PopupGroup` 自身の GameObject を `SetActive(false)` にする。

---

## 3. 使い方

### 3.1 スクリプトからの制御

#### 単体 `Popup` の基本操作
```csharp
using Hisui.UI.Components;
using LitMotion;
using UnityEngine;

public class PopupExample : MonoBehaviour
{
    [SerializeField] private Popup _popup;

    private async void Example()
    {
        // 登場（非アクティブ時は自動で SetActive(true) される）
        await _popup.PlayInAsync();

        // 到着状態で待機後、退場
        await _popup.PlayOutAsync();

        // 演出なしで即座に状態を変更
        _popup.SetInImmediate();
        _popup.SetOutImmediate();

        // メソッドチェーンによる動的設定
        _popup.SetSlide(true, new Vector2(0f, -50f), new Vector2(0f, 50f))
              .SetScale(true, new Vector3(0.7f, 0.7f, 1f))
              .SetFade(true, inStartAlpha: 0f)
              .SetInMotion(0.4f, Ease.OutBack)
              .SetAutoOut(3.0f);
    }
}
```

#### `PopupGroup` による一括・時間差操作
```csharp
using Hisui.UI.Components;
using Hisui.UI.Core;
using UnityEngine;

public class PopupGroupExample : MonoBehaviour
{
    [SerializeField] private PopupGroup _group;

    private async void Example()
    {
        // 配下の全要素を Stagger 付きで登場
        await _group.PlayInAsync();

        // 表示中の要素を Stagger 付きで退場
        await _group.PlayOutAsync();

        // メソッドチェーンによるグループ設定
        _group.SetInStagger(0.08f, StaggerOrder.Forward)
              .SetOutStagger(0.05f, StaggerOrder.Reverse)
              .SetAutoOut(PopupGroup.GroupAutoOutMode.AllAfterLastInComplete, delay: 2.5f)
              .SetDeactivateGroupOnAllClosed(true);
    }
}
```

---

## 4. 設計上の特徴と挙動仕様

### 4.1 初期非アクティブからの自動復帰
* GameObject が非アクティブ状態（`startHidden` + `deactivateOnClose` による自動非アクティブ化、または Inspector での非アクティブ）であっても、`PlayIn()` / `PlayInAsync()` / `SetInImmediate()` を呼ぶと**自動的に `SetActive(true)`** されます。
* 初回アクティブ化時に `Canvas.ForceUpdateCanvases()` による Warmup（メッシュ生成・マテリアル同期・レイアウト確定）を描画前に行うため、初回表示時の文字・画像欠落やチラつきが発生しません。

### 4.2 親に LayoutGroup がある場合
* `VerticalLayoutGroup` や `HorizontalLayoutGroup` の直下に `Popup` の Target を置くと、毎フレーム `anchoredPosition` が LayoutGroup に上書きされ、スライド演出が打ち消されます。
* スライド演出を併用する場合は、以下のように**ラッパー（空の RectTransform）** を挟んで配置してください：
```
VerticalLayoutGroup
└─ ItemWrapper (RectTransform: レイアウト対象。レイアウトサイズを持つ)
   └─ PopupItem (Popup の Target。Wrapper 内でスライド)
```

### 4.3 中断・キャンセル時の安全性
* HisuiUI の共通基底（`HisuiMotionBehaviour`）に準拠しており、再生中の上書き再生、`Stop()`、GameObject の非アクティブ化（`OnDisable`）、破棄（`OnDestroy`）、または引数の `CancellationToken` による中断時も、**`OperationCanceledException` をスローせず静かに早期リターン**します。呼び出し側で try-catch を記述する必要はありません。
