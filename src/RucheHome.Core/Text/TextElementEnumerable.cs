using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

using static RucheHome.Diagnostics.ArgumentValidater;

namespace RucheHome.Text
{
    /// <summary>
    /// サロゲートペアを考慮した文字列挙を提供するクラス。
    /// </summary>
    public class TextElementEnumerable : IEnumerable<string>
    {
        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="source">対象文字列。</param>
        public TextElementEnumerable(string source)
        {
            ValidateArgumentNull(source, nameof(source));

            this.Source = source;
        }

        /// <summary>
        /// 対象文字列を取得する。
        /// </summary>
        public string Source { get; }

        #region IEnumerable<string> の実装

        /// <summary>
        /// 文字列挙子を取得する。
        /// </summary>
        /// <returns>文字列挙子。</returns>
        public IEnumerator<string> GetEnumerator()
        {
            for (var e = StringInfo.GetTextElementEnumerator(this.Source); e.MoveNext(); )
            {
                yield return e.GetTextElement();
            }
        }

        #endregion

        #region IEnumerable の明示的実装

        IEnumerator IEnumerable.GetEnumerator()
        {
            return StringInfo.GetTextElementEnumerator(this.Source);
        }

        #endregion
    }
}
