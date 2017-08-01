using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using RucheHome.Caches;

namespace RucheHome.Automation.Talkers.CeVIO
{
    /// <summary>
    /// パラメータID列挙。
    /// </summary>
    public enum ParameterId
    {
        /// <summary>
        /// 大きさ
        /// </summary>
        Volume,

        /// <summary>
        /// 速さ
        /// </summary>
        Speed,

        /// <summary>
        /// 高さ
        /// </summary>
        Tone,

        /// <summary>
        /// 声質
        /// </summary>
        Alpha,

        /// <summary>
        /// 抑揚
        /// </summary>
        Intonation,

        /// <summary>
        /// 感情 : 元気
        /// </summary>
        EmotionFine,

        /// <summary>
        /// 感情 : 普通
        /// </summary>
        EmotionNormal,

        /// <summary>
        /// 感情 : 怒り
        /// </summary>
        EmotionAngry,

        /// <summary>
        /// 感情 : 哀しみ
        /// </summary>
        EmotionSad,

        /// <summary>
        /// 感情 : クール
        /// </summary>
        EmotionCool,

        /// <summary>
        /// 感情 : 照れ
        /// </summary>
        EmotionShy,

        /// <summary>
        /// 感情 : へこみ
        /// </summary>
        EmotionDown,
    }

    /// <summary>
    /// <see cref="ParameterId"/> 列挙型に拡張メソッド等を提供する静的クラス。
    /// </summary>
    public static class ParameterIdExtension
    {
#if DEBUG
        /// <summary>
        /// 静的コンストラクタ。
        /// </summary>
        static ParameterIdExtension()
        {
            // Infos に全列挙値が含まれているか確認
            Debug.Assert(AllValues.All(p => Infos.ContainsKey(p)));

            // 全列挙値が音声効果関連または感情関連であることを確認
            Debug.Assert(AllValues.All(p => p.IsEffect() || p.IsEmotion()));
        }
#endif // DEBUG

        /// <summary>
        /// 全パラメータID値のコレクションを取得する。
        /// </summary>
        public static ReadOnlyCollection<ParameterId> AllValues =>
            EnumCache<ParameterId>.Values;

        /// <summary>
        /// パラメータ情報を取得する。
        /// </summary>
        /// <param name="self">パラメータID。</param>
        /// <returns>パラメータ情報。引数値が無効ならば null 。</returns>
        public static ParameterInfo<ParameterId> GetInfo(this ParameterId self) =>
            Infos.TryGetValue(self, out var info) ? info : null;

        /// <summary>
        /// 音声効果関連パラメータであるか否かを取得する。
        /// </summary>
        /// <param name="self">パラメータID。</param>
        /// <returns>音声効果関連パラメータならば true 。そうでなければ false 。</returns>
        public static bool IsEffect(this ParameterId self) =>
            (self >= ParameterId.Volume && self <= ParameterId.Intonation);

        /// <summary>
        /// 感情関連パラメータであるか否かを取得する。
        /// </summary>
        /// <param name="self">パラメータID。</param>
        /// <returns>感情関連パラメータならば true 。そうでなければ false 。</returns>
        public static bool IsEmotion(this ParameterId self) =>
            (self >= ParameterId.EmotionFine && self <= ParameterId.EmotionDown);

        /// <summary>
        /// 指定したキャラクターの感情関連パラメータであるか否かを取得する。
        /// </summary>
        /// <param name="self">パラメータID。</param>
        /// <param name="character">キャラクター。</param>
        /// <returns>
        /// 指定したキャラクターの感情関連パラメータならば true 。そうでなければ false 。
        /// </returns>
        public static bool IsEmotionOf(this ParameterId self, Cast character) =>
            (character.GetEmotionParameterIds()?.Contains(self) == true);

        /// <summary>
        /// 表示名から感情関連パラメータIDを検索する。
        /// </summary>
        /// <param name="displayName">表示名。</param>
        /// <returns>感情関連パラメータID。見つからなければ null 。</returns>
        public static ParameterId? FindEmotionByDisplayName(string displayName) =>
            Infos
                .Cast<KeyValuePair<ParameterId, ParameterInfo<ParameterId>>?>()
                .FirstOrDefault(
                    kv =>
                        kv.Value.Key.IsEmotion() &&
                        kv.Value.Value.DisplayName == displayName)?
                .Key;

        /// <summary>
        /// パラメータ情報ディクショナリ。
        /// </summary>
        private static readonly Dictionary<ParameterId, ParameterInfo<ParameterId>> Infos =
            new[]
            {
                new ParameterInfo<ParameterId>(
                    ParameterId.Volume, @"大きさ", 0, 50, 0, 100),
                new ParameterInfo<ParameterId>(
                    ParameterId.Speed, @"速さ", 0, 50, 0, 100),
                new ParameterInfo<ParameterId>(
                    ParameterId.Tone, @"高さ", 0, 50, 0, 100),
                new ParameterInfo<ParameterId>(
                    ParameterId.Alpha, @"声質", 0, 50, 0, 100),
                new ParameterInfo<ParameterId>(
                    ParameterId.Intonation, @"抑揚", 0, 50, 0, 100),

                new ParameterInfo<ParameterId>(
                    ParameterId.EmotionFine, @"元気", 0, 0, 0, 100),
                new ParameterInfo<ParameterId>(
                    ParameterId.EmotionNormal, @"普通", 0, 0, 0, 100),
                new ParameterInfo<ParameterId>(
                    ParameterId.EmotionAngry, @"怒り", 0, 0, 0, 100),
                new ParameterInfo<ParameterId>(
                    ParameterId.EmotionSad, @"哀しみ", 0, 0, 0, 100),
                new ParameterInfo<ParameterId>(
                    ParameterId.EmotionCool, @"クール", 0, 0, 0, 100),
                new ParameterInfo<ParameterId>(
                    ParameterId.EmotionShy, @"照れ", 0, 0, 0, 100),
                new ParameterInfo<ParameterId>(
                    ParameterId.EmotionDown, @"へこみ", 0, 0, 0, 100),
            }
            .ToDictionary(pi => pi.Id);
    }
}
