using System;

namespace RucheHome.Automation.Ymm
{
    /// <summary>
    /// ゆっくりMovieMakerの状態を表す列挙。
    /// </summary>
    public enum YmmState
    {
        /// <summary>
        /// 状態なし。動作していない。
        /// </summary>
        None,

        /// <summary>
        /// 起動途中。
        /// </summary>
        Startup,

        /// <summary>
        /// 終了処理中。
        /// </summary>
        Cleanup,

        /// <summary>
        /// アイドル状態。
        /// </summary>
        Idle,

        /// <summary>
        /// タイムラインウィンドウが隠れている状態。
        /// </summary>
        TimelineHidden,

        /// <summary>
        /// 処理を受け付けない状態。
        /// </summary>
        Blocking,
    }
}
