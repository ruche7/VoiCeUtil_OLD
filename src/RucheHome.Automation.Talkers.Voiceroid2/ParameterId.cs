using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;

namespace RucheHome.Automation.Talkers.Voiceroid2
{
    /// <summary>
    /// パラメータID列挙。
    /// </summary>
    public enum ParameterId
    {
        /// <summary>
        /// マスター : 音量
        /// </summary>
        Volume,

        /// <summary>
        /// マスター : 話速
        /// </summary>
        Speed,

        /// <summary>
        /// マスター : 高さ
        /// </summary>
        Tone,

        /// <summary>
        /// マスター : 抑揚
        /// </summary>
        Intonation,

        /// <summary>
        /// マスター : 短ポーズ
        /// </summary>
        PauseShort,

        /// <summary>
        /// マスター : 長ポーズ
        /// </summary>
        PauseLong,

        /// <summary>
        /// マスター : 文末ポーズ
        /// </summary>
        PauseSentence,

        /// <summary>
        /// ボイスプリセット : 音量
        /// </summary>
        PresetVolume,

        /// <summary>
        /// ボイスプリセット : 話速
        /// </summary>
        PresetSpeed,

        /// <summary>
        /// ボイスプリセット : 高さ
        /// </summary>
        PresetTone,

        /// <summary>
        /// ボイスプリセット : 抑揚
        /// </summary>
        PresetIntonation,

        /// <summary>
        /// ボイスプリセット : 喜び
        /// </summary>
        PresetJoy,

        /// <summary>
        /// ボイスプリセット : 怒り
        /// </summary>
        PresetAnger,

        /// <summary>
        /// ボイスプリセット : 悲しみ
        /// </summary>
        PresetSorrow,
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

            // 全列挙値がマスター設定またはボイスプリセット設定であることを確認
            Debug.Assert(AllValues.All(p => p.IsMaster() || p.IsPreset()));
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
        /// マスター設定であるか否かを取得する。
        /// </summary>
        /// <param name="self">パラメータID。</param>
        /// <returns>マスター設定ならば true 。そうでなければ false 。</returns>
        public static bool IsMaster(this ParameterId self) =>
            (self >= ParameterId.Volume && self <= ParameterId.PauseSentence);

        /// <summary>
        /// ボイスプリセット設定であるか否かを取得する。
        /// </summary>
        /// <param name="self">パラメータID。</param>
        /// <returns>ボイスプリセット設定ならば true 。そうでなければ false 。</returns>
        public static bool IsPreset(this ParameterId self) =>
            (self >= ParameterId.PresetVolume && self <= ParameterId.PresetSorrow);

        /// <summary>
        /// 存在しない可能性のある設定であるか否かを取得する。
        /// </summary>
        /// <param name="self">パラメータID。</param>
        /// <returns>存在しない可能性があるならば true 。そうでなければ false 。</returns>
        public static bool IsOptional(this ParameterId self) =>
            (self >= ParameterId.PresetJoy && self <= ParameterId.PresetSorrow);

        /// <summary>
        /// パラメータ情報ディクショナリ。
        /// </summary>
        private static readonly Dictionary<ParameterId, ParameterInfo<ParameterId>> Infos =
            new[]
            {
                new ParameterInfo<ParameterId>(
                    ParameterId.Volume, @"音量", 2, 1.00m, 0.00m, 5.00m),
                new ParameterInfo<ParameterId>(
                    ParameterId.Speed, @"話速", 2, 1.00m, 0.50m, 4.00m),
                new ParameterInfo<ParameterId>(
                    ParameterId.Tone, @"高さ", 2, 1.00m, 0.50m, 2.00m),
                new ParameterInfo<ParameterId>(
                    ParameterId.Intonation, @"抑揚", 2, 1.00m, 0.00m, 2.00m),
                new ParameterInfo<ParameterId>(
                    ParameterId.PauseShort, @"短ポーズ", 0, 150, 80, 500),
                new ParameterInfo<ParameterId>(
                    ParameterId.PauseLong, @"長ポーズ", 0, 370, 100, 2000),
                new ParameterInfo<ParameterId>(
                    ParameterId.PauseSentence, @"文末ポーズ", 0, 800, 0, 10000),

                new ParameterInfo<ParameterId>(
                    ParameterId.PresetVolume, @"音量", 2, 1.00m, 0.00m, 2.00m),
                new ParameterInfo<ParameterId>(
                    ParameterId.PresetSpeed, @"話速", 2, 1.00m, 0.50m, 4.00m),
                new ParameterInfo<ParameterId>(
                    ParameterId.PresetTone, @"高さ", 2, 1.00m, 0.50m, 2.00m),
                new ParameterInfo<ParameterId>(
                    ParameterId.PresetIntonation, @"抑揚", 2, 1.00m, 0.00m, 2.00m),
                new ParameterInfo<ParameterId>(
                    ParameterId.PresetJoy, @"喜び", 2, 0.00m, 0.00m, 1.00m),
                new ParameterInfo<ParameterId>(
                    ParameterId.PresetAnger, @"怒り", 2, 0.00m, 0.00m, 1.00m),
                new ParameterInfo<ParameterId>(
                    ParameterId.PresetSorrow, @"悲しみ", 2, 0.00m, 0.00m, 1.00m),
            }
            .ToDictionary(pi => pi.Id);
    }
}
