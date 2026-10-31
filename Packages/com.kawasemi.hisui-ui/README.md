# HisuiUI (`com.kawasemi.hisui-ui`)

Unity (uGUI) 向けの軽量・ゼロアロケーション志向 UI アニメーションライブラリです。  
**LitMotion** による高速なトゥイーン、**UniTask** による直感的な `async/await` 制御、**uPalette** によるカラーテーマ自動追従を統合しています。

---

## 必要環境・依存パッケージ

- Unity 6.3 LTS (6000.3) 以降
- `com.unity.ugui`
- `com.unity.textmeshpro`
- `com.cysharp.unitask`
- `com.annulusgames.lit-motion`
- `com.harumak.upalette`（※ git URL 導入パッケージのため、プロジェクトに導入済みの状態でご使用ください）

---

## サンプルプレファブによる簡単UI構築

`Assets/Samples/Prefabs` に、デザインとモーション設定済みのプレファブが多数収録されています。  
これらをシーン上にドラッグ＆ドロップして組み合わせるだけで、リッチなUIを素早く簡単に作成できます。

- **`Elements`（単体パーツ）**
  - `CustomButton`: 連打防止・SE連携が組み込まれたボタン
  - `DelayGauge`: ダメージ／回復の2層HPゲージ
  - `PopupPanel`: スケール・フェード付きのポップアップウィンドウ
  - `SlideMessage_Single` / `SlideMessage_WithSub`: 画面端からスライドインする通知バー
  - `Slider`: スタイリング済みのスライダー
  - `TypewriterText`: 文字送り（TMP）テキスト
- **`Examples`（実践的な組み合わせサンプル）**
  - `SettingsPanel`: 音量スライダーやボタンをまとめた設定パネル
  - `Card` / `CardPopup`: カード型UIおよびカードのポップアップ演出
  - `ButtonPopupGroup`: 複数ボタンが時間差で展開するメニューグループ
  - `MiniSpeechPopup`: 吹き出し風の小型ポップアップ

実際の動作や組み合わせ方は `Assets/Samples/Scenes/Demo.unity` で確認できます。

---

## 収録コンポーネント一覧

各コンポーネントの詳細なパラメーターやスクリプトからの制御方法は、各ガイドドキュメント（`Assets/Documents/`）を参照してください。

### ボタン系 (`Hisui.UI.Button`)
- **`CustomButton`** (`Hisui/UI/Custom Button`)
  - 標準の `UnityEngine.UI.Button` を拡張したボタン。
  - 指定秒数の連打防止（クールダウン）を搭載。
  - マウスクリック後のフォーカス（Selected状態）自動解除に対応。
  - 外部SEライブラリと疎結合に連携できるサウンド再生に対応。
  - `await button.WaitClickAsync()` によるクリック非同期待機が可能。
  - 詳細: `Assets/Documents/CustomButton_Guide.md`
- **`ButtonMotion`** (`Hisui/UI/Button Motion`)
  - ボタンの状態（Hover / Pressed 等）に応じたスケール・位置演出を制御。
  - クリック時のパンチアニメーションを簡単に付与。
  - `CustomButton` と同一オブジェクトに置くことで、連打防止とモーションが自動連動。
  - 詳細: `Assets/Documents/ButtonMotion_Guide.md`

### トランジション・演出系 (`Hisui.UI.Components`)
- **`DelayGauge`** (`Hisui/UI/Delay Gauge`)
  - 即時変化と遅延追従の2層構造を持つゲージコンポーネント。
  - ダメージ時の遅延減少や、回復時の先行増加演出を表現。
  - 9-Slice スプライトおよびインスペクタでのリアルタイムプレビューに対応。
  - 詳細: `Assets/Documents/DelayGauge_Guide.md`
- **`PanelTransition`** (`Hisui/UI/Panel Transition`)
  - パネルやダイアログの開閉アニメーション。
  - フェード、スケール、上下左右のスライドを自由に合成可能。
  - 開閉時の `CanvasGroup` および当たり判定（Raycast）を自動制御。
  - 詳細: `Assets/Documents/PanelTransition_Guide.md`
- **`EdgeSlideIn`** (`Hisui/UI/Edge Slide In`)
  - 画面外（ルート Canvas 端）からのスライドイン・アウト演出。
  - 画面外の退避座標を自動計算。
  - 複数要素の時間差（スタッガー）登場に対応。
  - 詳細: `Assets/Documents/EdgeSlideIn_Guide.md`
- **`Popup`** (`Hisui/UI/Popup`)
  - 初期位置を基準としたポップアップ出現・退場演出。
  - オフセット移動、スケール、フェードを組み合わせて演出可能。
  - 閉じるボタン連携や、表示後の自動退場タイマーに対応。
  - 詳細: `Assets/Documents/Popup_Guide.md`
- **`PopupGroup`** (`Hisui/UI/Popup Group`)
  - 複数の `Popup` を束ねて管理するコンポーネント。
  - 子要素の時間差（スタッガー）順次出現・一括退場を制御。
  - 各要素が個別退場した場合も追跡し、全要素退場時に自身を非アクティブ化可能。
  - 詳細: `Assets/Documents/Popup_Guide.md`
- **`Typewriter`** (`Hisui/UI/Typewriter`)
  - TextMeshPro 向けの文字送りコンポーネント。
  - 1文字ずつの表示速度、イージング、スキップ（即時全表示）に対応。
  - 中央揃えでも文字送りが揺れないガタつき防止（`Prevent Jitter`）機能を搭載。
  - 1文字表示ごとのコールバックにより、タイプ音SEの再生が容易。
  - 詳細: `Assets/Documents/Typewriter_Guide.md`

### ローディング・画面遷移系 (`Hisui.UI.Loading`)
- **`LoadingScreen`** (`Hisui/UI/Loading Screen`)
  - 画面全体を覆うローディング・画面遷移演出。
  - シーン非同期読み込み（`LoadSceneAsync`）や非同期処理のラップ（`RunAsync`）に対応。
  - 最低表示時間の保証機能やスピナー演出（回転・拡縮ループ）を内包。
  - シーンをまたぐ常駐管理（`LoadingScreen.Instance`）に対応。
  - 詳細: `Assets/Documents/LoadingScreen_Guide.md`

---

## アセンブリ構成

用途に応じた疎結合設計を採用しています。

```
Hisui.UI.Core       ← Hisui.UI.Palette ← Hisui.UI.Components
(LitMotion/UniTask)   (uPalette連携)     (DelayGauge / PanelTransition / EdgeSlideIn / Popup / Typewriter)

Hisui.UI.Button                          Hisui.UI.Loading
(ButtonMotion / CustomButton)            (LoadingScreen)
```

- **`Hisui.UI.Button` / `Hisui.UI.Loading`**:
  - `Core` のみを参照し、uPalette や他のコンポーネントに依存しません。
