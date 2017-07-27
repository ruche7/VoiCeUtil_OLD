using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;

namespace RucheHome.Talker.CeVIO
{
    /// <summary>
    /// キャラクター列挙。
    /// </summary>
    public enum Character
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
    /// <see cref="Character"/> 列挙型に拡張メソッド等を提供する静的クラス。
    /// </summary>
    public static class CharacterExtension
    {
#if DEBUG
        /// <summary>
        /// 静的コンストラクタ。
        /// </summary>
        static CharacterExtension()
        {
            // Infos に全列挙値が含まれているか確認
            Debug.Assert(AllValues.All(p => Infos.ContainsKey(p)));
        }
#endif // DEBUG

        /// <summary>
        /// 全キャラクター列挙値のコレクションを取得する。
        /// </summary>
        public static ReadOnlyCollection<Character> AllValues { get; } =
            Array.AsReadOnly(((Character[])Enum.GetValues(typeof(Character))).ToArray());

        /// <summary>
        /// キャラクター名を取得する。
        /// </summary>
        /// <param name="self">キャラクター。</param>
        /// <returns>キャラクター名。引数値が無効ならば null 。</returns>
        public static string GetName(this Character self) =>
            Infos.TryGetValue(self, out var info) ? info.Name : null;

        /// <summary>
        /// キャラクターに紐付く感情パラメータIDの一覧を取得する。
        /// </summary>
        /// <param name="self">キャラクター。</param>
        /// <returns>感情パラメータIDの一覧。引数値が無効ならば null 。</returns>
        public static ReadOnlyCollection<ParameterId> GetEmotionParameterIds(
            this Character self)
            =>
            Infos.TryGetValue(self, out var info) ? info.EmotionParameterIds : null;

        /// <summary>
        /// キャラクター名からキャラクターを検索する。
        /// </summary>
        /// <param name="name">キャラクター名。</param>
        /// <returns>キャラクター。見つからなければ null 。</returns>
        public static Character? FindByName(string name) =>
            Infos
                .Cast<KeyValuePair<Character, Info>?>()
                .FirstOrDefault(kv => kv.Value.Value.Name == name)?
                .Key;

        /// <summary>
        /// キャラクター情報クラス。
        /// </summary>
        private class Info
        {
            /// <summary>
            /// コンストラクタ。
            /// </summary>
            /// <param name="name">キャラクター名。</param>
            /// <param name="emotionParameterIds">
            /// キャラクターに紐付く感情パラメータIDの列挙。
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
            /// キャラクター名を取得する。
            /// </summary>
            public string Name { get; }

            /// <summary>
            /// キャラクターに紐付く感情パラメータIDの一覧を取得する。
            /// </summary>
            public ReadOnlyCollection<ParameterId> EmotionParameterIds { get; }
        }

        /// <summary>
        /// キャラクター情報ディクショナリ。
        /// </summary>
        private static readonly Dictionary<Character, Info> Infos =
            new Dictionary<Character, Info>
            {
                {
                    Character.Sasara,
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
                    Character.Tsuzumi,
                    new Info(
                        @"すずきつづみ",
                        new[]
                        {
                            ParameterId.EmotionCool,
                            ParameterId.EmotionShy,
                        })
                },
                {
                    Character.Takahashi,
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
