# HisuiUI (`com.kawasemi.hisui-ui`)

Unity (uGUI) 向けの軽量・ゼロアロケーション志向 UI アニメーションライブラリです。  
**LitMotion** による高速なトゥイーン、**UniTask** による直感的な `async/await` 制御、**uPalette** によるカラーテーマ自動追従を統合しています。

## 必要環境・依存パッケージ

- Unity 6.3 LTS (6000.3) 以降
- `com.unity.ugui`
- `com.cysharp.unitask`
- `com.annulusgames.lit-motion`
- `com.harumak.upalette`（※ git URL 導入パッケージのため `package.json` の `dependencies` には含めていません。プロジェクトに導入済みの状態でご使用ください）

## アセンブリ構成

```
Hisui.UI.Core       ← Hisui.UI.Palette ← Hisui.UI.Components
(uPalette非依存)      (uPalette連携)     (DelayGauge / PanelTransition)
```

## 収録コンポーネント（初期実装）

- **`DelayGauge`** (`Component/Hisui/UI/Delay Gauge`)
  - uGUI `Slider` と同様の `RectTransform` アンカー制御（9-Slice 対応）による、即時側（前面）と遅延・先行側（背面）の 2 層ゲージ演出（ダメージ時：即時減少＋遅延追従、回復時：先行増加＋メイン追従、インスペクタ上の `Value` 操作でもリアルタイムにプレビュー再生）。
- **`PanelTransition`** (`Component/Hisui/UI/Panel Transition`)
  - フェード・スケール・スライド（上下左右・パネルサイズ比 / 絶対値）を組み合わせ可能なパネル開閉演出と `CanvasGroup` 自動制御。
- **`EdgeSlideIn`** (`Component/Hisui/UI/Edge Slide In`)
  - 画面端（ルート Canvas の矩形）の外から登場／退場する演出。画面外位置は再生直前に自動計算（`ScreenEdgeUtility`）。複数 Target の stagger、In/Out 個別設定、途中反転、フェード併用に対応。
  - 対応 Canvas: Screen Space Overlay / Screen Space Camera（**World Space は対象外**）。
  - 親に `LayoutGroup` がある場合は位置が上書きされるため、ラッパーの `RectTransform` を挟むこと。
  - 詳細: `Assets/Documents/EdgeSlideIn_Guide.md`
