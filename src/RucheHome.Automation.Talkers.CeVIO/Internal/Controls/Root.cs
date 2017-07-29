using System;
using System.Windows;
using Codeer.Friendly.Dynamic;
using RucheHome.Automation.Friendly.Wpf;
using RucheHome.Diagnostics;

namespace RucheHome.Automation.Talkers.CeVIO.Internal.Controls
{
    /// <summary>
    /// ルートコントロール取得処理を提供するクラス。
    /// </summary>
    internal sealed class Root
    {
        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="mainWindowGetter">メインウィンドウ取得デリゲート。</param>
        /// <param name="appVisualTreeGetter">
        /// ビジュアルツリー走査用オブジェクト取得デリゲート。
        /// </param>
        /// <param name="canChangeTrackGetter">トラック選択変更可否取得デリゲート。</param>
        public Root(
            Func<dynamic> mainWindowGetter,
            Func<AppVisualTree> appVisualTreeGetter,
            Func<bool> canChangeTrackGetter)
        {
            this.MainWindowGetter =
                mainWindowGetter ??
                throw new ArgumentNullException(nameof(mainWindowGetter));
            this.AppVisualTreeGetter =
                appVisualTreeGetter ??
                throw new ArgumentNullException(nameof(appVisualTreeGetter));

            this.TrackSelector = new TrackSelector(this);
            this.ControlPanel =
                new ControlPanel(this, appVisualTreeGetter, canChangeTrackGetter);
        }

        /// <summary>
        /// ウィンドウ上部のトラックセレクタ取得用オブジェクトを取得する。
        /// </summary>
        public TrackSelector TrackSelector { get; }

        /// <summary>
        /// ウィンドウ下部のコントロールパネル取得用オブジェクトを取得する。
        /// </summary>
        public ControlPanel ControlPanel { get; }

        /// <summary>
        /// ルートコントロールを取得する。
        /// </summary>
        /// <param name="mainWindow">
        /// メインウィンドウ。 null ならばメソッド内で取得される。
        /// </param>
        /// <param name="appVisualTree">
        /// ビジュアルツリー走査用オブジェクト。 null ならばメソッド内で取得される。
        /// </param>
        /// <returns>コントロール。見つからないか非表示ならば null 。</returns>
        public Result<dynamic> Get(
            dynamic mainWindow = null,
            AppVisualTree appVisualTree = null) =>
            this.Get(out var _, (DynamicAppVar)mainWindow, appVisualTree);

        /// <summary>
        /// ルートコントロールを取得する。
        /// </summary>
        /// <param name="compacted">コンパクト表示中であるか否かの設定先。</param>
        /// <param name="mainWindow">
        /// メインウィンドウ。 null ならばメソッド内で取得される。
        /// </param>
        /// <param name="appVisualTree">
        /// ビジュアルツリー走査用オブジェクト。 null ならばメソッド内で取得される。
        /// </param>
        /// <returns>コントロール。見つからないか非表示ならば null 。</returns>
        public Result<dynamic> Get(
            out bool compacted,
            dynamic mainWindow = null,
            AppVisualTree appVisualTree = null)
        {
            compacted = false;

            // メインウィンドウを取得
            var mainWin = mainWindow ?? this.MainWindowGetter();
            if (mainWin == null)
            {
                return (null, @"本体のウィンドウが見つかりません。");
            }

            // ビジュアルツリー走査用オブジェクトを取得
            var vtree = appVisualTree ?? this.AppVisualTreeGetter();
            if (vtree == null)
            {
                return (null, @"本体の情報を取得できません。");
            }

            try
            {
                var tabControl = mainWin.Content.Children[0].Children[1];
                var sceneParent = vtree.GetDescendant(tabControl, 0).Children[1].Child;
                var rootParent = vtree.GetDescendant(sceneParent, 0).Content.Children[0];

                // コンパクト表示中はルートの親が非表示になっている
                if ((Visibility)rootParent.Visibility != Visibility.Visible)
                {
                    compacted = true;
                    return (null, @"本体がコンパクト表示中です。");
                }

                return (rootParent.Children[0], null);
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
            }
            return (null, @"本体のベース画面が見つかりませんでした。");
        }

        /// <summary>
        /// メインウィンドウ取得デリゲートを取得する。
        /// </summary>
        private Func<dynamic> MainWindowGetter { get; }

        /// <summary>
        /// ビジュアルツリー走査用オブジェクト取得デリゲートを取得する。
        /// </summary>
        private Func<AppVisualTree> AppVisualTreeGetter { get; }
    }
}
