using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using Codeer.Friendly;
using Codeer.Friendly.Dynamic;
using RM.Friendly.WPFStandardControls;
using RucheHome.Diagnostics;

namespace RucheHome.Talker.CeVIO.Internal.Controls
{
    /// <summary>
    /// パラメータスライダー群取得処理を提供するクラス。
    /// </summary>
    internal sealed class ParameterSliders
    {
        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="operationPanel">操作パネル取得用オブジェクト。</param>
        /// <param name="visualTreeHelperGetter">
        /// 操作対象アプリの VisualTreeHelper 型オブジェクト取得デリゲート。
        /// </param>
        public ParameterSliders(
            OperationPanel operationPanel,
            Func<dynamic> visualTreeHelperGetter)
        {
            this.OperationPanel =
                operationPanel ?? throw new ArgumentNullException(nameof(operationPanel));
            this.VisualTreeHelperGetter =
                visualTreeHelperGetter ??
                throw new ArgumentNullException(nameof(visualTreeHelperGetter));
        }

        /// <summary>
        /// パラメータスライダー群を取得する。
        /// </summary>
        /// <param name="targetParameterIds">
        /// 取得対象パラメータID列挙。 null ならばすべて対象。
        /// </param>
        /// <param name="operationPanel">
        /// 操作パネル。 null ならばメソッド内で取得される。
        /// </param>
        /// <returns>
        /// スライダーのディクショナリ。見つからないか取得できない状態ならば null 。
        /// </returns>
        public Result<Dictionary<ParameterId, WPFSlider>> Get(
            IEnumerable<ParameterId> targetParameterIds = null,
            AppVar operationPanel = null)
        {
            // 操作パネルを取得
            var opePanel = operationPanel;
            if (opePanel == null)
            {
                var ov = this.OperationPanel.Get();
                if (ov.Value == null)
                {
                    return (null, ov.Message);
                }
                opePanel = ov.Value;
            }

            // VisualTreeHelper 型オブジェクトを取得
            dynamic vtree = null;
            try
            {
                vtree =
                    this.VisualTreeHelperGetter() ??
                    opePanel.App?.Type(typeof(VisualTreeHelper));
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
                vtree = null;
            }
            if (vtree == null)
            {
                return (null, @"本体の情報を取得できません。");
            }

            var dict = new Dictionary<ParameterId, WPFSlider>();

            var effectIdIndices =
                (targetParameterIds == null) ?
                    AllEffectIdIndices :
                    AllEffectIdIndices.Where(v => targetParameterIds.Contains(v.id));
            var emotionIds =
                (targetParameterIds ?? ParameterIdExtension.AllValues)
                    .Where(id => id.IsEmotion());

            if (effectIdIndices.Any() || emotionIds.Any())
            {
                try
                {
                    var panelChildren =
                        opePanel.Dynamic()
                            .Children[1]    // Border
                            .Child          // DockPanel
                            .Children[0]    // StackPanel
                            .Children;

                    // 音声効果
                    if (effectIdIndices.Any())
                    {
                        var border = vtree.GetChild(panelChildren[0], 0);
                        var panel = vtree.GetChild(border.Child, 0);

                        foreach (var ii in effectIdIndices)
                        {
                            var sliderPanel = vtree.GetChild(panel.Children[ii.index], 0);
                            dict.Add(ii.id, new WPFSlider(sliderPanel.Children[1]));
                        }
                    }

                    // 感情
                    if (emotionIds.Any())
                    {
                        var border = vtree.GetChild(panelChildren[2], 0);
                        var panel = vtree.GetChild(border.Child, 0);

                        foreach (var c in panel.Children)
                        {
                            var sliderPanel = vtree.GetChild(c, 0);

                            // 感情名からパラメータID検索
                            var name = (string)sliderPanel.Children[0].Child.Text;
                            var id = ParameterIdExtension.FindEmotionByDisplayName(name);

                            if (id.HasValue && emotionIds.Contains(id.Value))
                            {
                                dict.Add(id.Value, new WPFSlider(sliderPanel.Children[1]));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ThreadTrace.WriteException(ex);
                    return (null, @"本体のパラメータスライダー群が見つかりません。");
                }
            }

            return dict;
        }

        /// <summary>
        /// 全音声効果パラメータIDとインデックスの配列。
        /// </summary>
        private static readonly (ParameterId id, int index)[] AllEffectIdIndices =
            ParameterIdExtension.AllValues
                .Where(id => id.IsEffect())
                .Select((id, index) => (id, index))
                .ToArray();

        /// <summary>
        /// 操作パネル取得用オブジェクトを取得する。
        /// </summary>
        private OperationPanel OperationPanel { get; }

        /// <summary>
        /// 操作対象アプリの VisualTreeHelper 型オブジェクト取得デリゲートを取得する。
        /// </summary>
        private Func<dynamic> VisualTreeHelperGetter { get; }
    }
}
