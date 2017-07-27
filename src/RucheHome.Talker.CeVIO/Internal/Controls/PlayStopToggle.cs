using System;
using Codeer.Friendly;
using Codeer.Friendly.Dynamic;
using RM.Friendly.WPFStandardControls;
using RucheHome.Diagnostics;

namespace RucheHome.Talker.CeVIO.Internal.Controls
{
    /// <summary>
    /// 試聴/停止トグルボタン取得処理を提供するクラス。
    /// </summary>
    internal sealed class PlayStopToggle
    {
        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="operationPanel">操作パネル取得用オブジェクト。</param>
        public PlayStopToggle(OperationPanel operationPanel)
        {
            this.OperationPanel =
                operationPanel ?? throw new ArgumentNullException(nameof(operationPanel));
        }

        /// <summary>
        /// 試聴/停止トグルボタンを取得する。
        /// </summary>
        /// <param name="operationPanel">
        /// 操作パネル。 null ならばメソッド内で取得される。
        /// </param>
        /// <returns>コントロール。見つからないか取得できない状態ならば null 。</returns>
        public Result<WPFToggleButton> Get(AppVar operationPanel = null)
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

            try
            {
                return new WPFToggleButton(opePanel.Dynamic().Children[0].Children[0]);
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
            }
            return (null, @"本体の再生切替ボタンが見つかりません。");
        }

        /// <summary>
        /// 操作パネル取得用オブジェクトを取得する。
        /// </summary>
        private OperationPanel OperationPanel { get; }
    }
}
