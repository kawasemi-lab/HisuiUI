# CustomButton ガイド

## 概要
`CustomButton` は、Unity 標準の `UnityEngine.UI.Button` を拡張し、**連打防止（クールダウン）**・**マウスクリック時の選択（フォーカス）自動解除**・**サウンドコンポーネント連携** を提供する軽量コンポーネントです。

演出（スケール変化や位置オフセット、パンチアニメーション等）は `ButtonMotion` が担当するため、本コンポーネントは見た目のモーション処理を行わず、ボタンのロジック拡張に専念します。

---

## 主な機能

### 1. 連打防止（Cooldown）
- 指定秒数（`CooldownTime`）以内の連続クリック・二重送信を自動で無視します。
- `Time.unscaledTime` を用いて判定しているため、ポーズ中（`Time.timeScale = 0`）でも正常に動作します。
- 連打防止で弾かれたクリックは、`onClick` やサウンド再生、`ButtonMotion` のパンチ演出も発火しません。

### 2. 押下後の自動無効化（Interactable 制御）
- 決定ボタンや送信ボタンなど、1度押したら二重実行を防ぐために `interactable = false` にしたい場合に使用します。
- **復帰契機（RestoreMode）**：
  - `OnEnable`: ダイアログの再オープン時など、GameObject が再アクティブ化されたタイミングで自動的に `interactable = true` に復帰します。
  - `Manual`: スクリプトから明示的に `button.RestoreInteractable()` や `button.interactable = true` を呼ぶまで無効状態を維持します。

### 3. マウスクリック時の選択（フォーカス）自動解除
- PC/Web 等でボタンをマウスクリックした際、Unity 標準の挙動ではボタンが `Selected` 状態（フォーカス中）のまま残り、フォーカス枠やハイライト色が外れない問題があります。
- `DeselectOnPointerClick` を有効にすると、ポインタクリック完了時に自動で `EventSystem.current.SetSelectedGameObject(null)` を呼び出し、Normal 状態へ戻します。
- キーボードやゲームパッドによる決定（`OnSubmit`）時にはフォーカス解除を行わないため、コントローラー操作のフォーカス移動を損なうことはありません。

### 4. サウンド連携（外部サウンドライブラリへの柔軟な対応）
HisuiUI は特定のサウンドライブラリに依存しません。各自お使いのサウンドライブラリ（CriWare、FMOD、独自の AudioManager、AudioSource 等）に合わせたコンポーネントを自由に連携できます。

---

## サウンドコンポーネントの実装例

同じ GameObject に `IButtonSoundHandler` を実装したコンポーネントをアタッチするだけで、`CustomButton` は有効なクリックが行われたタイミングで自動的にそれを検知して呼び出します。

### 実装例（`IButtonSoundHandler` を使用）
```csharp
using UnityEngine;
using Hisui.UI.Components;

[RequireComponent(typeof(CustomButton))]
public class ButtonSound : MonoBehaviour, IButtonSoundHandler
{
    [SerializeField] private AudioClip _defaultClip;
    [SerializeField] private AudioSource _audioSource;

    public void OnPlayClickSound(CustomButton button)
    {
        // ボタン側の SoundKey を見て再生する SE を分岐することも可能
        if (button.SoundKey == "Cancel")
        {
            // キャンセル音再生
            return;
        }

        // デフォルトクリック音の再生
        if (_audioSource != null && _defaultClip != null)
        {
            _audioSource.PlayOneShot(_defaultClip);
        }
    }
}
```

### その他の連携方法
- **`OnValidClick` イベント**:
  連打防止を通過した正規クリック時に発火する C# イベントです。
  ```csharp
  customButton.OnValidClick += (btn) => AudioManager.PlaySE("Click");
  ```
- **標準の `onClick`**:
  `CustomButton` は連打防止チェックを通過した場合にのみ `base.OnPointerClick` / `base.OnSubmit`（`onClick.Invoke`）を呼ぶため、通常の `customButton.onClick.AddListener(...)` でも連打防止されたタイミングでのみ実行されます。
- **派生クラスによる拡張**:
  `public virtual void PlayClickSound()` をオーバーライドして、プロジェクト共通のサウンドマネージャーを直接呼ぶ派生ボタンを作成することも可能です。

---

## ButtonMotion との組み合わせ

同じ GameObject に `CustomButton` と `ButtonMotion` を両方アタッチするだけで、自動的に連動します。
- `ButtonMotion` は同じ GameObject の `CustomButton`（`Selectable` / `Button`）を自動検知します。
- `ButtonMotion` のクリック演出（パンチ）は `button.onClick` に連動しているため、`CustomButton` の連打防止で弾かれたクリックではパンチ演出も走りません。

---

## 非同期待機（WaitClickAsync）

ボタンが有効クリックされるまで待機できます。HisuiUI の共通設計原則に則り、`CancellationToken` による中断時や GameObject 破棄時にも例外（`OperationCanceledException`）をスローせず静かにリターンします。

```csharp
// ダイアログの決定待ちなど
await okButton.WaitClickAsync(destroyCancellationToken);
```

---

## スクリプティング API

```csharp
var btn = GetComponent<CustomButton>();

// プロパティ
btn.UseCooldown = true;
btn.CooldownTime = 0.25f;
btn.DisableOnClick = true;
btn.RestoreMode = InteractableRestoreMode.OnEnable; // または Manual
btn.DeselectOnPointerClick = true;
btn.SoundKey = "Decide";

// 状態確認
bool inCd = btn.IsInCooldown;
float remain = btn.RemainingCooldownTime;

// メソッド
btn.ResetCooldown();       // タイマーを即時リセット
btn.RestoreInteractable(); // interactable = true に復帰

// Fluent API
btn.SetCooldown(true, 0.2f)
   .SetDisableOnClick(true, InteractableRestoreMode.OnEnable)
   .SetDeselectOnPointerClick(true)
   .SetSoundKey("Sub");
```
