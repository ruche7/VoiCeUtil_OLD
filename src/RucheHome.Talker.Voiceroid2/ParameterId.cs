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
            // Infos, GuiGroups に全列挙値が含まれているか確認
            Debug.Assert(AllIds.All(p => Infos.ContainsKey(p) && GuiGroups.ContainsKey(p)));

            // 全列挙値がマスター設定またはボイスプリセット設定であることを確認
            Debug.Assert(AllIds.All(p => p.IsMaster() || p.IsPreset()));
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
        public static bool IsMaster(this ParameterId self)
        {
            var (group, _) = self.GetGuiGroup();
            return (group == GuiGroup.MasterSound || group == GuiGroup.MasterPause);
        }

        /// <summary>
        /// ボイスプリセット設定であるか否かを取得する。
        /// </summary>
        /// <param name="self">パラメータID。</param>
        /// <returns>ボイスプリセット設定ならば true 。そうでなければ false 。</returns>
        public static bool IsPreset(this ParameterId self)
        {
            var (group, _) = self.GetGuiGroup();
            return (group == GuiGroup.PresetSound || group == GuiGroup.PresetEmotion);
        }

        /// <summary>
        /// 存在しない可能性のある設定であるか否かを取得する。
        /// </summary>
        /// <param name="self">パラメータID。</param>
        /// <returns>存在しない可能性があるならば true 。そうでなければ false 。</returns>
        public static bool IsOptional(this ParameterId self) =>
            (self >= ParameterId.PresetJoy && self <= ParameterId.PresetSorrow);

        /// <summary>
        /// パラメータの属するGUIグループを定義する列挙。
        /// </summary>
        internal enum GuiGroup
        {
            /// <summary>
            /// 不明。
            /// </summary>
            Unknown = -1,

            /// <summary>
            /// マスター設定の音量、話速、高さ、抑揚。
            /// </summary>
            MasterSound = 0,

            /// <summary>
            /// マスター設定のポーズ関連。
            /// </summary>
            MasterPause,

            /// <summary>
            /// ボイスプリセット設定の音量、話速、高さ、抑揚。
            /// </summary>
            PresetSound,

            /// <summary>
            /// ボイスプリセット設定の感情関連。
            /// </summary>
            PresetEmotion,
        }

        /// <summary>
        /// パラメータの属するGUIグループとグループ内インデックスを取得する。
        /// </summary>
        /// <param name="self">パラメータID。</param>
        /// <returns>
        /// GUIグループとグループ内インデックス。
        /// 引数値が不正ならば (<see cref="GuiGroup.Unknown"/>, -1) 。
        /// </returns>
        internal static (GuiGroup group, int index) GetGuiGroup(this ParameterId self) =>
            GuiGroups.TryGetValue(self, out var g) ? g : (GuiGroup.Unknown, -1);

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
        /// GUIグループとグループ内インデックスのディクショナリ。
        /// </summary>
        private static readonly Dictionary<ParameterId, (GuiGroup group, int index)>
        GuiGroups =
            new Dictionary<ParameterId, (GuiGroup group, int index)>
            {
                // マスター : 音量～抑揚
                { ParameterId.Volume, (GuiGroup.MasterSound, 0) },
                { ParameterId.Speed, (GuiGroup.MasterSound, 1) },
                { ParameterId.Tone, (GuiGroup.MasterSound, 2) },
                { ParameterId.Intonation, (GuiGroup.MasterSound, 3) },

                // マスター : ポーズ関連
                { ParameterId.PauseShort, (GuiGroup.MasterPause, 0) },
                { ParameterId.PauseLong, (GuiGroup.MasterPause, 1) },
                { ParameterId.PauseSentence, (GuiGroup.MasterPause, 2) },

                // ボイスプリセット : 音量～抑揚
                { ParameterId.PresetVolume, (GuiGroup.PresetSound, 0) },
                { ParameterId.PresetSpeed, (GuiGroup.PresetSound, 1) },
                { ParameterId.PresetTone, (GuiGroup.PresetSound, 2) },
                { ParameterId.PresetIntonation, (GuiGroup.PresetSound, 3) },

                // ボイスプリセット : 感情関連
                { ParameterId.PresetJoy, (GuiGroup.PresetEmotion, 0) },
                { ParameterId.PresetAnger, (GuiGroup.PresetEmotion, 1) },
                { ParameterId.PresetSorrow, (GuiGroup.PresetEmotion, 2) },
            };
    }
}
