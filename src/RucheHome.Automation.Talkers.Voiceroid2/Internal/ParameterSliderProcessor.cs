using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using Codeer.Friendly.Dynamic;
using RucheHome.Automation.Friendly.Wpf;
using RucheHome.Automation.Talkers.Friendly;
using RucheHome.Diagnostics;

namespace RucheHome.Automation.Talkers.Voiceroid2.Internal
{
    /// <summary>
    /// <see cref="Talker.ProcessParameterSliders{T}"/> の実処理を行うクラス。
    /// </summary>
    internal sealed class ParameterSliderProcessor
    {
#if DEBUG
        /// <summary>
        /// 静的コンストラクタ。
        /// </summary>
        static ParameterSliderProcessor()
        {
            // GuiGroupInfos に全 GuiGroup 列挙値が含まれているか確認
            Debug.Assert(
                ((GuiGroup[])Enum.GetValues(typeof(GuiGroup)))
                    .All(g => GuiGroupInfos.ContainsKey(g)));

            // GuiGroupInfos に全 ParameterId 列挙値が1つずつ含まれているか確認
            Debug.Assert(
                Enumerable.SequenceEqual(
                    GuiGroupInfos.SelectMany(info => info.Value.Ids).OrderBy(id => id),
                    ParameterIdExtension.AllValues.OrderBy(id => id)));
        }
#endif // DEBUG

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        public ParameterSliderProcessor() { }

        /// <summary>
        /// パラメータを保持するスライダーに対する処理を行うデリゲート。
        /// </summary>
        /// <typeparam name="T">処理結果値の型。</typeparam>
        /// <param name="id">パラメータID。</param>
        /// <param name="slider">スライダー。</param>
        /// <returns>
        /// 処理結果値。
        /// Message は成功ならば null 、失敗ならばエラーメッセージとすること。
        /// </returns>
        public delegate Result<T> Executer<T>(ParameterId id, dynamic slider);

        /// <summary>
        /// <see cref="Talker.ProcessParameterSliders{T}"/> の実処理を行う。
        /// </summary>
        /// <typeparam name="T">処理結果値の型。</typeparam>
        /// <param name="mainWindow">メインウィンドウ。</param>
        /// <param name="visualTree">ビジュアルツリー走査用オブジェクト。</param>
        /// <param name="executer">
        /// 処理デリゲート。
        /// 戻り値の Message は成功ならば null 、失敗ならばエラーメッセージとすること。
        /// </param>
        /// <param name="targetIds">処理対象パラメータID列挙。 null ならばすべて対象。</param>
        /// <returns></returns>
        public Result<Dictionary<ParameterId, T>> Execute<T>(
            dynamic mainWindow,
            AppVisualTree visualTree,
            Executer<T> executer,
            IEnumerable<ParameterId> targetIds = null)
        {
            ArgumentValidation.IsNotNull(mainWindow, nameof(mainWindow));
            ArgumentValidation.IsNotNull(visualTree, nameof(visualTree));
            ArgumentValidation.IsNotNull(executer, nameof(executer));

            // タブコントロールを取得
            dynamic tabControl;
            try
            {
                tabControl = mainWindow.Content.Children[1].Children[2];
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
                return (null, @"本体のタブページが見つかりません。");
            }

            // タブアイテムコレクションを取得
            dynamic tabItems;
            try
            {
                tabItems = tabControl.Items;
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
                return (null, @"本体のタブページが見つかりません。");
            }

            var results = new Dictionary<ParameterId, T>();

            var targetInfos = MakeGuiGroupTargetInfos(targetIds);

            // マスタータブ
            var masterGroups = new[] { GuiGroup.MasterEffect, GuiGroup.MasterPause };
            if (masterGroups.Any(g => targetInfos[g].IdIndices.Any()))
            {
                var sliders = new Dictionary<ParameterId, dynamic>();

                try
                {
                    var panelsParent =
                        tabItems[0].Content.Content.Children[0].Content.Children;
                    foreach (var group in masterGroups)
                    {
                        AddLogicalSlidersTo(panelsParent, targetInfos[group], sliders);
                    }
                }
                catch (Exception ex)
                {
                    ThreadTrace.WriteException(ex);
                    return (null, @"本体のマスタータブのスライダーが見つかりませんでした。");
                }

                var message = ApplyExecuter(sliders, executer, results);
                if (message != null)
                {
                    return (null, message);
                }
            }

            // ボイスタブ
            var presetEffectTargetInfo = targetInfos[GuiGroup.PresetEffect];
            var presetEmotionTargetInfo = targetInfos[GuiGroup.PresetEmotion];
            if (
                presetEffectTargetInfo.IdIndices.Any() ||
                presetEmotionTargetInfo.IdIndices.Any())
            {
                var sliders = new Dictionary<ParameterId, dynamic>();
                int tabIndex = -1;

                try
                {
                    try
                    {
                        var panelsParent =
                            tabItems[1].Content.Content.Children[2].Content.Children;

                        // 音声効果
                        AddLogicalSlidersTo(panelsParent, presetEffectTargetInfo, sliders);

                        // 現在のタブインデックスを保存
                        tabIndex = (int)tabControl.SelectedIndex;

                        // 感情
                        AddPresetEmotionSlidersTo(
                            tabControl,
                            panelsParent,
                            visualTree,
                            presetEmotionTargetInfo,
                            sliders);
                    }
                    catch (Exception ex)
                    {
                        ThreadTrace.WriteException(ex);
                        return (
                            null,
                            @"本体のボイスタブのスライダーが見つかりませんでした。");
                    }

                    var message = ApplyExecuter(sliders, executer, results);
                    if (message != null)
                    {
                        return (null, message);
                    }
                }
                finally
                {
                    // 元のタブを選択する
                    // 失敗してもよい
                    if (tabIndex >= 0)
                    {
                        try
                        {
                            tabControl.SelectedIndex = tabIndex;
                        }
                        catch (Exception ex)
                        {
                            ThreadDebug.WriteException(ex);
                        }
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// パラメータの属するGUIグループを定義する列挙。
        /// </summary>
        private enum GuiGroup
        {
            /// <summary>
            /// マスター設定の音声効果関連。
            /// </summary>
            MasterEffect,

            /// <summary>
            /// マスター設定のポーズ関連。
            /// </summary>
            MasterPause,

            /// <summary>
            /// ボイスプリセット設定の音声効果関連。
            /// </summary>
            PresetEffect,

            /// <summary>
            /// ボイスプリセット設定の感情関連。
            /// </summary>
            PresetEmotion,
        }

        /// <summary>
        /// GUIグループに関する情報を保持するクラス。
        /// </summary>
        private class GuiGroupInfo
        {
            /// <summary>
            /// コンストラクタ。
            /// </summary>
            /// <param name="ids">対象パラメータID配列。</param>
            /// <param name="panelIndex">
            /// GUI操作時に参照するパネルコントロールのインデックス。
            /// </param>
            public GuiGroupInfo(ParameterId[] ids, int panelIndex)
            {
                Debug.Assert(ids != null && ids.Length > 0);
                Debug.Assert(panelIndex >= 0);

                this.Ids = Array.AsReadOnly(ids);
                this.PanelIndex = panelIndex;
            }

            /// <summary>
            /// 対象パラメータIDコレクションを取得する。
            /// </summary>
            public ReadOnlyCollection<ParameterId> Ids { get; }

            /// <summary>
            /// GUI操作時に参照するパネルコントロールのインデックスを取得する。
            /// </summary>
            public int PanelIndex { get; }
        }

        /// <summary>
        /// GUIグループ情報ディクショナリ。
        /// </summary>
        private static readonly Dictionary<GuiGroup, GuiGroupInfo> GuiGroupInfos =
            new Dictionary<GuiGroup, GuiGroupInfo>
            {
                {
                    GuiGroup.MasterEffect,
                    new GuiGroupInfo(
                        new[]
                        {
                            ParameterId.Volume,
                            ParameterId.Speed,
                            ParameterId.Tone,
                            ParameterId.Intonation,
                        },
                        1)
                },
                {
                    GuiGroup.MasterPause,
                    new GuiGroupInfo(
                        new[]
                        {
                            ParameterId.PauseShort,
                            ParameterId.PauseLong,
                            ParameterId.PauseSentence,
                        },
                        3)
                },
                {
                    GuiGroup.PresetEffect,
                    new GuiGroupInfo(
                        new[]
                        {
                            ParameterId.PresetVolume,
                            ParameterId.PresetSpeed,
                            ParameterId.PresetTone,
                            ParameterId.PresetIntonation,
                        },
                        1)
                },
                {
                    GuiGroup.PresetEmotion,
                    new GuiGroupInfo(
                        new[]
                        {
                            ParameterId.PresetJoy,
                            ParameterId.PresetAnger,
                            ParameterId.PresetSorrow,
                        },
                        5)
                }
            };

        /// <summary>
        /// GUIグループごとの処理対象情報を保持するクラス。
        /// </summary>
        private class GuiGroupTargetInfo
        {
            /// <summary>
            /// コンストラクタ。
            /// </summary>
            /// <param name="idIndices">
            /// パラメータIDとGUIグループ内インデックスの列挙。
            /// </param>
            /// <param name="panelIndex">
            /// GUI操作時に参照するパネルコントロールのインデックス。
            /// </param>
            public GuiGroupTargetInfo(
                IEnumerable<(ParameterId id, int index)> idIndices,
                int panelIndex)
            {
                Debug.Assert(idIndices != null);
                Debug.Assert(panelIndex >= 0);

                this.IdIndices = idIndices;
                this.PanelIndex = panelIndex;
            }

            /// <summary>
            /// パラメータIDとGUIグループ内インデックスの列挙を取得する。
            /// </summary>
            public IEnumerable<(ParameterId id, int index)> IdIndices { get; }

            /// <summary>
            /// GUI操作時に参照するパネルコントロールのインデックスを取得する。
            /// </summary>
            public int PanelIndex { get; }
        }

        /// <summary>
        /// GUIグループごとの処理対象情報ディクショナリを作成する。
        /// </summary>
        /// <param name="targetIds">処理対象パラメータID列挙。 null ならばすべて対象。</param>
        /// <returns>GUIグループごとの処理対象情報ディクショナリ。</returns>
        private static Dictionary<GuiGroup, GuiGroupTargetInfo> MakeGuiGroupTargetInfos(
            IEnumerable<ParameterId> targetIds)
        {
            var allIds = targetIds ?? ParameterIdExtension.AllValues;

            return
                GuiGroupInfos
                    .ToDictionary(
                        kv => kv.Key,
                        kv =>
                            new GuiGroupTargetInfo(
                                kv.Value.Ids
                                    .Select((id, index) => (id: id, index: index))
                                    .Where(v => allIds.Contains(v.id)),
                                kv.Value.PanelIndex));
        }

        /// <summary>
        /// ロジカルツリーに基づいてスライダー群を追加する。
        /// </summary>
        /// <param name="panelsParent">
        /// スライダー群の親となるパネルコントロール群の親コントロール。
        /// </param>
        /// <param name="targetInfo">処理対象情報。</param>
        /// <param name="idSliders">スライダー群の追加先。</param>
        private static void AddLogicalSlidersTo(
            dynamic panelsParent,
            GuiGroupTargetInfo targetInfo,
            IDictionary<ParameterId, dynamic> idSliders)
        {
            Debug.Assert(targetInfo != null);
            Debug.Assert(idSliders != null);

            if (targetInfo.IdIndices.Any())
            {
                var panelChildren = panelsParent[targetInfo.PanelIndex].Children;
                foreach (var (id, index) in targetInfo.IdIndices)
                {
                    idSliders.Add(id, panelChildren[index].Content.Children[2]);
                }
            }
        }

        /// <summary>
        /// ボイスプリセットの感情スライダー群を追加する。
        /// </summary>
        /// <param name="tabControl">
        /// ウィンドウ下部の操作タブコントロール。
        /// 選択中タブアイテムが変更される場合がある。
        /// </param>
        /// <param name="panelsParent">
        /// スライダー群の親となるパネルコントロール群の親コントロール。
        /// </param>
        /// <param name="visualTree">ビジュアルツリー走査用オブジェクト。</param>
        /// <param name="targetInfo">処理対象情報。</param>
        /// <param name="idSliders">パラメータIDとスライダーのペア群の追加先。</param>
        private static void AddPresetEmotionSlidersTo(
            dynamic tabControl,
            dynamic panelsParent,
            AppVisualTree visualTree,
            GuiGroupTargetInfo targetInfo,
            IDictionary<ParameterId, dynamic> idSliders)
        {
            Debug.Assert((DynamicAppVar)tabControl != null);
            Debug.Assert((DynamicAppVar)panelsParent != null);
            Debug.Assert(visualTree != null);
            Debug.Assert(targetInfo != null);
            Debug.Assert(idSliders != null);

            if (!targetInfo.IdIndices.Any())
            {
                return;
            }

            var baseListBox = panelsParent[targetInfo.PanelIndex];

            // 感情非対応のボイスプリセットでは非表示になっている
            if ((Visibility)baseListBox.Visibility != Visibility.Visible)
            {
                return;
            }

            // 直接 Items をいじるのが手っ取り早いのだが、
            // VOICEROID2定義の型を操作することになるのでやめておく。

            // ビジュアルツリー走査のためにボイスタブ選択
            tabControl.SelectedIndex = 1;

            // ListBoxItem 群の親を取得
            var itemsParent = visualTree.GetDescendant(baseListBox, 0, 0);

            foreach (var (id, index) in targetInfo.IdIndices)
            {
                var item = itemsParent.Children[index];
                var fader = visualTree.GetDescendant(item, 0, 0);
                var slider = fader.Content.Children[2];

                idSliders.Add(id, slider);
            }
        }

        /// <summary>
        /// スライダー群に対して処理デリゲートを適用する。
        /// </summary>
        /// <typeparam name="T">処理結果値の型。</typeparam>
        /// <param name="idSliders">パラメータIDとスライダーのペア列挙。</param>
        /// <param name="executer">処理デリゲート。</param>
        /// <param name="results">処理結果値の追加先。</param>
        /// <returns>すべて成功したならば null 。そうでなければエラーメッセージ。</returns>
        private static string ApplyExecuter<T>(
            IEnumerable<KeyValuePair<ParameterId, dynamic>> idSliders,
            Executer<T> executer,
            IDictionary<ParameterId, T> results)
        {
            Debug.Assert(idSliders != null);
            Debug.Assert(executer != null);
            Debug.Assert(results != null);

            foreach (var kv in idSliders)
            {
                var r = executer(kv.Key, (DynamicAppVar)kv.Value);
                if (r.Message != null)
                {
                    return r.Message;
                }

                results.Add(kv.Key, r.Value);
            }

            return null;
        }
    }
}
