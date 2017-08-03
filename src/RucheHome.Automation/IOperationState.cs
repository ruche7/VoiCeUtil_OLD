using System;

namespace RucheHome.Automation
{
    /// <summary>
    /// 操作対象の状態を提供するインタフェース。
    /// </summary>
    public interface IOperationState
    {
        /// <summary>
        /// 操作対象が生存状態であるか否かを取得する。
        /// </summary>
        bool IsAlive { get; }

        /// <summary>
        /// 操作対象が操作可能な状態であるか否かを取得する。
        /// </summary>
        bool CanOperate { get; }
    }
}
