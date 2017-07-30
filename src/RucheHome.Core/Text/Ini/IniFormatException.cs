using System;

namespace RucheHome.Text.Ini
{
    /// <summary>
    /// INIファイル形式が不正である場合に送出される例外クラス。
    /// </summary>
    public class IniFormatException : FormatException
    {
        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="lineNumber">例外送出の原因となった行番号。</param>
        /// <param name="message">例外メッセージ。</param>
        public IniFormatException(int lineNumber, string message)
            : base(MakeMessage(lineNumber, message))
        {
            this.LineNumber = lineNumber;
        }

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="lineNumber">例外送出の原因となった行番号。</param>
        /// <param name="message">例外メッセージ。</param>
        /// <param name="innerException">この例外を送出する原因となった例外。</param>
        public IniFormatException(
            int lineNumber,
            string message,
            Exception innerException)
            : base(MakeMessage(lineNumber, message), innerException)
        {
            this.LineNumber = lineNumber;
        }

        /// <summary>
        /// 例外送出の原因となった行番号を取得します。
        /// </summary>
        public int LineNumber { get; }

        /// <summary>
        /// 例外メッセージを作成する。
        /// </summary>
        /// <param name="lineNumber">例外送出の原因となった行番号。</param>
        /// <param name="message">コンストラクタに渡された例外メッセージ。</param>
        /// <returns>例外メッセージ。</returns>
        private static string MakeMessage(int lineNumber, string message) =>
            @"Line " + lineNumber + @" : " + (message ?? @"Invalid INI format.");
    }
}
