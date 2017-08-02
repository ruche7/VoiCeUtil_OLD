using System;

namespace RucheHome.Automation.Talkers.AITalkEx
{
    /// <summary>
    /// AITalkExベースプロセスの操作によって
    /// <see cref="IProcessTalker{ParameterId}"/>
    /// インタフェース機能を提供するインタフェース。
    /// </summary>
    public interface ITalker : IProcessTalker<ParameterId>, IDisposable
    {
        /// <summary>
        /// 製品種別を取得する。
        /// </summary>
        /// <remarks>
        /// インスタンス生成後に値が変化することはない。
        /// </remarks>
        Product Product { get; }
    }
}
