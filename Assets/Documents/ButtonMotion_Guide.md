# ButtonMotion ガイド

## 概要
`Button`（`Selectable`）の状態に応じて、**スケールと位置オフセットのみ**を演出する。クリック判定・`onClick`・色／スプライトは標準 `Button` の担当。

## 使い方
1. `Button` と同じ GameObject に `Component/Hisui/UI/Button Motion` を追加（Reset で既定値が入る）。
2. 当たり判定を拡縮させたくない場合は、見た目用の子を作り **Target** に指定する。
3. 色の変化は `Button` の Transition（Color Tint）で行う。

## 値の意味
- スケール: Normal（Awake 時の `localScale`）に対する**倍率**
- 位置オフセット: Normal（Awake 時の `anchoredPosition`）からの**差分**
- 無効（enabled = false）にした状態は Normal と同じ値を目標にする
- 優先順位: Disabled > Pressed > Hover > Selected > Normal

## API
```csharp
var m = GetComponent<ButtonMotion>();
m.SetHoverScale(1.08f).SetPressedScale(0.95f).SetPressedOffset(new Vector2(0, -4));
m.SetState(ButtonMotionState.Normal, immediate: true);
m.PlayClick();                       // = Play()
await m.PlayClickAsync(ct);          // 中断時も例外を投げない
m.Refresh();                         // 状態の再評価
m.ResetImmediate();                  // = Stop()
```
- `Play()` = クリック演出（Punch）の再生 / `Stop()` = 進行中のモーションを止めて Normal へ即座に戻す
- `Button` 以外の `Selectable` では `onClick` 連動しないため、`PlayClick()` を呼ぶ

## 注意
- `Button` の Transition が `Animation` の場合、Animator がスケール・位置を操作すると競合する。併用する場合は `Color Tint` などを使う。
- 他の ButtonMotion や `PanelTransition` と同じ `RectTransform` を操作すると競合する。見た目用の子オブジェクトに分けるなどで回避する。
- 親が `LayoutGroup` の場合、`anchoredPosition` が上書きされる。見た目用の子を Target にする。
- Disabled 検知: `IsInteractable()` / `OnCanvasGroupChanged` / `LateUpdate` の bool 比較。
- ランタイムで `AddComponent` した場合は Reset が走らないため、`Configure(ButtonMotion.Settings.Default)` を呼ぶこと。
