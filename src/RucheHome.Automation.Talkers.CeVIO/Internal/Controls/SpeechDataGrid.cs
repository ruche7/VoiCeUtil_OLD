using System;
using RucheHome.Automation.Friendly.Wpf;
using RucheHome.Diagnostics;

namespace RucheHome.Automation.Talkers.CeVIO.Internal.Controls
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
        /// <param name="appVisualTreeGetter">
        /// ビジュアルツリー走査用オブジェクト取得デリゲート。
        /// </param>
        public SpeechDataGrid(
            ControlPanel controlPanel,
            Func<AppVisualTree> appVisualTreeGetter)
        {
            this.ControlPanel =
                controlPanel ?? throw new ArgumentNullException(nameof(controlPanel));
            this.AppVisualTreeGetter =
                appVisualTreeGetter ??
                throw new ArgumentNullException(nameof(appVisualTreeGetter));
        }

        /// <summary>
        /// セリフデータグリッドを取得する。
        /// </summary>
        /// <param name="controlPanel">
        /// コントロールパネル。 null ならばメソッド内で取得される。
        /// </param>
        /// <param name="appVisualTree">
        /// ビジュアルツリー走査用オブジェクト。 null ならばメソッド内で取得される。
        /// </param>
        /// <returns>コントロール。見つからないか取得できない状態ならば null 。</returns>
        public Result<AppDataGrid> Get(
            dynamic controlPanel = null,
            AppVisualTree appVisualTree = null)
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

            // ビジュアルツリー走査用オブジェクトを取得
            var vtree = appVisualTree ?? this.AppVisualTreeGetter();
            if (vtree == null)
            {
                return (null, @"本体の情報を取得できません。");
            }

            try
            {
                var dataGrid =
                    new AppDataGrid(ctrlPanel.Children[0].Content.Children[0], vtree);

                // コンテキストメニューを初期化して取得
                var menu = InitializeContextMenu(dataGrid);
                if (menu == null)
                {
                    return (null, @"本体のセリフ一覧表を初期化できません。");
                }

                // キャスト列を表示させる
                var castItem = FindCastMenuItem(menu);
                if (castItem == null)
                {
                    return (null, @"本体のセリフ一覧表を初期化できません。");
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
        /// セリフデータグリッドのコンテキストメニューを初期化して取得する。
        /// </summary>
        /// <param name="dataGrid">セリフデータグリッド。</param>
        /// <returns>コンテキストメニュー。</returns>
        private static dynamic InitializeContextMenu(AppDataGrid dataGrid)
        {
            var menu = dataGrid.Control.ContextMenu;
            if (menu == null)
            {
                return null;
            }

            foreach (var item in menu.Items)
            {
                // メニュー項目名取得
                // Separator の場合があるので例外は握り潰す
                string header;
                try
                {
                    header = (string)item.Header;
                }
                catch
                {
                    continue;
                }

                // メニュー項目に null があるなら要初期化
                if (header == null)
                {
                    // 初期化
                    try
                    {
                        menu.PlacementTarget = dataGrid.Control;
                        try
                        {
                            menu.IsOpen = true;
                        }
                        finally
                        {
                            menu.IsOpen = false;
                        }
                    }
                    finally
                    {
                        menu.PlacementTarget = null;
                    }

                    // 非 null になったか確認
                    if ((string)item.Header == null)
                    {
                        return null;
                    }

                    break;
                }
            }

            return menu;
        }

        /// <summary>
        /// セリフデータグリッドのコンテキストメニューからキャスト表示切替項目を検索する。
        /// </summary>
        /// <param name="contextMenu">コンテキストメニュー。</param>
        /// <returns>項目。見つからなければ null 。</returns>
        /// <remarks>
        /// コンテキストメニュー未初期化の場合は取得に失敗する。
        /// </remarks>
        private static dynamic FindCastMenuItem(dynamic contextMenu)
        {
            if (contextMenu == null)
            {
                return null;
            }

            var items = contextMenu.Items[10].Items;

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
            // Separator の場合があるので例外は握り潰す
            try
            {
                return
                    (bool)menuItem.IsCheckable &&
                    ((string)menuItem.Header == @"キャスト");
            }
            catch { }
            return false;
        }

        /// <summary>
        /// コントロールパネル取得用オブジェクトを取得する。
        /// </summary>
        private ControlPanel ControlPanel { get; }

        /// <summary>
        /// ビジュアルツリー走査用オブジェクト取得デリゲートを取得する。
        /// </summary>
        private Func<AppVisualTree> AppVisualTreeGetter { get; }
    }
}
