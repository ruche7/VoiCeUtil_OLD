using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;

namespace RucheHome.Talker.CeVIO
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
        /// さとうささら : 元気
        /// </summary>
        SasaraActive,

        /// <summary>
        /// さとうささら : 普通
        /// </summary>
        SasaraNormal,

        /// <summary>
        /// さとうささら : 怒り
        /// </summary>
        SasaraAnger,

        /// <summary>
        /// さとうささら : 悲しみ
        /// </summary>
        SasaraSorrow,

        /// <summary>
        /// すずきつづみ : クール
        /// </summary>
        TsuzumiCool,

        /// <summary>
        /// すずきつづみ : 照れ
        /// </summary>
        TsuzumiShy,

        /// <summary>
        /// タカハシ : 元気
        /// </summary>
        TakahashiActive,

        /// <summary>
        /// タカハシ : 普通
        /// </summary>
        TakahashiNormal,

        /// <summary>
        /// タカハシ : へこみ
        /// </summary>
        TakahashiDown,
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
        public static ReadOnlyCollection<ParameterId> AllValues { get; } =
            Array.AsReadOnly(((ParameterId[])Enum.GetValues(typeof(ParameterId))).ToArray());

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
            (self >= ParameterId.SasaraActive && self <= ParameterId.TakahashiDown);

        /// <summary>
        /// 指定したキャラクターの感情関連パラメータであるか否かを取得する。
        /// </summary>
        /// <param name="self">パラメータID。</param>
        /// <param name="character">キャラクター。</param>
        /// <returns>
        /// 指定したキャラクターの感情関連パラメータならば true 。そうでなければ false 。
        /// </returns>
        public static bool IsEmotionOf(this ParameterId self, Character character) =>
            (character.GetEmotionParameterIds()?.Contains(self) == true);

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

                // さとうささら
                new ParameterInfo<ParameterId>(
                    ParameterId.SasaraActive, @"元気", 0, 0, 0, 100),
                new ParameterInfo<ParameterId>(
                    ParameterId.SasaraNormal, @"普通", 0, 0, 0, 100),
                new ParameterInfo<ParameterId>(
                    ParameterId.SasaraAnger, @"怒り", 0, 0, 0, 100),
                new ParameterInfo<ParameterId>(
                    ParameterId.SasaraSorrow, @"悲しみ", 0, 0, 0, 100),

                // すずきつづみ
                new ParameterInfo<ParameterId>(
                    ParameterId.TsuzumiCool, @"クール", 0, 0, 0, 100),
                new ParameterInfo<ParameterId>(
                    ParameterId.TsuzumiShy, @"照れ", 0, 0, 0, 100),

                // タカハシ
                new ParameterInfo<ParameterId>(
                    ParameterId.TakahashiActive, @"元気", 0, 0, 0, 100),
                new ParameterInfo<ParameterId>(
                    ParameterId.TakahashiNormal, @"普通", 0, 0, 0, 100),
                new ParameterInfo<ParameterId>(
                    ParameterId.TakahashiDown, @"へこみ", 0, 0, 0, 100),
            }
            .ToDictionary(pi => pi.Id);
    }
}
