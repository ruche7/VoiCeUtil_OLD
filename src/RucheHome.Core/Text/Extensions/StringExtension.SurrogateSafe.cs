using System;
using System.Diagnostics;
using System.Text;
using RucheHome.Diagnostics;

namespace RucheHome.Text.Extensions
{
    // サロゲートペア関連拡張メソッド実装
    public static partial class StringExtension
    {
        /// <summary>
        /// サロゲートペアを分断しないように部分文字列を削除する。
        /// </summary>
        /// <param name="source">対象文字列。</param>
        /// <param name="startIndex">削除開始位置。</param>
        /// <param name="moveAfter">
        /// 指定位置がサロゲートペアを分断する時、位置を後方にずらすならば true 。
        /// 既定では前方にずらす。
        /// </param>
        /// <returns>部分文字列を削除した文字列。</returns>
        public static string RemoveSurrogateSafe(
            this string source,
            int startIndex,
            bool moveAfter = false)
        {
            ArgumentValidation.IsNotNull(source, nameof(source));

            CorrectRangeSurrogateSafe(
                source.Length,
                source[startIndex],
                ref startIndex,
                moveAfter);

            return source.Remove(startIndex);
        }

        /// <summary>
        /// サロゲートペアを分断しないように部分文字列を削除する。
        /// </summary>
        /// <param name="source">対象文字列。</param>
        /// <param name="startIndex">削除開始位置。</param>
        /// <param name="count">削除文字数。</param>
        /// <param name="moveAfter">
        /// 指定位置がサロゲートペアを分断する時、位置を後方にずらすならば true 。
        /// 既定では前方にずらす。
        /// </param>
        /// <returns>部分文字列を削除した文字列。</returns>
        public static string RemoveSurrogateSafe(
            this string source,
            int startIndex,
            int count,
            bool moveAfter = false)
        {
            ArgumentValidation.IsNotNull(source, nameof(source));

            CorrectRangeSurrogateSafe(
                source.Length,
                i => source[i],
                ref startIndex,
                ref count,
                moveAfter);

            return source.Remove(startIndex, count);
        }

        /// <summary>
        /// サロゲートペアを分断しないように部分文字列を取得する。
        /// </summary>
        /// <param name="source">対象文字列。</param>
        /// <param name="startIndex">取得開始位置。</param>
        /// <param name="moveAfter">
        /// 指定位置がサロゲートペアを分断する時、位置を後方にずらすならば true 。
        /// 既定では前方にずらす。
        /// </param>
        /// <returns>取得した部分文字列。</returns>
        public static string SubstringSurrogateSafe(
            this string source,
            int startIndex,
            bool moveAfter = false)
        {
            ArgumentValidation.IsNotNull(source, nameof(source));

            CorrectRangeSurrogateSafe(
                source.Length,
                source[startIndex],
                ref startIndex,
                moveAfter);

            return source.Substring(startIndex);
        }

        /// <summary>
        /// サロゲートペアを分断しないように部分文字列を取得する。
        /// </summary>
        /// <param name="source">対象文字列。</param>
        /// <param name="startIndex">取得開始位置。</param>
        /// <param name="count">取得文字数。</param>
        /// <param name="moveAfter">
        /// 指定位置がサロゲートペアを分断する時、位置を後方にずらすならば true 。
        /// 既定では前方にずらす。
        /// </param>
        /// <returns>取得した部分文字列。</returns>
        public static string SubstringSurrogateSafe(
            this string source,
            int startIndex,
            int count,
            bool moveAfter = false)
        {
            ArgumentValidation.IsNotNull(source, nameof(source));

            CorrectRangeSurrogateSafe(
                source.Length,
                i => source[i],
                ref startIndex,
                ref count,
                moveAfter);

            return source.Substring(startIndex, count);
        }

        /// <summary>
        /// サロゲートペアを分断しないように部分文字列を削除する。
        /// </summary>
        /// <param name="source">対象文字列。</param>
        /// <param name="startIndex">削除開始位置。</param>
        /// <param name="moveAfter">
        /// 指定位置がサロゲートペアを分断する時、位置を後方にずらすならば true 。
        /// 既定では前方にずらす。
        /// </param>
        /// <returns>部分文字列を削除した文字列。</returns>
        public static StringBuilder RemoveSurrogateSafe(
            this StringBuilder source,
            int startIndex,
            bool moveAfter = false)
        {
            ArgumentValidation.IsNotNull(source, nameof(source));

            CorrectRangeSurrogateSafe(
                source.Length,
                source[startIndex],
                ref startIndex,
                moveAfter);

            var length =
                (source == null || startIndex < 0 || startIndex > source.Length) ?
                    0 : (source.Length - startIndex);

            return source.Remove(startIndex, length);
        }

        /// <summary>
        /// サロゲートペアを分断しないように部分文字列を削除する。
        /// </summary>
        /// <param name="source">対象文字列。</param>
        /// <param name="startIndex">削除開始位置。</param>
        /// <param name="length">削除文字数。</param>
        /// <param name="moveAfter">
        /// 指定位置がサロゲートペアを分断する時、位置を後方にずらすならば true 。
        /// 既定では前方にずらす。
        /// </param>
        /// <returns>部分文字列を削除した文字列。</returns>
        public static StringBuilder RemoveSurrogateSafe(
            this StringBuilder source,
            int startIndex,
            int length,
            bool moveAfter = false)
        {
            ArgumentValidation.IsNotNull(source, nameof(source));

            CorrectRangeSurrogateSafe(
                source.Length,
                i => source[i],
                ref startIndex,
                ref length,
                moveAfter);

            return source.Remove(startIndex, length);
        }

        /// <summary>
        /// サロゲートペアを分断しないように部分文字列を取得する。
        /// </summary>
        /// <param name="source">対象文字列。</param>
        /// <param name="startIndex">取得開始位置。</param>
        /// <param name="moveAfter">
        /// 指定位置がサロゲートペアを分断する時、位置を後方にずらすならば true 。
        /// 既定では前方にずらす。
        /// </param>
        /// <returns>取得した部分文字列。</returns>
        public static string ToStringSurrogateSafe(
            this StringBuilder source,
            int startIndex,
            bool moveAfter = false)
        {
            ArgumentValidation.IsNotNull(source, nameof(source));

            CorrectRangeSurrogateSafe(
                source.Length,
                source[startIndex],
                ref startIndex,
                moveAfter);

            var length =
                (source == null || startIndex < 0 || startIndex > source.Length) ?
                    0 : (source.Length - startIndex);

            return source.ToString(startIndex, length);
        }

        /// <summary>
        /// サロゲートペアを分断しないように部分文字列を取得する。
        /// </summary>
        /// <param name="source">対象文字列。</param>
        /// <param name="startIndex">取得開始位置。</param>
        /// <param name="length">取得文字数。</param>
        /// <param name="moveAfter">
        /// 指定位置がサロゲートペアを分断する時、位置を後方にずらすならば true 。
        /// 既定では前方にずらす。
        /// </param>
        /// <returns>取得した部分文字列。</returns>
        public static string ToStringSurrogateSafe(
            this StringBuilder source,
            int startIndex,
            int length,
            bool moveAfter = false)
        {
            ArgumentValidation.IsNotNull(source, nameof(source));

            CorrectRangeSurrogateSafe(
                source.Length,
                i => source[i],
                ref startIndex,
                ref length,
                moveAfter);

            return source.ToString(startIndex, length);
        }

        /// <summary>
        /// サロゲートペアを分断しないように範囲指定値を補正する。
        /// </summary>
        /// <param name="sourceLength">処理対象文字列の長さ。</param>
        /// <param name="sourceCharAtStartIndex">
        /// 処理対象文字列の startIndex の位置にある文字。
        /// </param>
        /// <param name="startIndex">補正対象の開始位置値。</param>
        /// <param name="moveAfter">
        /// 指定位置がサロゲートペアを分断する時、位置を後方にずらすならば true 。
        /// 既定では前方にずらす。
        /// </param>
        private static void CorrectRangeSurrogateSafe(
            int sourceLength,
            char sourceCharAtStartIndex,
            ref int startIndex,
            bool moveAfter)
        {
            if (
                startIndex > 0 &&
                startIndex < sourceLength &&
                char.IsLowSurrogate(sourceCharAtStartIndex))
            {
                // 開始位置が下位サロゲートなら範囲を移動
                startIndex += moveAfter ? +1 : -1;
            }
        }

        /// <summary>
        /// サロゲートペアを分断しないように範囲指定値を補正する。
        /// </summary>
        /// <param name="sourceLength">処理対象文字列の長さ。</param>
        /// <param name="sourceCharGetter">
        /// 処理対象文字列から特定位置の文字を取得するデリゲート。
        /// </param>
        /// <param name="startIndex">補正対象の開始位置値。</param>
        /// <param name="count">補正対象の文字数値。</param>
        /// <param name="moveAfter">
        /// 指定位置がサロゲートペアを分断する時、位置を後方にずらすならば true 。
        /// 既定では前方にずらす。
        /// </param>
        private static void CorrectRangeSurrogateSafe(
            int sourceLength,
            Func<int, char> sourceCharGetter,
            ref int startIndex,
            ref int count,
            bool moveAfter)
        {
            Debug.Assert(sourceCharGetter != null);

            if (
                startIndex >= 0 &&
                count >= 0 &&
                startIndex + count <= sourceLength)
            {
                if (
                    startIndex > 0 &&
                    startIndex < sourceLength &&
                    char.IsLowSurrogate(sourceCharGetter(startIndex)))
                {
                    // 開始位置が下位サロゲートなら範囲を移動
                    startIndex += moveAfter ? +1 : -1;

                    // ++startIndex により終端位置が範囲外になるなら補正
                    if (moveAfter && startIndex + count > sourceLength)
                    {
                        --count;
                    }
                }

                if (count > 0)
                {
                    var end = startIndex + count;
                    if (end < sourceLength && char.IsLowSurrogate(sourceCharGetter(end)))
                    {
                        // 終端位置が下位サロゲートなら範囲を移動
                        count += moveAfter ? +1 : -1;
                    }
                }
            }
        }
    }
}
