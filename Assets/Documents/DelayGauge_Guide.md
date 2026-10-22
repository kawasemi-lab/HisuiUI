# DelayGauge 使い方ガイド・リファレンス

`DelayGauge` は、HPバーなどのように「即時側（前面バー）」と「遅延側（背面バー）」の2層構造で値の変化を直感的に見せるUIゲージコンポーネントです。

---

## 1. 主な機能と特徴

- **2層式ゲージ演出**:
  - **ダメージ（減少）時**: 前面メインバーが先行して減少し、背面サブバーが一定時間（`DamageHoldDuration`）待機した後に追従して滑らかに減少。
  - **回復（増加）時**: 背面サブバーが回復色で先行して増加し、前面メインバーが一定時間（`HealHoldDuration`）待機した後に追従して増加。
- **9-Slice 対応（uGUI Slider 方式）**:
  - `Image.fillAmount`（Filled）ではなく、uGUI標準の `Slider` と同様に `RectTransform` のアンカー（`anchorMax.x` 等）を制御するため、Sliced（9-Slice）の画像を使用しても角丸や枠線が歪みません。
- **インスペクタでのリアルタイムプレビュー**:
  - `[ExecuteAlways]` に対応しており、Editモード・Playモード問わず、インスペクタ上の `Value` スライダーを動かすだけで実際の遅延アニメーションがリアルタイムに再生されます。
- **uPalette 自動カラー追従**:
  - 前面バー、ダメージ遅延バー、回復先行バーの色を uPalette のエントリ参照または直接指定で設定可能。テーマ切替時のクロスフェードにも対応。

---

## 2. 推奨GameObject階層構成

uGUI 標準の Slider とほぼ同様の構成でセットアップします。

```text
[GameObject] DelayGauge                ← DelayGauge コンポーネント
  ├─ [Image] Background                ← 背景画像（Sliced 等）
  ├─ [RectTransform] Delay Area        ← 遅延バーの表示領域（余白・パディング定義）
  │    └─ [Image] Delay                ← 遅延・先行バー（Sliced 等）※BackImage に指定
  └─ [RectTransform] Fill Area         ← 前面バーの表示領域
       └─ [Image] Fill                 ← メイン前面バー（Sliced 等）※FrontImage に指定
```

### RectTransform 設定のポイント
- **Delay Area / Fill Area**:
  - アンカーを `(0, 0)` 〜 `(1, 1)`（Stretch-Stretch）にし、枠線の内側に収まるよう `Left / Top / Right / Bottom` のオフセットを調整します。
- **Delay / Fill (子オブジェクト)**:
  - `DelayGauge` が起動時にアンカー（`anchorMin` / `anchorMax`）を自動制御します。
  - `AnchoredPosition = (0, 0)`, `SizeDelta = (0, 0)`（オフセットなし）にしておきます。

---

## 3. インスペクタ設定項目

| 項目 | 型 | 説明 |
|---|---|---|
| **Value** | `float (0〜1)` | 現在の正規化値。インスペクタ上で動かすと遅延アニメーションが再生されます。シーン開始時の初期値としても機能します。 |
| **Direction** | `enum` | ゲージの伸縮方向（`LeftToRight`, `RightToLeft`, `BottomToTop`, `TopToBottom`）。 |
| **Damage Front Motion** | `MotionSpec` | 減少時の前面（即時側）モーション（Duration, Ease, Delay）。 |
| **Damage Hold Duration** | `float` | 減少時に背面バーが追従を開始するまでの待機時間（秒）。 |
| **Damage Back Motion** | `MotionSpec` | 減少時の背面（遅延側）追従モーション。 |
| **Heal Lead Motion** | `MotionSpec` | 回復時の背面（先行側）モーション。 |
| **Heal Hold Duration** | `float` | 回復時に前面バーが増加を開始するまでの待機時間（秒）。 |
| **Heal Follow Motion** | `MotionSpec` | 回復時の前面（追従側）追従モーション。 |
| **Main Color** | `HisuiColor` | メインバー（前面）の色（Direct / uPalette）。 |
| **Damage Delay Color** | `HisuiColor` | ダメージ時の遅延バー（背面）の色。 |
| **Heal Delay Color** | `HisuiColor` | 回復時の先行バー（背面）の色。 |
| **Theme Crossfade Duration**| `float` | uPalette テーマ切替時の色クロスフェード秒数。 |
| **Front Image** | `Image` | 前面バーの Image。 |
| **Back Image** | `Image` | 背面バーの Image。 |

---

## 4. スクリプトからの使用例

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;
using Hisui.UI.Components;
using Hisui.UI.Core;
using Hisui.UI.Palette;
using LitMotion;
using UnityEngine;

public class DelayGaugeExample : MonoBehaviour
{
    [SerializeField] private DelayGauge _gauge;

    private async void Start()
    {
        // 1. コードからの動的設定（Fluent API / メソッドチェーン）
        _gauge.SetDirection(DelayGauge.Direction.LeftToRight)
              .SetDamageDelay(holdSeconds: 0.3f, duration: 0.5f, ease: Ease.OutCubic)
              .SetHealDelay(holdSeconds: 0.2f, duration: 0.4f, ease: Ease.OutQuad)
              .SetIgnoreTimeScale(true); // ポーズ中（Time.timeScale = 0）でも動かす

        // 2. 初期値を即座に設定（アニメーションなし）
        _gauge.SetValue(1.0f, immediate: true);

        await UniTask.Delay(1000);

        // 3. ダメージ演出（0.6 へ減少）
        // 前面が即座に減り、0.3秒ホールド後に背面が追従して減少する
        _gauge.SetValue(0.6f);

        await UniTask.Delay(1500);

        // 4. 回復演出（非同期 await で完了待機）
        // 背面が緑色で先行して 0.9 まで伸び、0.2秒後にメインバーが追従する
        CancellationToken ct = this.GetCancellationTokenOnDestroy();
        await _gauge.SetValueAsync(0.9f, ct);

        Debug.Log("ゲージのアニメーションが完了しました！");
    }

    // 値変更イベントの購読例
    private void OnEnable()
    {
        _gauge.OnValueChanged += HandleValueChanged;
    }

    private void OnDisable()
    {
        _gauge.OnValueChanged -= HandleValueChanged;
    }

    private void HandleValueChanged(float normalizedValue)
    {
        Debug.Log($"HP変更: {normalizedValue:P0}");
    }
}
```
