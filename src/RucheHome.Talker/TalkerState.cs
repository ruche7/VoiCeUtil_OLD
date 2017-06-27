using System;

namespace RucheHome.Talker
{
    /// <summary>
    /// 文章読み上げソフトの状態を表す列挙。
    /// </summary>
    public enum TalkerState
    {
        /// <summary>
        /// 状態なし。起動していない。
        /// </summary>
        None,

        /// <summary>
        /// 状態を正しく取得できない状態。
        /// </summary>
        Fail,

        /// <summary>
        /// 起動途中。
        /// </summary>
        Startup,

        /// <summary>
        /// アイドル状態。
        /// </summary>
        Idle,

        /// <summary>
        /// 文章読み上げ中。
        /// </summary>
        Speaking,

        /// <summary>
        /// モーダルダイアログ表示等により処理を受け付けない状態。
        /// </summary>
        /// <remarks>
        /// 音声ファイル保存処理中の場合は <see cref="Saving"/> が優先される。
        /// </remarks>
        Blocking,

        /// <summary>
        /// 音声ファイル保存処理中。
        /// </summary>
        FileSaving,
    }
}
