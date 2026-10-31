# Typewriter 使い方ガイド・リファレンス

`Typewriter` は、TextMeshPro（`TMP_Text` / `TextMeshProUGUI`）を対象とした軽量・ゼロアロケーション志向の文字送り（タイプライター）演出コンポーネントです。

---

## 1. 主な機能と特徴

- **TextMeshPro 専用設計**:
  - `TMP_Text` がアタッチされた GameObject に直接アタッチして使用します（`[RequireComponent(typeof(TMP_Text))]`）。
  - リッチテキストタグ（`<color=...>` 等）に対応。
- **初期全文字表示と再生時空欄スタート**:
  - シーン読み込み時やエディタ配置時はテキストが消えないよう全ての文字が表示された状態を維持します。
  - 再生開始（Play）が実行されたタイミングで自動的に空欄になり、そこから設定スピード・イージングに応じて1文字ずつテキスト送りで表示されます。
- **柔軟なスピード＆イージング設定**:
  - **スピード**: 毎秒何文字表示するか（`CharactersPerSecond`）で指定。
  - **イージング**: デフォルトは `Linear`（等速）。`OutQuad` や `InCubic`、`CustomAnimationCurve` 等のイージングも自在に設定可能。
- **行・列の中央揃えガタつき防止オプション（`PreventJitter`）**:
  - 水平中央揃え（`Center`）や垂直中央揃え（`Middle`）に設定したテキストで、文字を 1 文字ずつ文字列連結（`Substring`）して追加していくと、文字が増えるたびに全体の幅・高さが変わり、既存の文字が毎文字左右・上下にガタガタと揺れてしまいます。
  - 本コンポーネントの `PreventJitter` オプション（デフォルト ON）を有効にすると、あらかじめ全文のレイアウトとメッシュ情報を確定（`ForceMeshUpdate`）させた上で、`TMP_Text.maxVisibleCharacters` を用いて可視文字数を 0 から増やしていくため、中央揃えであっても表示が一切ガタつかず、完成形の座標位置のまま美しく文字送りが行われます。
- **動的なテキスト流し込み**:
  - `Play(text)` や `PlayAsync(text)`、`SetText(text)` により、スクリプトから動的に文字列を渡して再生可能。
- **スキップ（一瞬で全文字表示）機能**:
  - ノベルゲームや会話UIで必須となる「クリックで全文字即時表示（スキップ）」を行える `Complete()` / `Skip()` を標準搭載。
- **サウンド（SE）連携イベント**:
  - 1文字新しく表示されるたびに `OnCharacterRevealed(int visibleIndex)` イベントが発火するため、タイプライター音・ピコピコ音（SE）の再生を簡単に組み込めます。
- **インスペクタでのプレビュー機能**:
  - `[ExecuteAlways]` に対応しており、Editモード・Playモード問わずインスペクタ上の操作パネル（[Play] [Stop] [Skip] [Clear]）からリアルタイムに文字送りのプレビュー確認が可能です。

---

## 2. セットアップ方法

1. Canvas 下に `TextMeshPro - Text`（または `TextMeshProUGUI`）を作成します。
2. その GameObject に `Typewriter` コンポーネントを追加します。
   - メニュー: `Component` > `Hisui` > `UI` > `Typewriter`
3. テキストの配置（Alignment）を「水平中央（Center）」「垂直中央（Middle）」にしても、`Prevent Jitter` が有効であれば文字送り中に一切ガタつきません。

---

## 3. インスペクタ設定項目

| 項目 | 型 | デフォルト | 説明 |
|---|---|---|---|
| **Default Text** | `string (TextArea)` | `""` | 事前設定するテキスト。空の場合は `TMP_Text` に現在入力されているテキストが使用されます。 |
| **Characters Per Second** | `float` | `30.0` | テキスト表示スピード（毎秒何文字表示するか）。 |
| **Start Delay** | `float` | `0.0` | 再生開始までの待機時間（秒）。 |
| **Ease** | `Ease` | `Linear` | テキスト送りのイージング種別。 |
| **Custom Curve** | `AnimationCurve` | `null` | `Ease` に `CustomAnimationCurve` を指定した場合に使用するカスタムカーブ。 |
| **Prevent Jitter** | `bool` | `true` | **中央揃え時のガタつき防止**。ONの場合、`maxVisibleCharacters` を用いて全文レイアウトを保持したまま文字を表示します。OFFにすると文字を順次追加する従来の挙動になります。 |
| **Play Trigger** | `PlayTrigger` | `None` | 自動再生のトリガー（`None` または `OnEnable`）。 |
| **Ignore Time Scale** | `bool` | `true` | ポーズ中（`Time.timeScale = 0`）でも文字送りを進行させるか。 |

---

## 4. スクリプトからの使用例

### 4.1 基本的な再生と動的テキスト流し込み

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;
using Hisui.UI.Components;
using LitMotion;
using UnityEngine;

public class TypewriterExample : MonoBehaviour
{
    [SerializeField] private Typewriter _typewriter;

    private async void Start()
    {
        // 1. Fluent API による設定変更
        _typewriter.SetSpeed(40f)                 // 毎秒40文字
                   .SetEase(Ease.Linear)          // 等速
                   .SetPreventJitter(true)        // 中央揃えガタつき防止
                   .SetIgnoreTimeScale(true);

        // 2. 既存のテキストで文字送り再生
        _typewriter.Play();

        // 完了待機
        await UniTask.WaitUntil(() => !_typewriter.IsPlaying);

        await UniTask.Delay(1000);

        // 3. 新しいテキストを動的に流し込んで非同期再生（PlayAsync）
        CancellationToken ct = this.GetCancellationTokenOnDestroy();
        await _typewriter.PlayAsync("こんにちは！HisuiUIの文字送りコンポーネントです。", ct);
    }
}
```

### 4.2 クリックでスキップ（会話UI・ノベルゲーム）

```csharp
using Cysharp.Threading.Tasks;
using Hisui.UI.Components;
using UnityEngine;

public class DialogueController : MonoBehaviour
{
    [SerializeField] private Typewriter _dialogueText;

    private readonly string[] _lines = new[]
    {
        "かつて、この地には翡翠色の美しい湖がありました。",
        "その湖の底には、古い文明の秘密が眠っていると言われています。",
        "準備はいいですか？冒険を始めましょう！"
    };

    private int _lineIndex = 0;

    private void Start()
    {
        ShowCurrentLine();
    }

    private void Update()
    {
        // 画面クリックまたはSpaceキー入力時
        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space))
        {
            if (_dialogueText.IsPlaying)
            {
                // 文字送り中なら即座にスキップ（全文字表示）
                _dialogueText.Skip(); // または Complete()
            }
            else
            {
                // 文字送りが終わっていれば次のセリフへ
                _lineIndex++;
                if (_lineIndex < _lines.Length)
                {
                    ShowCurrentLine();
                }
            }
        }
    }

    private void ShowCurrentLine()
    {
        _dialogueText.Play(_lines[_lineIndex]);
    }
}
```

### 4.3 タイプライター音（SE）の再生

```csharp
using Hisui.UI.Components;
using UnityEngine;

public class TypewriterAudioSync : MonoBehaviour
{
    [SerializeField] private Typewriter _typewriter;
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _typeSound;

    private void OnEnable()
    {
        _typewriter.OnCharacterRevealed += PlayCharacterSound;
    }

    private void OnDisable()
    {
        _typewriter.OnCharacterRevealed -= PlayCharacterSound;
    }

    private void PlayCharacterSound(int characterIndex)
    {
        // 空白文字などでSEを鳴らしたくない場合は適宜フィルタリング可能
        if (_audioSource != null && _typeSound != null)
        {
            _audioSource.PlayOneShot(_typeSound, 0.5f);
        }
    }
}
```

---

## 5. 設計思想：中央揃えでガタつく理由と `maxVisibleCharacters` による解決

### 従来の文字追加方式（Substring / ガタつきの原因）
TextMeshPro で配置を「中央揃え（Horizontal / Vertical Alignment）」に設定している場合、文字列を1文字ずつ `text += c` や `text.Substring(0, count)` で追加していくと、次のような現象が発生します：

1. 文字が追加されるたびに、テキスト全体の矩形横幅（PreferredWidth）が増加する。
2. 水平中央揃えは「横幅の中央」を原点に左右均等に文字を並べるため、横幅が広がるたびに**すでに出現している文字の座標が左へシフトする**。
3. これが毎文字繰り返されることで、文字送り中に文章全体が左右に激しく振動（ガタガタと揺れる）する。

### HisuiUI Typewriter の解決策（`maxVisibleCharacters` 方式）
`Typewriter` では、`PreventJitter`（デフォルト ON）によって以下のアルゴリズムで描画を行います：

1. 再生開始時に全文を `TMP_Text.text` に代入し、即座に `TMP_Text.ForceMeshUpdate()` を呼び出します。
2. これにより、TextMeshPro のレイアウトエンジンが全文分の最終矩形幅・折り返し位置・中央揃え座標・メッシュ情報をあらかじめ完全に確定させます。
3. その上で `TMP_Text.maxVisibleCharacters = 0` からスタートし、LitMotion のイージング計算に従って可視文字数のみを順次増やしていきます。
4. 各文字はあらかじめ確定している完成形の座標位置に出現するため、**既存の文字が 1 ピクセルも動かず、中央揃えでも完全に静止した安定した文字送り**が実現されます。
