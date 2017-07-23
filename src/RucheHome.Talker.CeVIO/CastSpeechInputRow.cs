using System;

namespace RucheHome.Talker.CeVIO
{
    /// <summary>
    /// キャラクターや文章を
    /// CeVIO Creative Studio S のセリフグリッドに入力する際の入力対象行設定を表す列挙。
    /// </summary>
    public enum CastSpeechInputRow
    {
        /// <summary>
        /// セリフの入っていない行を選択する。
        /// </summary>
        Blank,

        /// <summary>
        /// 現在選択中の行を上書きする。
        /// </summary>
        Current,
    }
}
