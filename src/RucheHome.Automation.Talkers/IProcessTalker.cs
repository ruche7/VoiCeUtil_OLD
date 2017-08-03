using System;

namespace RucheHome.Automation.Talkers
{
    /// <summary>
    /// プロセス操作によって
    /// <see cref="ITalker"/> インタフェース機能を提供するインタフェース。
    /// </summary>
    public interface IProcessTalker : ITalker, IProcessOperation
    {
    }

    /// <summary>
    /// 固定のパラメータID型を持つ IProcessTalker 派生インタフェース。
    /// </summary>
    /// <typeparam name="TParameterId">パラメータID型。</typeparam>
    public interface IProcessTalker<TParameterId> : IProcessTalker, ITalker<TParameterId>
    {
    }
}
