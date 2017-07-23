using System;

namespace RucheHome.Talker.CeVIO.Internal.Controls
{
    /// <summary>
    /// コントロールパネル種別列挙。
    /// </summary>
    internal enum ControlPanelKind
    {
        /// <summary>
        /// 未選択状態。
        /// </summary>
        None,

        /// <summary>
        /// トーク用。
        /// </summary>
        Talk,

        /// <summary>
        /// ソング用。
        /// </summary>
        Song,
    }
}
