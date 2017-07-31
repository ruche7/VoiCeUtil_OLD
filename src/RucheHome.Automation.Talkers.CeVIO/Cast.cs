using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;

namespace RucheHome.Automation.Talkers.CeVIO
{
    /// <summary>
    /// キャスト列挙。
    /// </summary>
    public enum Cast
    {
        /// <summary>
        /// さとうささら
        /// </summary>
        Sasara,

        /// <summary>
        /// すずきつづみ
        /// </summary>
        Tsuzumi,

        /// <summary>
        /// タカハシ
        /// </summary>
        Takahashi,
    }

    /// <summary>
    /// <see cref="Cast"/> 列挙型に拡張メソッド等を提供する静的クラス。
    /// </summary>
    public static class CastExtension
    {
#if DEBUG
        /// <summary>
        /// 静的コンストラクタ。
        /// </summary>
        static CastExtension()
        {
            // Infos に全列挙値が含まれているか確認
            Debug.Assert(AllValues.All(p => Infos.ContainsKey(p)));
        }
#endif // DEBUG

        /// <summary>
        /// 全キャスト列挙値のコレクションを取得する。
        /// </summary>
        public static ReadOnlyCollection<Cast> AllValues { get; } =
            Array.AsReadOnly(((Cast[])Enum.GetValues(typeof(Cast))).ToArray());

        /// <summary>
        /// キャスト名を取得する。
        /// </summary>
        /// <param name="cast">キャスト。</param>
        /// <returns>キャスト名。引数値が無効ならば null 。</returns>
        public static string GetName(this Cast cast) =>
            Infos.TryGetValue(cast, out var info) ? info.Name : null;

        /// <summary>
        /// キャストに紐付く感情パラメータIDの一覧を取得する。
        /// </summary>
        /// <param name="cast">キャスト。</param>
        /// <returns>感情パラメータIDの一覧。引数値が無効ならば null 。</returns>
        public static ReadOnlyCollection<ParameterId> GetEmotionParameterIds(this Cast cast)
            =>
            Infos.TryGetValue(cast, out var info) ? info.EmotionParameterIds : null;

        /// <summary>
        /// キャスト名からキャストを検索する。
        /// </summary>
        /// <param name="name">キャスト名。</param>
        /// <returns>キャスト。見つからなければ null 。</returns>
        public static Cast? FindByName(string name) =>
            Infos
                .Cast<KeyValuePair<Cast, Info>?>()
                .FirstOrDefault(kv => kv.Value.Value.Name == name)?
                .Key;

        /// <summary>
        /// キャスト情報クラス。
        /// </summary>
        private class Info
        {
            /// <summary>
            /// コンストラクタ。
            /// </summary>
            /// <param name="name">キャスト名。</param>
            /// <param name="emotionParameterIds">
            /// キャストに紐付く感情パラメータIDの列挙。
            /// </param>
            public Info(string name, IEnumerable<ParameterId> emotionParameterIds)
            {
                Debug.Assert(!string.IsNullOrWhiteSpace(name));
                Debug.Assert(emotionParameterIds != null);
                Debug.Assert(emotionParameterIds.All(id => id.IsEmotion()));

                this.Name = name;
                this.EmotionParameterIds = Array.AsReadOnly(emotionParameterIds.ToArray());
            }

            /// <summary>
            /// キャスト名を取得する。
            /// </summary>
            public string Name { get; }

            /// <summary>
            /// キャストに紐付く感情パラメータIDの一覧を取得する。
            /// </summary>
            public ReadOnlyCollection<ParameterId> EmotionParameterIds { get; }
        }

        /// <summary>
        /// キャスト情報ディクショナリ。
        /// </summary>
        private static readonly Dictionary<Cast, Info> Infos =
            new Dictionary<Cast, Info>
            {
                {
                    Cast.Sasara,
                    new Info(
                        @"さとうささら",
                        new[]
                        {
                            ParameterId.EmotionFine,
                            ParameterId.EmotionNormal,
                            ParameterId.EmotionAngry,
                            ParameterId.EmotionSad,
                        })
                },
                {
                    Cast.Tsuzumi,
                    new Info(
                        @"すずきつづみ",
                        new[]
                        {
                            ParameterId.EmotionCool,
                            ParameterId.EmotionShy,
                        })
                },
                {
                    Cast.Takahashi,
                    new Info(
                        @"タカハシ",
                        new[]
                        {
                            ParameterId.EmotionFine,
                            ParameterId.EmotionNormal,
                            ParameterId.EmotionDown,
                        })
                },
            };
    }
}
