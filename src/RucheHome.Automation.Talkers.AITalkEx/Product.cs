using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace RucheHome.Automation.Talkers.AITalkEx
{
    /// <summary>
    /// 操作対象製品を表す列挙。
    /// </summary>
    public enum Product
    {
        /// <summary>
        /// VOICEROID+ 結月ゆかり EX
        /// </summary>
        VoiceroidYukariEx,

        /// <summary>
        /// VOICEROID+ 民安ともえ(弦巻マキ) EX
        /// </summary>
        VoiceroidMakiEx,

        /// <summary>
        /// VOICEROID+ 東北ずん子 EX
        /// </summary>
        VoiceroidZunkoEx,

        /// <summary>
        /// VOICEROID+ 東北きりたん EX
        /// </summary>
        VoiceroidKiritanEx,

        /// <summary>
        /// VOICEROID+ 琴葉茜
        /// </summary>
        VoiceroidAkane,

        /// <summary>
        /// VOICEROID+ 琴葉葵
        /// </summary>
        VoiceroidAoi,

        /// <summary>
        /// VOICEROID+ 月読アイ EX
        /// </summary>
        VoiceroidAiEx,

        /// <summary>
        /// VOICEROID+ 月読ショウタ EX
        /// </summary>
        VoiceroidShoutaEx,

        /// <summary>
        /// VOICEROID+ 京町セイカ EX
        /// </summary>
        VoiceroidSeikaEx,

        /// <summary>
        /// VOICEROID+ 水奈瀬コウ EX
        /// </summary>
        VoiceroidKouEx,

        /// <summary>
        /// 音街ウナTalk Ex
        /// </summary>
        UnaTalkEx,
    }

    /// <summary>
    /// <see cref="Product"/> 列挙型に拡張メソッドを提供する静的クラス。
    /// </summary>
    public static class ProductExtension
    {
#if DEBUG
        /// <summary>
        /// 静的コンストラクタ。
        /// </summary>
        static ProductExtension()
        {
            // Infos に全列挙値が含まれているか確認
            Debug.Assert(
                ((Product[])Enum.GetValues(typeof(Product)))
                    .All(p => Infos.ContainsKey(p)));
        }
#endif // DEBUG

        /// <summary>
        /// プロセスの実行ファイル名(拡張子なし)を取得する。
        /// </summary>
        /// <param name="product">製品種別。</param>
        /// <returns>
        /// プロセスの実行ファイル名(拡張子なし)。引数値が無効ならば null 。
        /// </returns>
        public static string GetProcessFileName(this Product product) =>
            FindInfo(product)?.ProcessFileName;

        /// <summary>
        /// プロセスの製品名情報を取得する。
        /// </summary>
        /// <param name="product">製品種別。</param>
        /// <returns>プロセスの製品名情報。引数値が無効ならば null 。</returns>
        public static string GetProcessProduct(this Product product) =>
            FindInfo(product)?.ProcessProduct;

        /// <summary>
        /// 話者名を取得する。
        /// </summary>
        /// <param name="product">製品種別。</param>
        /// <returns>話者名。引数値が無効ならば null 。</returns>
        public static string GetTalkerName(this Product product) =>
            FindInfo(product)?.TalkerName;

        /// <summary>
        /// 製品情報構造体。
        /// </summary>
        private struct Info
        {
            /// <summary>
            /// コンストラクタ。
            /// </summary>
            /// <param name="processFileName">プロセスの実行ファイル名(拡張子なし)。</param>
            /// <param name="processProduct">プロセスの製品名情報。</param>
            /// <param name="talkerName">話者名。 null ならば processProduct を使う。</param>
            public Info(
                string processFileName,
                string processProduct,
                string talkerName = null)
            {
                Debug.Assert(!string.IsNullOrWhiteSpace(processFileName));
                Debug.Assert(!string.IsNullOrWhiteSpace(processProduct));

                this.ProcessFileName = processFileName;
                this.ProcessProduct = processProduct;
                this.TalkerName = talkerName ?? processProduct;
            }

            /// <summary>
            /// プロセスの実行ファイル名(拡張子なし)を取得する。
            /// </summary>
            public string ProcessFileName { get; }

            /// <summary>
            /// プロセスの製品名情報を取得する。
            /// </summary>
            public string ProcessProduct { get; }

            /// <summary>
            /// 話者名を取得する。
            /// </summary>
            public string TalkerName { get; }
        }

        /// <summary>
        /// VOICEROID+ EX シリーズの実行ファイル名。
        /// </summary>
        private const string VoiceroidProcessFileName = @"VOICEROID";

        /// <summary>
        /// 製品情報ディクショナリ。
        /// </summary>
        private static readonly Dictionary<Product, Info> Infos =
            new Dictionary<Product, Info>
            {
                {
                    Product.VoiceroidYukariEx,
                    new Info(
                        VoiceroidProcessFileName,
                        @"VOICEROID＋ 結月ゆかり EX",
                        @"VOICEROID+ 結月ゆかり EX")
                },
                {
                    Product.VoiceroidMakiEx,
                    new Info(
                        VoiceroidProcessFileName,
                        @"VOICEROID＋ 民安ともえ EX",
                        @"VOICEROID+ 民安ともえ(弦巻マキ) EX")
                },
                {
                    Product.VoiceroidZunkoEx,
                    new Info(
                        VoiceroidProcessFileName,
                        @"VOICEROID＋ 東北ずん子 EX",
                        @"VOICEROID+ 東北ずん子 EX")
                },
                {
                    Product.VoiceroidKiritanEx,
                    new Info(
                        VoiceroidProcessFileName,
                        @"VOICEROID＋ 東北きりたん",
                        @"VOICEROID+ 東北きりたん EX")
                },
                {
                    Product.VoiceroidAkane,
                    new Info(
                        VoiceroidProcessFileName,
                        @"VOICEROID＋ 琴葉茜",
                        @"VOICEROID+ 琴葉茜")
                },
                {
                    Product.VoiceroidAoi,
                    new Info(
                        VoiceroidProcessFileName,
                        @"VOICEROID＋ 琴葉葵",
                        @"VOICEROID+ 琴葉葵")
                },
                {
                    Product.VoiceroidAiEx,
                    new Info(
                        VoiceroidProcessFileName,
                        @"VOICEROID＋ 月読アイ EX",
                        @"VOICEROID+ 月読アイ EX")
                },
                {
                    Product.VoiceroidShoutaEx,
                    new Info(
                        VoiceroidProcessFileName,
                        @"VOICEROID＋ 月読ショウタ EX",
                        @"VOICEROID+ 月読ショウタ EX")
                },
                {
                    Product.VoiceroidSeikaEx,
                    new Info(
                        VoiceroidProcessFileName,
                        @"VOICEROID＋ 京町セイカ",
                        @"VOICEROID+ 京町セイカ EX")
                },
                {
                    Product.VoiceroidKouEx,
                    new Info(
                        VoiceroidProcessFileName,
                        @"VOICEROID＋ 水奈瀬コウ EX",
                        @"VOICEROID+ 水奈瀬コウ EX")
                },
                {
                    Product.UnaTalkEx,
                    new Info(
                        @"OtomachiUnaTalkEx",
                        @"音街ウナTalk Ex")
                },
            };

        /// <summary>
        /// 製品情報を検索する。
        /// </summary>
        /// <param name="product">製品種別。</param>
        /// <returns>製品情報。引数値が無効ならば null 。</returns>
        private static Info? FindInfo(Product product) =>
            Infos.TryGetValue(product, out var info) ? (Info?)info : null;
    }
}
