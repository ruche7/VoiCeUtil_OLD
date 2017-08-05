using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace RucheHome.Automation.Friendly
{
    /// <summary>
    /// CLRバージョン種別列挙。
    /// </summary>
    public enum ClrVersion
    {
        /// <summary>
        /// v2.0.50727
        /// </summary>
        V2,

        /// <summary>
        /// v4.0.30319
        /// </summary>
        V4,
    }

    /// <summary>
    /// <see cref="ClrVersion"/> 列挙型に拡張メソッドを提供する静的クラス。
    /// </summary>
    public static class ClrVersionExtension
    {
#if DEBUG
        /// <summary>
        /// 静的コンストラクタ。
        /// </summary>
        static ClrVersionExtension()
        {
            // VersionStrings に全列挙値が含まれているか確認
            Debug.Assert(
                ((ClrVersion[])Enum.GetValues(typeof(ClrVersion)))
                    .All(p => VersionStrings.ContainsKey(p)));
        }
#endif // DEBUG

        /// <summary>
        /// CLRバージョン文字列を取得する。
        /// </summary>
        /// <param name="clrVersion">CLRバージョン。</param>
        /// <returns>CLRバージョン文字列。引数値が無効ならば null 。</returns>
        public static string GetVersionString(this ClrVersion clrVersion) =>
            VersionStrings.TryGetValue(clrVersion, out var s) ? s : null;

        /// <summary>
        /// CLRバージョン文字列ディクショナリ。
        /// </summary>
        private static readonly Dictionary<ClrVersion, string> VersionStrings =
            new Dictionary<ClrVersion, string>
            {
                { ClrVersion.V2, @"v2.0.50727" },
                { ClrVersion.V4, @"v4.0.30319" },
            };
    }
}
