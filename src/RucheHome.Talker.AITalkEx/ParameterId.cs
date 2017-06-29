using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RucheHome.Talker.AITalkEx
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
    /// <see cref="ParameterId"/> 列挙型に拡張メソッドを提供する静的クラス。
    /// </summary>
    public static class ParameterIdExtension
    {
        /// <summary>
        /// パラメータ情報を取得する。
        /// </summary>
        /// <param name="self">パラメータID。</param>
        /// <returns>パラメータ情報。引数値が無効ならば null 。</returns>
        public static ParameterInfo<ParameterId> GetInfo(this ParameterId self) =>
            Infos.TryGetValue(self, out var info) ? info : null;

        /// <summary>
        /// パラメータ情報ディクショナリ。
        /// </summary>
        public static readonly Dictionary<ParameterId, ParameterInfo<ParameterId>> Infos =
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
