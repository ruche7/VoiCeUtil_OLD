using System;
using System.Collections.Generic;
using System.Linq;
using RucheHome.Automation.Friendly.Wpf;
using RucheHome.Diagnostics;

namespace RucheHome.Automation.Talkers.CeVIO.Internal.Controls
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
        /// <param name="appVisualTreeGetter">
        /// ビジュアルツリー走査用オブジェクト取得デリゲート。
        /// </param>
        public ParameterSliders(
            OperationPanel operationPanel,
            Func<AppVisualTree> appVisualTreeGetter)
        {
            this.OperationPanel =
                operationPanel ?? throw new ArgumentNullException(nameof(operationPanel));
            this.AppVisualTreeGetter =
                appVisualTreeGetter ??
                throw new ArgumentNullException(nameof(appVisualTreeGetter));
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
        /// <param name="appVisualTree">
        /// ビジュアルツリー走査用オブジェクト。 null ならばメソッド内で取得される。
        /// </param>
        /// <returns>
        /// スライダーのディクショナリ。見つからないか取得できない状態ならば null 。
        /// </returns>
        public Result<Dictionary<ParameterId, dynamic>> Get(
            IEnumerable<ParameterId> targetParameterIds = null,
            dynamic operationPanel = null,
            AppVisualTree appVisualTree = null)
        {
            var dict = new Dictionary<ParameterId, dynamic>();

            var effectIdIndices =
                (targetParameterIds == null) ?
                    AllEffectIdIndices :
                    AllEffectIdIndices.Where(v => targetParameterIds.Contains(v.id));
            var emotionIds =
                (targetParameterIds ?? ParameterIdExtension.AllValues)
                    .Where(id => id.IsEmotion());

            if (effectIdIndices.Any() || emotionIds.Any())
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

                // ビジュアルツリー走査用オブジェクトを取得
                var vtree = appVisualTree ?? this.AppVisualTreeGetter();
                if (vtree == null)
                {
                    return (null, @"本体の情報を取得できません。");
                }

                try
                {
                    var panelChildren =
                        opePanel
                            .Children[1]    // Border
                            .Child          // DockPanel
                            .Children[0]    // StackPanel
                            .Children;

                    // 音声効果
                    if (effectIdIndices.Any())
                    {
                        var border = vtree.GetDescendant(panelChildren[0], 0);
                        var panel = vtree.GetDescendant(border.Child, 0);

                        foreach (var ii in effectIdIndices)
                        {
                            var sliderPanel =
                                vtree.GetDescendant(panel.Children[ii.index], 0);
                            dict.Add(ii.id, sliderPanel.Children[1]);
                        }
                    }

                    // 感情
                    if (emotionIds.Any())
                    {
                        var border = vtree.GetDescendant(panelChildren[2], 0);
                        var panel = vtree.GetDescendant(border.Child, 0);

                        foreach (var c in panel.Children)
                        {
                            var sliderPanel = vtree.GetDescendant(c, 0);

                            // 感情名からパラメータID検索
                            var name = (string)sliderPanel.Children[0].Child.Text;
                            var id = ParameterIdExtension.FindEmotionByDisplayName(name);

                            if (id.HasValue && emotionIds.Contains(id.Value))
                            {
                                dict.Add(id.Value, sliderPanel.Children[1]);
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
        /// ビジュアルツリー走査用オブジェクト取得デリゲートを取得する。
        /// </summary>
        private Func<AppVisualTree> AppVisualTreeGetter { get; }
    }
}
