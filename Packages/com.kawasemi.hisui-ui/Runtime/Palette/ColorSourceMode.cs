namespace Hisui.UI.Palette
{
    /// <summary>
    /// 色の指定方式（uPalette のエントリ参照 / 直接 Color 指定）を表す列挙型。
    /// </summary>
    public enum ColorSourceMode
    {
        /// <summary>直接 <see cref="UnityEngine.Color"/> を指定する。</summary>
        Direct = 0,

        /// <summary>uPalette の <see cref="uPalette.Runtime.Core.ColorEntryId"/> でエントリを参照する。</summary>
        Palette = 1,
    }
}
