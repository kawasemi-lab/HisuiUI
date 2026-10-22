namespace Hisui.UI.Core
{
    /// <summary>
    /// 画面端からの登場方向（<see cref="ScreenEdgeUtility"/> の入力）。
    /// 「どの端から現れるか」を表す。例えば <see cref="Left"/> は画面の左端の外側から入ってくる。
    /// </summary>
    public enum EdgeDirection
    {
        /// <summary>画面左端の外から登場する。</summary>
        Left = 0,

        /// <summary>画面右端の外から登場する。</summary>
        Right = 1,

        /// <summary>画面上端の外から登場する。</summary>
        Top = 2,

        /// <summary>画面下端の外から登場する。</summary>
        Bottom = 3,

        /// <summary>任意の方向ベクトルで指定する（斜めからの登場など）。</summary>
        Custom = 4,
    }
}
