using System;
using UnityEngine;
using uPalette.Runtime.Core;
using uPalette.Runtime.Foundation.TinyRx.ObservableProperty;

namespace Hisui.UI.Palette
{
    /// <summary>
    /// 「uPalette のエントリ参照」と「直接 <see cref="Color"/> 指定」を切り替えられるシリアライズ可能な色設定構造体。
    /// Inspector とスクリプト（<see cref="FromColor"/> / <see cref="FromEntry"/>）の双方から設定できる。
    /// </summary>
    [Serializable]
    public struct HisuiColor : IEquatable<HisuiColor>
    {
        /// <summary>色の取得モード（Direct / Palette）。</summary>
        [Tooltip("色の指定方式（Direct: 直接指定 / Palette: uPalette エントリ参照）")]
        public ColorSourceMode mode;

        /// <summary>直接指定モード時の色、またはパレット未検出時のフォールバック色。</summary>
        [Tooltip("直接指定する色（Palette モードでエントリが見つからない場合のフォールバックにも使用）")]
        public Color directColor;

        /// <summary>uPalette のカラーエントリ ID。Inspector では uPalette 標準のドロップダウンで選択可能。</summary>
        [Tooltip("uPalette の Color エントリ参照")]
        [SerializeField]
        private ColorEntryId _entryId;

        /// <summary>
        /// uPalette のエントリ ID 文字列を取得・設定する。
        /// </summary>
        public string EntryId
        {
            get => _entryId != null ? _entryId.Value : string.Empty;
            set
            {
                if (_entryId == null)
                {
                    _entryId = new ColorEntryId();
                }
                _entryId.Value = value;
            }
        }

        /// <summary>
        /// 直接 <see cref="Color"/> を指定して <see cref="HisuiColor"/> を作成する。
        /// </summary>
        /// <param name="color">指定する色。</param>
        public static HisuiColor FromColor(Color color)
        {
            return new HisuiColor
            {
                mode = ColorSourceMode.Direct,
                directColor = color,
                _entryId = new ColorEntryId { Value = string.Empty },
            };
        }

        /// <summary>
        /// uPalette のエントリ ID を指定して <see cref="HisuiColor"/> を作成する。
        /// </summary>
        /// <param name="entryId">uPalette のカラーエントリ ID。</param>
        /// <param name="fallbackColor">エントリが見つからない場合のフォールバック色（省略時は白）。</param>
        public static HisuiColor FromEntry(string entryId, Color? fallbackColor = null)
        {
            return new HisuiColor
            {
                mode = ColorSourceMode.Palette,
                directColor = fallbackColor ?? Color.white,
                _entryId = new ColorEntryId { Value = entryId ?? string.Empty },
            };
        }

        /// <summary>
        /// 現在の設定（Direct またはアクティブな uPalette テーマ）から実際の <see cref="Color"/> を解決して返す。
        /// </summary>
        public Color Resolve()
        {
            if (mode == ColorSourceMode.Palette && TryGetObservableProperty(out var prop))
            {
                return prop.Value;
            }
            return directColor;
        }

        /// <summary>
        /// Palette モード時に uPalette の <see cref="IReadOnlyObservableProperty{Color}"/> の取得を試みる。
        /// </summary>
        internal bool TryGetObservableProperty(out IReadOnlyObservableProperty<Color> property)
        {
            property = null;
            if (mode != ColorSourceMode.Palette)
            {
                return false;
            }

            string id = EntryId;
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }

            var store = PaletteStore.Instance;
            if (store == null || store.ColorPalette == null)
            {
                return false;
            }

            return store.ColorPalette.TryGetActiveValue(id, out property);
        }

        /// <summary>
        /// <see cref="Color"/> からの暗黙変換（Direct モード）。
        /// </summary>
        public static implicit operator HisuiColor(Color color) => FromColor(color);

        /// <inheritdoc />
        public bool Equals(HisuiColor other)
        {
            return mode == other.mode
                && directColor.Equals(other.directColor)
                && string.Equals(EntryId, other.EntryId, StringComparison.Ordinal);
        }

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is HisuiColor other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine((int)mode, directColor, EntryId);
    }
}
