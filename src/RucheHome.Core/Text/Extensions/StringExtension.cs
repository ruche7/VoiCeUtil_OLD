using System;
using System.Linq;
using RucheHome.Diagnostics;

namespace RucheHome.Text.Extensions
{
    /// <summary>
    /// String クラスおよび StringBuilder クラスに対する拡張メソッドを提供する静的クラス。
    /// </summary>
    public static partial class StringExtension
    {
        /// <summary>
        /// 先頭と末尾の改行文字を取り除いた文字列を作成する。
        /// </summary>
        /// <param name="source">対象文字列。</param>
        /// <returns>作成した文字列。</returns>
        public static string TrimLineBreaks(this string source) =>
            source?.Trim('\r', '\n') ?? throw new ArgumentNullException(nameof(source));

        /// <summary>
        /// 先頭の改行文字を取り除いた文字列を作成する。
        /// </summary>
        /// <param name="source">対象文字列。</param>
        /// <returns>作成した文字列。</returns>
        public static string TrimStartLineBreaks(this string source) =>
            source?.TrimStart('\r', '\n') ?? throw new ArgumentNullException(nameof(source));

        /// <summary>
        /// 末尾の改行文字を取り除いた文字列を作成する。
        /// </summary>
        /// <param name="source">対象文字列。</param>
        /// <returns>作成した文字列。</returns>
        public static string TrimEndLineBreaks(this string source) =>
            source?.TrimEnd('\r', '\n') ?? throw new ArgumentNullException(nameof(source));

        /// <summary>
        /// 行頭のインデントを最小幅分だけ除去した文字列を作成する。
        /// </summary>
        /// <param name="source">対象文字列。</param>
        /// <param name="tabWidth">タブ文字幅。 1 以上でなければならない。</param>
        /// <param name="newLine">改行文字列。通常は既定値 "\n" のままで問題ない。</param>
        /// <returns>作成した文字列。</returns>
        public static string StripIndent(
            this string source,
            int tabWidth = 4,
            string newLine = "\n")
        {
            ArgumentValidation.IsNotNull(source, nameof(source));
            ArgumentValidation.IsEqualsOrGreaterThan(tabWidth, 1, nameof(tabWidth));
            ArgumentValidation.IsNotNull(newLine, nameof(newLine));

            // 改行で分割
            var lines = source.Split(new[] { newLine }, StringSplitOptions.None);
            if (lines.Length <= 0)
            {
                return source;
            }

            // 最小インデント幅決定
            var lineIndents =
                lines
                    .Select(
                        line =>
                            line
                                .TakeWhile(c => c == ' ' || c == '\t')
                                .Sum(c => (c == ' ') ? 1 : tabWidth))
                    .Where(indent => indent > 0);
            if (!lineIndents.Any())
            {
                return source;
            }
            var minIndent = lineIndents.Min();

            // インデントを除去
            for (int li = 0; li < lines.Length; ++li)
            {
                var line = lines[li];

                int indent = 0, pos = 0;
                for (; indent < minIndent; ++pos)
                {
                    var c = line[pos];
                    if (c == ' ')
                    {
                        ++indent;
                    }
                    else if (c == '\t')
                    {
                        indent += tabWidth;
                    }
                    else
                    {
                        break;
                    }
                }

                if (pos > 0)
                {
                    line = line.Substring(pos);

                    // タブ文字幅によって除去しすぎたならば半角スペースを補填
                    if (indent > minIndent)
                    {
                        line = (new string(' ', indent - minIndent)) + line;
                    }

                    lines[li] = line;
                }
            }

            return string.Join(newLine, lines);
        }
    }
}
