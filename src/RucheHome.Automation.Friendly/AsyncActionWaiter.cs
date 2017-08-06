using System;
using Codeer.Friendly;
using RucheHome.Diagnostics;

namespace RucheHome.Automation.Friendly
{
    /// <summary>
    /// 非同期アクションの完了を待機する機能を提供するクラス。
    /// </summary>
    public static class AsyncActionWaiter
    {
        /// <summary>
        /// 非同期アクションを開始し、その完了を待機する。
        /// </summary>
        /// <param name="action">非同期アクション。</param>
        /// <param name="timeoutMilliseconds">
        /// タイムアウトミリ秒数。負数ならば無制限。
        /// </param>
        /// <returns>待機完了したならば true 。タイムアウトしたならば false 。</returns>
        /// <remarks>
        /// 非同期アクション内で例外が送出された場合はそれをそのまま呼び出し元へ送出する。
        /// </remarks>
        public static bool Wait(Action<Async> action, int timeoutMilliseconds = -1)
        {
            ArgumentValidation.IsNotNull(action, nameof(action));

            var async = new Async();

            action(async);

            if (!Waiter.Until(() => async.IsCompleted, timeoutMilliseconds))
            {
                return false;
            }

            if (async.ExecutingException != null)
            {
                throw async.ExecutingException;
            }

            return async.IsCompleted;
        }
    }
}
