using System;

namespace RucheHome.Tests.Automation.Talkers
{
    /// <summary>
    /// <see cref="ITalker"/> 実装クラス種別列挙。
    /// </summary>
    public enum TalkerKind
    {
        /// <summary>
        /// VOICEROID+ EX シリーズ、TalkExシリーズ等
        /// </summary>
        AITalkEx,

        /// <summary>
        /// VOICEROID2
        /// </summary>
        Voiceroid2,

        /// <summary>
        /// CeVIO
        /// </summary>
        CeVIO,
    }
}
