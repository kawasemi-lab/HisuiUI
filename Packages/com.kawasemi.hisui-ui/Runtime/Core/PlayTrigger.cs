namespace Hisui.UI.Core
{
    /// <summary>
    /// コンポーネントの自動再生トリガーを表す列挙型。
    /// </summary>
    public enum PlayTrigger
    {
        /// <summary>自動再生しない（スクリプトから明示的に再生する）。</summary>
        None = 0,

        /// <summary><c>OnEnable</c> 時に自動再生する。</summary>
        OnEnable = 1,
    }
}
