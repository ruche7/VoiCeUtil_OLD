using System;
using Codeer.Friendly.Dynamic;
using RucheHome.Automation.Friendly.Wpf;
using RucheHome.Diagnostics;

namespace RucheHome.Automation.Talkers.CeVIO.Internal.Controls
{
    /// <summary>
    /// ウィンドウ下部のコントロールパネル取得処理を提供するクラス。
    /// </summary>
    internal sealed class ControlPanel
    {
        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="root">ルートコントロール取得用オブジェクト。</param>
        /// <param name="appVisualTreeGetter">
        /// ビジュアルツリー走査用オブジェクト取得デリゲート。
        /// </param>
        /// <param name="canChangeTrackGetter">トラック選択変更可否取得デリゲート。</param>
        public ControlPanel(
            Root root,
            Func<AppVisualTree> appVisualTreeGetter,
            Func<bool> canChangeTrackGetter)
        {
            this.Root = root ?? throw new ArgumentNullException(nameof(root));
            this.AppVisualTreeGetter =
                appVisualTreeGetter ??
                throw new ArgumentNullException(nameof(appVisualTreeGetter));
            this.CanChangeTrackGetter =
                canChangeTrackGetter ??
                throw new ArgumentNullException(nameof(canChangeTrackGetter));

            this.SpeechDataGrid = new SpeechDataGrid(this, appVisualTreeGetter);
            this.OperationPanel = new OperationPanel(this, appVisualTreeGetter);
        }

        /// <summary>
        /// トーク用コントロールパネル左側のセリフデータグリッド取得用オブジェクトを取得する。
        /// </summary>
        public SpeechDataGrid SpeechDataGrid { get; }

        /// <summary>
        /// トーク用コントロールパネル右側の操作パネル取得用オブジェクトを取得する。
        /// </summary>
        public OperationPanel OperationPanel { get; }

        /// <summary>
        /// コントロールパネルを取得する。
        /// </summary>
        /// <param name="kind">コントロールパネル種別の設定先。</param>
        /// <param name="root">
        /// ルートコントロール。 null ならばメソッド内で取得される。
        /// </param>
        /// <param name="appVisualTree">
        /// ビジュアルツリー走査用オブジェクト。 null ならばメソッド内で取得される。
        /// </param>
        /// <returns>コントロール。見つからないならば null 。</returns>
        public Result<dynamic> GetAny(
            out ControlPanelKind kind,
            dynamic root = null,
            AppVisualTree appVisualTree = null)
        {
            kind = ControlPanelKind.None;

            // ルートコントロールを取得
            var rootCtrl = root;
            if (rootCtrl == null)
            {
                var rv = this.Root.Get();
                if (rv.Value == null)
                {
                    return (null, rv.Message);
                }
                rootCtrl = rv.Value;
            }

            // ビジュアルツリー走査用オブジェクトを取得
            var vtree = appVisualTree ?? this.AppVisualTreeGetter();
            if (vtree == null)
            {
                return (null, @"本体の情報を取得できません。");
            }

            try
            {
                var editor = vtree.GetDescendant(rootCtrl.Children[3], 0, 0);
                try
                {
                    var panel = editor.Content;

                    kind =
                        ((int)panel.Children.Count < 4) ?
                            ControlPanelKind.None : ControlPanelKind.Talk;
                    return (panel, null);
                }
                catch
                {
                    // ソング用は階層構造が異なる
                    kind = ControlPanelKind.Song;
                    return (editor.Child.Content, null);
                }
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
            }

            kind = ControlPanelKind.None;
            return (null, @"本体の操作画面が見つかりませんでした。");
        }

        /// <summary>
        /// トーク用コントロールパネルを取得する。
        /// </summary>
        /// <param name="root">
        /// ルートコントロール。 null ならばメソッド内で取得される。
        /// </param>
        /// <param name="appVisualTree">
        /// ビジュアルツリー走査用オブジェクト。 null ならばメソッド内で取得される。
        /// </param>
        /// <returns>コントロール。見つからないか取得できない状態ならば null 。</returns>
        public Result<dynamic> GetTalk(
            dynamic root = null,
            AppVisualTree appVisualTree = null)
        {
            // ルートコントロールを取得
            var rootCtrl = root;
            if (rootCtrl == null)
            {
                var rv = this.Root.Get();
                if (rv.Value == null)
                {
                    return (null, rv.Message);
                }
                rootCtrl = rv.Value;
            }

            // コントロールパネル取得
            var (panel, panelMessage) =
                this.GetAny(out var kind, (DynamicAppVar)rootCtrl, appVisualTree);
            if (panel == null)
            {
                return (null, panelMessage);
            }

            // 既にトーク用トラック選択中ならそのまま返す
            if (kind == ControlPanelKind.Talk)
            {
                return panel;
            }

            // トラック選択変更不可なら失敗扱い
            if (!this.CanChangeTrackGetter())
            {
                return (null, @"本体のトークトラックが選択されていません。");
            }

            // トラックセレクタ取得
            var (trackSelector, trackSelectorMessage) =
                this.Root.TrackSelector.Get((DynamicAppVar)rootCtrl);
            if (trackSelector == null)
            {
                return (null, trackSelectorMessage);
            }

            // トーク用トラックを選択する
            int? selectedIndex = null;
            try
            {
                // selector の Items[N].Category でトーク用/ソング用を判別できるが、
                // CeVIO定義の型を参照することになるのでやめておく。

                selectedIndex = (int)trackSelector.SelectedIndex;

                var count = (int)trackSelector.Items.Count;
                for (int ii = 0; ii < count; ++ii)
                {
                    // 元々選択していたものはトーク用ではないのでスキップ
                    if (ii == selectedIndex.Value)
                    {
                        continue;
                    }

                    // トラック選択変更
                    trackSelector.SelectedIndex = ii;

                    // 改めてコントロールパネル取得
                    (panel, panelMessage) =
                        this.GetAny(out kind, (DynamicAppVar)rootCtrl, appVisualTree);
                    if (panel == null)
                    {
                        return (null, panelMessage);
                    }

                    if (kind == ControlPanelKind.Talk)
                    {
                        selectedIndex = null; // finally での戻し処理を行わせない
                        return (panel, null);
                    }
                }
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
            }
            finally
            {
                // トーク用トラックが見つからなければ選択を元に戻しておく
                // 失敗してもよい
                if (selectedIndex.HasValue)
                {
                    try
                    {
                        trackSelector.SelectedIndex = selectedIndex.Value;
                    }
                    catch (Exception ex)
                    {
                        ThreadDebug.WriteException(ex);
                    }
                }
            }

            return (null, @"本体にトークトラックが追加されていません。");
        }

        /// <summary>
        /// ルートコントロール取得用オブジェクト取得する。
        /// </summary>
        private Root Root { get; }

        /// <summary>
        /// ビジュアルツリー走査用オブジェクト取得デリゲートを取得する。
        /// </summary>
        private Func<AppVisualTree> AppVisualTreeGetter { get; }

        /// <summary>
        /// トラック選択変更可否取得デリゲートを取得する。
        /// </summary>
        private Func<bool> CanChangeTrackGetter { get; }
    }
}
