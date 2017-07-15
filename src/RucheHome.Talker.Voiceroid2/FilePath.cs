using System;
using System.IO;
using RucheHome.Diagnostics;

namespace RucheHome.Talker.Voiceroid2
{
    /// <summary>
    /// VOICEROID2のファイルパスに関する処理を提供する静的クラス。
    /// </summary>
    public static class FilePath
    {
        /// <summary>
        /// ファイルパスをVOICEROID2の連番ファイルパスに変更する。
        /// </summary>
        /// <param name="filePath">ファイルパス。 null や空文字列であってはならない。</param>
        /// <param name="index">連番インデックス。負数であってはならない。</param>
        /// <returns>連番ファイルパス。</returns>
        /// <remarks>
        /// 拡張子種別を問わず、拡張子の直前に ("-" + index) を挿入する。
        /// </remarks>
        public static string ToSequential(string filePath, int index)
        {
            ArgumentValidation.IsNotNullOrEmpty(filePath, nameof(filePath));
            ArgumentValidation.IsEqualsOrGreaterThan(index, 0, nameof(index));

            return
                Path.Combine(
                    Path.GetDirectoryName(filePath),
                    Path.GetFileNameWithoutExtension(filePath) +
                    @"-" +
                    index +
                    Path.GetExtension(filePath));
        }

        /// <summary>
        /// ファイルパスがVOICEROID2の連番ファイルパス形式ならば連番インデックスを取得する。
        /// </summary>
        /// <param name="filePath">ファイルパス。</param>
        /// <returns>連番インデックス。連番ファイルパス形式ではないならば -1 。</returns>
        public static int GetSequentialIndex(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return -1;
            }

            var name = Path.GetFileNameWithoutExtension(filePath);
            var pos = name.LastIndexOf('-');
            if (pos < 0)
            {
                return -1;
            }

            return int.TryParse(name.Substring(pos + 1), out var index) ? index : -1;
        }
    }
}
