﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;

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
#if DEBUG
        /// <summary>
        /// 静的コンストラクタ。
        /// </summary>
        static ParameterIdExtension()
        {
            // Infos, ZOrderIndices に全列挙値が含まれているか確認
            Debug.Assert(
                AllIds.All(p => Infos.ContainsKey(p) && ZOrderIndices.ContainsKey(p)));
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
        /// 音声効果設定であるか否かを取得する。
        /// </summary>
        /// <param name="self">パラメータID。</param>
        /// <returns>音声効果設定ならば true 。そうでなければ false 。</returns>
        public static bool IsEffect(this ParameterId self) =>
            (self >= ParameterId.Volume && self <= ParameterId.Intonation);

        /// <summary>
        /// ポーズ設定であるか否かを取得する。
        /// </summary>
        /// <param name="self">パラメータID。</param>
        /// <returns>ポーズ設定ならば true 。そうでなければ false 。</returns>
        public static bool IsPause(this ParameterId self) =>
            (self >= ParameterId.PauseShort && self <= ParameterId.PauseEnd);

        /// <summary>
        /// タブページを基準とするテキストボックスのZオーダーインデックス配列を取得する。
        /// </summary>
        /// <param name="self">パラメータID。</param>
        /// <returns>Zオーダーインデックス配列。引数値が無効ならば null 。</returns>
        internal static int[] GetZOrderIndices(this ParameterId self) =>
            ZOrderIndices.TryGetValue(self, out var indices) ? (int[])indices.Clone() : null;

        /// <summary>
        /// タブページ名を取得する。
        /// </summary>
        /// <param name="self">パラメータID。</param>
        /// <returns>タブページ名。引数値が無効ならば null 。</returns>
        internal static string GetTabPageName(this ParameterId self) =>
            self.IsEffect() ? @"音声効果" : (self.IsPause() ? @"ポーズ" : null);

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

        /// <summary>
        /// タブページを基準とするテキストボックスのZオーダーインデックス配列ディクショナリ。
        /// </summary>
        private static readonly Dictionary<ParameterId, int[]> ZOrderIndices =
            new Dictionary<ParameterId, int[]>
            {
                // 音声効果
                { ParameterId.Volume, new[] { 0, 8 } },
                { ParameterId.Speed, new[] { 0, 9 } },
                { ParameterId.Tone, new[] { 0, 10 } },
                { ParameterId.Intonation, new[] { 0, 11 } },

                // ポーズ
                { ParameterId.PauseShort, new[] { 0, 3, 0 } },
                { ParameterId.PauseLong, new[] { 0, 5, 0 } },
                { ParameterId.PauseSentence, new[] { 0, 1, 0 } },
                { ParameterId.PauseBegin, new[] { 0, 7, 0 } },
                { ParameterId.PauseEnd, new[] { 0, 8, 0 } },
            };
    }
}
