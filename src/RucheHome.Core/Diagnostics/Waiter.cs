using System;
using System.Diagnostics;
using System.Threading;

namespace RucheHome.Diagnostics
{
    /// <summary>
    /// デリゲートの戻り値が特定の状態になるまで待機する機能を提供する静的クラス。
    /// </summary>
    public static class Waiter
    {
        /// <summary>
        /// デリゲートの戻り値が条件を満たしている間待機する。
        /// </summary>
        /// <typeparam name="T">戻り値の型。</typeparam>
        /// <param name="func">処理対象デリゲート。</param>
        /// <param name="predicator">戻り値の条件判定を行うデリゲート。</param>
        /// <param name="timeoutMilliseconds">
        /// タイムアウトミリ秒数。負数ならば無制限。
        /// </param>
        /// <returns>
        /// 戻り値が条件を満たさなくなったならばその時の値。
        /// タイムアウトしたならばタイムアウト直前の戻り値。
        /// </returns>
        public static T For<T>(
            Func<T> func,
            Func<T, bool> predicator,
            int timeoutMilliseconds = -1)
        {
            ArgumentValidation.IsNotNull(func, nameof(func));
            ArgumentValidation.IsNotNull(predicator, nameof(predicator));

            var result = func();

            for (
                var sw = Stopwatch.StartNew();
                predicator(result) &&
                (timeoutMilliseconds < 0 || sw.ElapsedMilliseconds < timeoutMilliseconds);)
            {
                Thread.Yield();
                result = func();
            }

            return result;
        }

        /// <summary>
        /// デリゲートの戻り値が true である間待機する。
        /// </summary>
        /// <param name="func">処理対象デリゲート。</param>
        /// <param name="timeoutMilliseconds">
        /// タイムアウトミリ秒数。負数ならば無制限。
        /// </param>
        /// <returns>待機完了したならば true 。タイムアウトしたならば false 。</returns>
        public static bool For(Func<bool> func, int timeoutMilliseconds = -1) =>
            !For(func, r => r, timeoutMilliseconds);

        /// <summary>
        /// デリゲートの戻り値が条件を満たすまでの間待機する。
        /// </summary>
        /// <typeparam name="T">戻り値の型。</typeparam>
        /// <param name="func">処理対象デリゲート。</param>
        /// <param name="predicator">戻り値の条件判定を行うデリゲート。</param>
        /// <param name="timeoutMilliseconds">
        /// タイムアウトミリ秒数。負数ならば無制限。
        /// </param>
        /// <returns>
        /// 戻り値が条件を満たしたならばその時の値。
        /// タイムアウトしたならばタイムアウト直前の戻り値。
        /// </returns>
        public static T Until<T>(
            Func<T> func,
            Func<T, bool> predicator,
            int timeoutMilliseconds = -1)
            =>
            For(
                func,
                (predicator == null) ? (Func<T, bool>)null : (r => !predicator(r)),
                timeoutMilliseconds);

        /// <summary>
        /// デリゲートの戻り値が false である間待機する。
        /// </summary>
        /// <param name="func">処理対象デリゲート。</param>
        /// <param name="timeoutMilliseconds">
        /// タイムアウトミリ秒数。負数ならば無制限。
        /// </param>
        /// <returns>待機完了したならば true 。タイムアウトしたならば false 。</returns>
        public static bool Until(Func<bool> func, int timeoutMilliseconds = -1) =>
            Until(func, r => r, timeoutMilliseconds);
    }
}
