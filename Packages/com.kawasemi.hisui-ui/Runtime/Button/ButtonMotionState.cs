namespace Hisui.UI.Components
{
    /// <summary>
    /// <see cref="ButtonMotion"/> が扱うボタンの状態。優先順位は Disabled &gt; Pressed &gt; Hover &gt; Selected &gt; Normal。
    /// </summary>
    public enum ButtonMotionState
    {
        /// <summary>基準状態。スケール・位置は Awake 時の値。</summary>
        Normal = 0,

        /// <summary>ポインタが要素上にある。</summary>
        Hover = 1,

        /// <summary>押下中、かつポインタが要素上にある。</summary>
        Pressed = 2,

        /// <summary><c>Selectable.IsInteractable()</c> が false。</summary>
        Disabled = 3,

        /// <summary>EventSystem の選択対象（キーボード・ゲームパッドのフォーカス）。</summary>
        Selected = 4,
    }
}
