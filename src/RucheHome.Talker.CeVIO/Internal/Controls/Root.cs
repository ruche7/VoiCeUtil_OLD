using System;
using System.Windows;
using System.Windows.Media;
using Codeer.Friendly;
using Codeer.Friendly.Dynamic;
using RucheHome.Diagnostics;

namespace RucheHome.Talker.CeVIO.Internal.Controls
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
        /// <param name="visualTreeHelperGetter">
        /// 操作対象アプリの VisualTreeHelper 型オブジェクト取得デリゲート。
        /// </param>
        /// <param name="canChangeTrackGetter">トラック選択変更可否取得デリゲート。</param>
        public Root(
            Func<AppVar> mainWindowGetter,
            Func<dynamic> visualTreeHelperGetter,
            Func<bool> canChangeTrackGetter)
        {
            this.MainWindowGetter =
                mainWindowGetter ??
                throw new ArgumentNullException(nameof(mainWindowGetter));
            this.VisualTreeHelperGetter =
                visualTreeHelperGetter ??
                throw new ArgumentNullException(nameof(visualTreeHelperGetter));

            this.TrackSelector = new TrackSelector(this);
            this.ControlPanel =
                new ControlPanel(this, visualTreeHelperGetter, canChangeTrackGetter);
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
        /// <returns>コントロール。見つからないか非表示ならば null 。</returns>
        public Result<AppVar> Get(AppVar mainWindow = null)
        {
            // メインウィンドウを取得
            var mainWin = mainWindow ?? this.MainWindowGetter();
            if (mainWin == null)
            {
                return (null, @"本体のウィンドウが見つかりません。");
            }

            // VisualTreeHelper 型オブジェクトを取得
            dynamic vtree = null;
            try
            {
                vtree =
                    this.VisualTreeHelperGetter() ??
                    mainWin.App?.Type(typeof(VisualTreeHelper));
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

            try
            {
                var tabControl = mainWin.Dynamic().Content.Children[0].Children[1];
                var sceneParent = vtree.GetChild(tabControl, 0).Children[1].Child;
                var rootParent = vtree.GetChild(sceneParent, 0).Content.Children[0];

                // コンパクト表示中はルートの親が非表示になっている
                if ((Visibility)rootParent.Visibility != Visibility.Visible)
                {
                    return (null, @"本体がコンパクト表示中です。");
                }

                return (AppVar)rootParent.Children[0];
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
        private Func<AppVar> MainWindowGetter { get; }

        /// <summary>
        /// 操作対象アプリの VisualTreeHelper 型オブジェクト取得デリゲートを取得する。
        /// </summary>
        private Func<dynamic> VisualTreeHelperGetter { get; }
    }
}
