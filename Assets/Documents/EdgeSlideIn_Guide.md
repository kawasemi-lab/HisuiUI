# EdgeSlideIn ガイド

UI 要素を**画面外から**登場させ、画面端へ退場させるコンポーネント（タイトルロゴ、サイドメニュー、リザルト表示など）。  
`Component/Hisui/UI/Edge Slide In`

| | PanelTransition | EdgeSlideIn |
|---|---|---|
| 役割 | パネルの開閉 | 画面外からの登場 / 退場 |
| 移動量 | パネルサイズ基準 or 絶対値 | **画面外位置を自動計算** |

## 制約

- 対応 Canvas: **Screen Space Overlay / Screen Space Camera**。**World Space は対象外**。
- 基準は `Canvas.rootCanvas` の矩形。Target のサイズ・pivot・スケール・回転を考慮し、「画面端から完全に外れる最小位置」＋ `Extra Margin` へ配置する。
- 画面外位置は**再生の直前に毎回**計算する（解像度変更・画面回転・Canvas Scaler に追従）。画面外で待機中にキャンバスサイズが変わった場合も `Update` で追従する。
- コンポーネントを実行時に `AddComponent` した場合は `Reset` が呼ばれないため、`Configure(EdgeSlideIn.Settings.Default)` で初期化すること。

### 親に LayoutGroup がある場合

`LayoutGroup` は子の `anchoredPosition` を毎回上書きするため、スライドが効かない。**ラッパーを挟む**:

```
VerticalLayoutGroup
└─ Wrapper (RectTransform: レイアウト対象。サイズを持つだけ)
   └─ Menu (EdgeSlideIn の Target。ストレッチなどで Wrapper に追従)
```

## 設定

- **Targets**: 空なら自分自身。複数なら stagger で順番に登場（`Forward` / `Reverse` / `FromCenter`。リスト順基準）。
- **Direction**: `Left / Right / Top / Bottom / Custom`（`Custom Direction` に斜めベクトル）。`Alternate Direction` で奇数番目の Target の方向を反転（左右交互）。
- **Separate Out Direction / Out Direction**: OFF（既定）なら退場は来た方向へ戻る。ON にすると退場方向を別指定できる（例: 左から来て右へ退場）。`SetOutDirection(EdgeDirection.Right)` / `SetOutDirectionSameAsIn()`。`Alternate Direction` は退場にも適用される。
- **In / Out**: それぞれ `Motion`（duration / ease / delay）、`Stagger Interval`、`Stagger Order`。
- **Use Fade**: Target 自身の `CanvasGroup`、無ければ `Graphic` の alpha を使う（どちらも無ければフェードなし。CanvasGroup は要求しない）。
- **Start Hidden**（既定 ON）: `OnEnable` の時点で画面外へ配置。**Start Delay** は自動再生 `OnEnable` 時の待ち時間。
- **Auto Out Delay**: `-1` で自動退場しない。`0` 以上なら In 完了からその秒数待って自動で Out を開始（`PlayInAsync` の完了には影響しない）。手動の `PlayOut` / `Stop` / `SetInImmediate` などで予約は解除される。既定は `-1`（`AddComponent` 直後は 0 になるため `Configure(Settings.Default)` か `SetAutoOut(-1)` を呼ぶこと）。
- **Scale Duration On Reverse**: 途中反転時、残り距離に応じて duration を短縮。

> [!NOTE]
> `OutBack` などオーバーシュートする Ease では、到着位置を行き過ぎたり画面外側へ飛び出す場合がある。仕様として許容している（alpha のみ 0–1 にクランプ）。

## 使い方

```csharp
var slide = GetComponent<EdgeSlideIn>();

slide.PlayIn();                         // Play() と同義
await slide.PlayInAsync(ct);            // 全 Target が到着した時点で完了
await slide.PlayOutAsync(ct);           // 全 Target が画面外へ戻った時点で完了

slide.SetInImmediate();                 // 演出なしで到着状態
slide.SetOutImmediate();                // 演出なしで画面外
slide.CaptureRestPosition();            // 到着位置の再取得（到着状態で呼ぶこと）

slide.SetDirection(EdgeDirection.Right)
     .SetStagger(0.1f, StaggerOrder.FromCenter)
     .SetExtraMargin(24f)
     .SetInMotion(0.6f, Ease.OutBack)
     .SetFade(true);

slide.Configure(EdgeSlideIn.Settings.Default);
```

- 逆方向の再生が途中で呼ばれると、現在位置から滑らかに反転する。stagger 中の反転では、**まだ動き出していない Target は動き出さずそのまま待機**する。
- すでに到着済みの状態から `PlayIn` を呼ぶと、画面外から再演出する。
- Cancel / Stop / OnDisable / OnDestroy の挙動は基底（`HisuiMotionBehaviour`）の方針に従い、`PlayAsync` 系は例外を投げず静かに戻る。

## Core 側ヘルパー（将来の `EdgeSlideInText` 向け）

- `ScreenEdgeUtility`（`Hisui.UI.Core`）: `CalculateOffscreenOffset(target, direction, extraMargin)` など。UI コンポーネント非依存。
- `StaggerUtility` / `StaggerOrder`（`Hisui.UI.Core`）: `GetRank` / `GetDelay` / `GetSpan`。stagger の順序・時間計算のみを担当。

文字単位（TextMeshPro 頂点操作）の登場は今回スコープ外。
