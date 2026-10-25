namespace Hisui.UI.Components
{
    /// <summary>
    /// ボタンのクリック音再生を受け取るインターフェース。
    /// <para>
    /// 同じ GameObject または親／子にアタッチしたサウンドコンポーネント（ButtonSound 等）に実装することで、
    /// 連打防止チェックを通過した有効なクリック時に自動的に呼び出されます。
    /// </para>
    /// </summary>
    public interface IButtonSoundHandler
    {
        /// <summary>
        /// 連打防止を通過した有効なクリックが行われたときに呼び出されます。
        /// </summary>
        /// <param name="button">クリックされた <see cref="CustomButton"/></param>
        void OnPlayClickSound(CustomButton button);
    }
}
