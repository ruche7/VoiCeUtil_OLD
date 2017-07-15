using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;

namespace RucheHome.Talker.Voiceroid2
{
    /// <summary>
    /// パラメータID列挙。
    /// </summary>
    public enum ParameterId
    {
        /// <summary>
        /// マスター音量
        /// </summary>
        Volume,

        /// <summary>
        /// マスター話速
        /// </summary>
        Speed,

        /// <summary>
        /// マスター高さ
        /// </summary>
        Tone,

        /// <summary>
        /// マスター抑揚
        /// </summary>
        Intonation,

        /// <summary>
        /// マスター短ポーズ
        /// </summary>
        PauseShort,

        /// <summary>
        /// マスター長ポーズ
        /// </summary>
        PauseLong,

        /// <summary>
        /// マスター文末ポーズ
        /// </summary>
        PauseSentence,

        /// <summary>
        /// ボイスプリセット音量
        /// </summary>
        PresetVolume,

        /// <summary>
        /// ボイスプリセット話速
        /// </summary>
        PresetSpeed,

        /// <summary>
        /// ボイスプリセット高さ
        /// </summary>
        PresetTone,

        /// <summary>
        /// ボイスプリセット抑揚
        /// </summary>
        PresetIntonation,

        /// <summary>
        /// ボイスプリセット喜び
        /// </summary>
        PresetJoy,

        /// <summary>
        /// ボイスプリセット怒り
        /// </summary>
        PresetAnger,

        /// <summary>
        /// ボイスプリセット悲しみ
        /// </summary>
        PresetSorrow,
    }

    /// <summary>
    /// <see cref="ParameterId"/> 列挙型に拡張メソッドを提供する静的クラス。
    /// </summary>
    public static class ParameterIdExtension
    {
#if DEBUG
        /// <summary>
        /// 静的コンストラクタ。
        /// </summary>
        static ParameterIdExtension()
        {
            // Infos, VisualTreeIndices に全列挙値が含まれているか確認
            Debug.Assert(
                AllIds.All(p => Infos.ContainsKey(p) && VisualTreeIndices.ContainsKey(p)));
        }
#endif // DEBUG

        /// <summary>
        /// 全パラメータID値のコレクションを取得する。
        /// </summary>
        public static ReadOnlyCollection<ParameterId> AllIds { get; } =
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
        /// スライダーのビジュアルツリーインデックス配列を取得する。
        /// </summary>
        /// <param name="self">パラメータID。</param>
        /// <returns>ビジュアルツリーインデックス配列。引数値が無効ならば null 。</returns>
        internal static int[] GetVisualTreeIndices(this ParameterId self) =>
            VisualTreeIndices.TryGetValue(self, out var indices) ?
                CommonVisualTreeIndices.Concat(indices).ToArray() : null;

        /// <summary>
        /// タブアイテム名を取得する。
        /// </summary>
        /// <param name="self">パラメータID。</param>
        /// <returns>タブアイテム名。引数値が無効ならば null 。</returns>
        internal static string GetTabItemName(this ParameterId self) =>
            self.IsMaster() ? @"マスター" : (self.IsPreset() ? @"ボイス" : null);

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

        /// <summary>
        /// スライダーのビジュアルツリーインデックス配列の共通部分。
        /// </summary>
        private static readonly int[] CommonVisualTreeIndices =
            { 0, 0, 0, 0, 1, 2, 0, 1, 0, 0, 0, 0, 0 };

        /// <summary>
        /// スライダーのビジュアルツリーインデックス配列ディクショナリ。
        /// </summary>
        private static readonly Dictionary<ParameterId, int[]> VisualTreeIndices =
            new Dictionary<ParameterId, int[]>
            {
                // マスター
                {
                    ParameterId.Volume,
                    new[] { 0, 0, 1, 0, 1, 0, 0, 0, 0, 2 }
                },
                {
                    ParameterId.Speed,
                    new[] { 0, 0, 1, 0, 1, 1, 0, 0, 0, 2 }
                },
                {
                    ParameterId.Tone,
                    new[] { 0, 0, 1, 0, 1, 2, 0, 0, 0, 2 }
                },
                {
                    ParameterId.Intonation,
                    new[] { 0, 0, 1, 0, 1, 3, 0, 0, 0, 2 }
                },
                {
                    ParameterId.PauseShort,
                    new[] { 0, 0, 1, 0, 3, 0, 0, 0, 0, 2 }
                },
                {
                    ParameterId.PauseLong,
                    new[] { 0, 0, 1, 0, 3, 1, 0, 0, 0, 2 }
                },
                {
                    ParameterId.PauseSentence,
                    new[] { 0, 0, 1, 0, 3, 2, 0, 0, 0, 2 }
                },

                // ボイス
                {
                    ParameterId.PresetVolume,
                    new[] { 2, 0, 1, 0, 1, 0, 0, 0, 0, 2 }
                },
                {
                    ParameterId.PresetSpeed,
                    new[] { 2, 0, 1, 0, 1, 1, 0, 0, 0, 2 }
                },
                {
                    ParameterId.PresetTone,
                    new[] { 2, 0, 1, 0, 1, 2, 0, 0, 0, 2 }
                },
                {
                    ParameterId.PresetIntonation,
                    new[] { 2, 0, 1, 0, 1, 3, 0, 0, 0, 2 }
                },
                {
                    ParameterId.PresetJoy,
                    new[] { 2, 0, 1, 0, 5, 0, 0, 0, 0, 0, 0, 0, 0, 2 }
                },
                {
                    ParameterId.PresetAnger,
                    new[] { 2, 0, 1, 0, 5, 0, 0, 1, 0, 0, 0, 0, 0, 2 }
                },
                {
                    ParameterId.PresetSorrow,
                    new[] { 2, 0, 1, 0, 5, 0, 0, 2, 0, 0, 0, 0, 0, 2 }
                },
            };
    }
}
