using System;

namespace RucheHome.Automation.Talkers.Voiceroid2
{
    /// <summary>
    /// VOICEROID2プロセスの操作によって
    /// <see cref="IProcessTalker{ParameterId}"/>
    /// インタフェース機能を提供するインタフェース。
    /// </summary>
    public interface ITalker : IProcessTalker<ParameterId>, IDisposable
    {
    }
}
