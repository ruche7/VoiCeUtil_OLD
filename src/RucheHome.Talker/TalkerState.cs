using System;

namespace RucheHome.Talker
{
    /// <summary>
    /// <see cref="ITalker"/> の状態を表す列挙。
    /// </summary>
    public enum TalkerState
    {
        /// <summary>
        /// 状態なし。動作していない。
        /// </summary>
        None,

        /// <summary>
        /// 状態を正しく取得できない状態。
        /// </summary>
        Fail,

        /// <summary>
        /// 動作準備中。
        /// </summary>
        Startup,

        /// <summary>
        /// アイドル状態。
        /// </summary>
        Idle,

        /// <summary>
        /// 文章読み上げ処理中。
        /// </summary>
        Speaking,

        /// <summary>
        /// 処理を受け付けない状態。
        /// </summary>
        /// <remarks>
        /// 音声ファイル保存処理中の場合は <see cref="FileSaving"/> が優先される。
        /// </remarks>
        Blocking,

        /// <summary>
        /// 音声ファイル保存処理中。
        /// </summary>
        FileSaving,
    }
}
