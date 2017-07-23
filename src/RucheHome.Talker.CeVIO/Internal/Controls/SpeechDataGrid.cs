using System;
using Codeer.Friendly;
using Codeer.Friendly.Dynamic;
using RM.Friendly.WPFStandardControls;
using RucheHome.Diagnostics;

namespace RucheHome.Talker.CeVIO.Internal.Controls
{
    /// <summary>
    /// トーク用コントロールパネル左側のセリフデータグリッド取得処理を提供するクラス。
    /// </summary>
    internal sealed class SpeechDataGrid
    {
        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="controlPanel">コントロールパネル取得用オブジェクト。</param>
        public SpeechDataGrid(ControlPanel controlPanel)
        {
            this.ControlPanel =
                controlPanel ?? throw new ArgumentNullException(nameof(controlPanel));
        }

        /// <summary>
        /// セリフデータグリッドを取得する。
        /// </summary>
        /// <param name="controlPanel">
        /// コントロールパネル。 null ならばメソッド内で取得される。
        /// </param>
        /// <returns>コントロール。見つからないか取得できない状態ならば null 。</returns>
        public Result<WPFDataGrid> Get(AppVar controlPanel = null)
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
                var dataGrid =
                    new WPFDataGrid(ctrlPanel.Dynamic().Children[0].Content.Children[0]);

                // キャスト列を表示状態にする
                var castItem = FindCastMenuItem(dataGrid);
                if (castItem == null)
                {
                    return (null, @"本体のセリフ一覧表が不正な状態です。");
                }
                castItem.IsChecked = true;

                return dataGrid;
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
            }
            return (null, @"本体のセリフ一覧表が見つかりません。");
        }

        /// <summary>
        /// データグリッドのコンテキストメニューからキャスト表示切替項目を検索する。
        /// </summary>
        /// <param name="dataGrid">データグリッド。</param>
        /// <returns>項目。見つからなければ null 。</returns>
        private static dynamic FindCastMenuItem(WPFDataGrid dataGrid)
        {
            var items = dataGrid.Dynamic().ContextMenu.Items[10].Items;

            // まず決め打ち
            var castItem = items[3];
            if (IsCastMenuItem(castItem))
            {
                return castItem;
            }

            // 検索
            foreach (var item in items)
            {
                if (IsCastMenuItem(item))
                {
                    return item;
                }
            }

            return null;
        }

        /// <summary>
        /// 指定したメニューアイテムがキャスト表示切替項目であるか否かを取得する。
        /// </summary>
        /// <param name="menuItem">メニューアイテム。</param>
        /// <returns>
        /// キャスト表示切替項目ならば true 。そうでなければ false 。
        /// </returns>
        private static bool IsCastMenuItem(dynamic menuItem)
        {
            // セパレータの場合があるので例外は握り潰す
            try
            {
                return
                    (bool)menuItem.IsCheckable &&
                    (bool)menuItem.Header.StartsWith(@"キャスト");
            }
            catch { }
            return false;
        }

        /// <summary>
        /// コントロールパネル取得用オブジェクトを取得する。
        /// </summary>
        private ControlPanel ControlPanel { get; }
    }
}
