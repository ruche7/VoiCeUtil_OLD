using System;
using Codeer.Friendly;
using Codeer.Friendly.Dynamic;
using RucheHome.Diagnostics;

namespace RucheHome.Talker.CeVIO.Internal.Controls
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
        public OperationPanel(ControlPanel controlPanel)
        {
            this.ControlPanel =
                controlPanel ?? throw new ArgumentNullException(nameof(controlPanel));

            this.PlayStopToggle = new PlayStopToggle(this);
        }

        /// <summary>
        /// 再生/停止トグルボタン取得用オブジェクトを取得する。
        /// </summary>
        public PlayStopToggle PlayStopToggle { get; }

        /// <summary>
        /// 操作パネルを取得する。
        /// </summary>
        /// <param name="controlPanel">
        /// コントロールパネル。 null ならばメソッド内で取得される。
        /// </param>
        /// <returns>コントロール。見つからないか取得できない状態ならば null 。</returns>
        public Result<AppVar> Get(AppVar controlPanel = null)
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
                return (AppVar)ctrlPanel.Dynamic().Children[2].Content.Content;
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
