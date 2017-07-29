using System;
using RucheHome.Automation.Friendly.Wpf;
using RucheHome.Diagnostics;

namespace RucheHome.Automation.Talkers.CeVIO.Internal.Controls
{
    /// <summary>
    /// トーク用コントロールパネル右側の操作パネル取得処理を提供するクラス。
    /// </summary>
    internal sealed class OperationPanel
    {
        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="controlPanel">コントロールパネル取得用オブジェクト。</param>
        /// <param name="appVisualTreeGetter">
        /// ビジュアルツリー走査用オブジェクト取得デリゲート。
        /// </param>
        public OperationPanel(
            ControlPanel controlPanel,
            Func<AppVisualTree> appVisualTreeGetter)
        {
            this.ControlPanel =
                controlPanel ?? throw new ArgumentNullException(nameof(controlPanel));

            this.PlayStopToggle = new PlayStopToggle(this);
            this.AutoPlayToggle = new AutoPlayToggle(this);
            this.ParameterSliders = new ParameterSliders(this, appVisualTreeGetter);
        }

        /// <summary>
        /// 試聴/停止トグルボタン取得用オブジェクトを取得する。
        /// </summary>
        public PlayStopToggle PlayStopToggle { get; }

        /// <summary>
        /// 自動試聴トグルボタン取得用オブジェクトを取得する。
        /// </summary>
        public AutoPlayToggle AutoPlayToggle { get; }

        /// <summary>
        /// パラメータスライダー群取得用オブジェクトを取得する。
        /// </summary>
        public ParameterSliders ParameterSliders { get; }

        /// <summary>
        /// 操作パネルを取得する。
        /// </summary>
        /// <param name="controlPanel">
        /// コントロールパネル。 null ならばメソッド内で取得される。
        /// </param>
        /// <returns>コントロール。見つからないか取得できない状態ならば null 。</returns>
        public Result<dynamic> Get(dynamic controlPanel = null)
        {
            // コントロールパネルを取得
            var ctrlPanel = controlPanel;
            if (ctrlPanel == null)
            {
                var cv = this.ControlPanel.GetTalk();
                if (cv.Value == null)
                {
                    return (null, cv.Message);
                }
                ctrlPanel = cv.Value;
            }

            try
            {
                return
                    ctrlPanel
                        .Children[2]    // VoiceEditor
                        .Content        // ScrollViewer
                        .Content;       // Grid
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
            }
            return (null, @"本体の操作画面が見つかりません。");
        }

        /// <summary>
        /// コントロールパネル取得用オブジェクトを取得する。
        /// </summary>
        private ControlPanel ControlPanel { get; }
    }
}
