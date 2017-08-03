using System;

namespace RucheHome.Automation
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

        /// <summary>
        /// ValueTuple 型への型分解を行う。
        /// </summary>
        /// <param name="value"><see cref="Value"/> の設定先。</param>
        /// <param name="message"><see cref="Message"/> の設定先。</param>
        public void Deconstruct(out T value, out string message)
        {
            value = this.Value;
            message = this.Message;
        }

        /// <summary>
        /// 戻り値の型からの暗黙の型変換を行う。
        /// </summary>
        /// <param name="source">変換元。</param>
        /// <returns>変換結果。 <see cref="Message"/> は null となる。</returns>
        public static implicit operator Result<T>(T source) => new Result<T>(source);

        /// <summary>
        /// ValueTuple 型からの暗黙の型変換を行う。
        /// </summary>
        /// <param name="source">変換元の ValueTuple 値。</param>
        /// <returns>変換結果。</returns>
        public static implicit operator Result<T>((T value, string message) source) =>
            new Result<T>(source.value, source.message);
    }
}
