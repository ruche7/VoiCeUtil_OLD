using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using RucheHome.Caches;

namespace RucheHome.Automation.Talkers.AITalkEx
{
    /// <summary>
    /// パラメータID列挙。
    /// </summary>
    public enum ParameterId
    {
        /// <summary>
        /// 音量
        /// </summary>
        Volume,

        /// <summary>
        /// 話速
        /// </summary>
        Speed,

        /// <summary>
        /// 高さ
        /// </summary>
        Tone,

        /// <summary>
        /// 抑揚
        /// </summary>
        Intonation,

        /// <summary>
        /// 文中短ポーズ
        /// </summary>
        PauseShort,

        /// <summary>
        /// 文中長ポーズ
        /// </summary>
        PauseLong,

        /// <summary>
        /// 文末ポーズ
        /// </summary>
        PauseSentence,

        /// <summary>
        /// 開始ポーズ
        /// </summary>
        PauseBegin,

        /// <summary>
        /// 終了ポーズ
        /// </summary>
        PauseEnd,
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

            // 全列挙値が音声効果設定またはボイスプリセット設定であることを確認
            Debug.Assert(AllValues.All(p => p.IsEffect() || p.IsPause()));
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
        /// <param name="id">パラメータID。</param>
        /// <returns>パラメータ情報。引数値が無効ならば null 。</returns>
        public static ParameterInfo<ParameterId> GetInfo(this ParameterId id) =>
            Infos.TryGetValue(id, out var info) ? info : null;

        /// <summary>
        /// 音声効果設定であるか否かを取得する。
        /// </summary>
        /// <param name="id">パラメータID。</param>
        /// <returns>音声効果設定ならば true 。そうでなければ false 。</returns>
        public static bool IsEffect(this ParameterId id) =>
            (id >= ParameterId.Volume && id <= ParameterId.Intonation);

        /// <summary>
        /// ポーズ設定であるか否かを取得する。
        /// </summary>
        /// <param name="id">パラメータID。</param>
        /// <returns>ポーズ設定ならば true 。そうでなければ false 。</returns>
        public static bool IsPause(this ParameterId id) =>
            (id >= ParameterId.PauseShort && id <= ParameterId.PauseEnd);

        /// <summary>
        /// パラメータ情報ディクショナリ。
        /// </summary>
        private static readonly Dictionary<ParameterId, ParameterInfo<ParameterId>> Infos =
            new[]
            {
                new ParameterInfo<ParameterId>(
                    ParameterId.Volume, @"音量", 2, 1.00m, 0.00m, 2.00m),
                new ParameterInfo<ParameterId>(
                    ParameterId.Speed, @"話速", 2, 1.00m, 0.50m, 4.00m),
                new ParameterInfo<ParameterId>(
                    ParameterId.Tone, @"高さ", 2, 1.00m, 0.50m, 2.00m),
                new ParameterInfo<ParameterId>(
                    ParameterId.Intonation, @"抑揚", 2, 1.00m, 0.00m, 2.00m),
                new ParameterInfo<ParameterId>(
                    ParameterId.PauseShort, @"文中短ポーズ", 0, 150, 80, 500),
                new ParameterInfo<ParameterId>(
                    ParameterId.PauseLong, @"文中長ポーズ", 0, 370, 100, 2000),
                new ParameterInfo<ParameterId>(
                    ParameterId.PauseSentence, @"文末ポーズ", 0, 800, 200, 10000),
                new ParameterInfo<ParameterId>(
                    ParameterId.PauseBegin, @"開始ポーズ", 0, 0, 0, 10000),
                new ParameterInfo<ParameterId>(
                    ParameterId.PauseEnd, @"終了ポーズ", 0, 0, 0, 10000),
            }
            .ToDictionary(pi => pi.Id);
    }
}
