using System;

namespace RucheHome.Talker
{
    /// <summary>
    /// メソッドの戻り値と付随メッセージを保持する構造体。
    /// </summary>
    /// <typeparam name="T">戻り値の型。</typeparam>
    public struct Result<T>
    {
        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="value">メソッドの戻り値。既定では default(T) 。</param>
        /// <param name="message">付随メッセージ。既定では null 。</param>
        public Result(T value = default(T), string message = null) : this()
        {
            this.Value = value;
            this.Message = message;
        }

        /// <summary>
        /// メソッドの戻り値を取得する。
        /// </summary>
        public T Value { get; }

        /// <summary>
        /// 付随メッセージを取得する。
        /// </summary>
        public string Message { get; }
    }
}
