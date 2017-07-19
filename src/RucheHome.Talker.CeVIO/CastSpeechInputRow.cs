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
        /// 現在選択中の行を上書きする。複数行選択している場合は最下行を選択する。
        /// </summary>
        Current,

        /// <summary>
        /// セリフの入っていない行を選択する。
        /// </summary>
        Blank,
    }
}
