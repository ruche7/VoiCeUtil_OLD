using System;
using System.Text.RegularExpressions;
using RucheHome.Text.Extensions;

namespace RucheHome.Automation.Talkers.CeVIO
{
    /// <summary>
    /// CeVIO Creative Studio S に入力するための文章整形処理を提供する静的クラス。
    /// </summary>
    public static class TextFormatter
    {
        /// <summary>
        /// 文章の最大許容文字数。
        /// </summary>
        public const int TextLengthLimit = 200;

        /// <summary>
        /// 文章を整形する。
        /// </summary>
        /// <param name="text">基となる文章。</param>
        /// <param name="separatingByLineBreaks">
        /// 改行を半角スペースに置換するならば true 。改行を削除するならば false 。
        /// </param>
        /// <returns>整形した文章。 text が null ならば null 。</returns>
        /// <remarks>
        /// <para>以下の整形処理を行う。</para>
        /// <list type="number">
        /// <item><description>
        /// 最大 <see cref="TextLengthLimit"/> 文字で切り取る。
        /// サロゲートペアを分割しないように考慮される。
        /// </description></item>
        /// <item><description>
        /// 改行を置換する。置換内容は引数 separatingByLineBreaks に依る。
        /// </description></item>
        /// <item><description>
        /// 全角スペースを除く空白文字を半角スペースに置換する。
        /// </description></item>
        /// </list>
        /// <para>このメソッドで整形した文章であっても、入力に成功するとは限らない。</para>
        /// </remarks>
        public static string Format(string text, bool separatingByLineBreaks) =>
            string.IsNullOrEmpty(text) ?
                text :
                RegexWhiteWithoutSpace.Replace(
                    text
                        .SubstringSurrogateSafe(0, TextLengthLimit)
                        .Replace(LineBreaks, separatingByLineBreaks ? OneSpace : OneEmpty),
                    @" ");

        /// <summary>
        /// 改行文字列配列。
        /// </summary>
        private static string[] LineBreaks = { "\r\n", "\r", "\n" };

        /// <summary>
        /// 改行文字の置換先として使われる半角スペース単体配列。
        /// </summary>
        private static string[] OneSpace = { @" " };

        /// <summary>
        /// 改行文字の置換先として使われる空文字列単体配列。
        /// </summary>
        private static string[] OneEmpty = { @"" };

        /// <summary>
        /// 半角スペースと全角スペースを除く空白文字にマッチする正規表現。
        /// </summary>
        private static Regex RegexWhiteWithoutSpace = new Regex(@"[\s-[ 　]]");
    }
}
