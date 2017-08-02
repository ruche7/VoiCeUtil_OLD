using System;

namespace RucheHome.Automation.Talkers.CeVIO
{
    /// <summary>
    /// CeVIO Creative Studio S プロセスの操作によって
    /// <see cref="IProcessTalker{ParameterId}"/>
    /// インタフェース機能を提供するインタフェース。
    /// </summary>
    public interface ITalker
        : IProcessTalker<ParameterId>, ICreativeStudioOperation, IDisposable
    {
    }
}
