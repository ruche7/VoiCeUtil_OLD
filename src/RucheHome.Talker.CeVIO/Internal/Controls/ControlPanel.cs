using System;
using Codeer.Friendly;
using Codeer.Friendly.Dynamic;
using RucheHome.Diagnostics;

namespace RucheHome.Talker.CeVIO.Internal.Controls
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
        /// <param name="visualTreeHelperGetter">
        /// 操作対象アプリの VisualTreeHelper 型オブジェクト取得デリゲート。
        /// </param>
        /// <param name="canChangeTrackGetter">トラック選択変更可否取得デリゲート。</param>
        public ControlPanel(
            Root root,
            Func<dynamic> visualTreeHelperGetter,
            Func<bool> canChangeTrackGetter)
        {
            this.Root = root ?? throw new ArgumentNullException(nameof(root));
            this.VisualTreeHelperGetter =
                visualTreeHelperGetter ??
                throw new ArgumentNullException(nameof(visualTreeHelperGetter));
            this.CanChangeTrackGetter =
                canChangeTrackGetter ??
                throw new ArgumentNullException(nameof(canChangeTrackGetter));

            this.SpeechDataGrid = new SpeechDataGrid(this);
            this.OperationPanel = new OperationPanel(this);
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
        /// <returns>コントロール。見つからないならば null 。</returns>
        public Result<AppVar> GetAny(out ControlPanelKind kind, AppVar root = null)
        {
            kind = ControlPanelKind.None;

            // ルートコントロールを取得
            var r = root;
            if (r == null)
            {
                var rv = this.Root.Get();
                if (rv.Value == null)
                {
                    return (null, rv.Message);
                }
                r = rv.Value;
            }

            // VisualTreeHelper 型オブジェクトを取得
            var vtree = this.VisualTreeHelperGetter();
            if (vtree == null)
            {
                return (null, @"本体の情報を取得できません。");
            }

            try
            {
                var editor =
                    vtree.GetChild(vtree.GetChild(r.Dynamic().Children[3], 0), 0);
                try
                {
                    var panel = editor.Content;

                    kind =
                        ((int)panel.Children.Count < 4) ?
                            ControlPanelKind.None : ControlPanelKind.Talk;
                    return (AppVar)panel;
                }
                catch
                {
                    // ソング用は階層構造が異なる
                    kind = ControlPanelKind.Song;
                    return (AppVar)editor.Child.Content;
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
        /// <returns>コントロール。見つからないか取得できない状態ならば null 。</returns>
        public Result<AppVar> GetTalk(AppVar root = null)
        {
            // ルートコントロールを取得
            var r = root;
            if (r == null)
            {
                var rv = this.Root.Get();
                if (rv.Value == null)
                {
                    return (null, rv.Message);
                }
                r = rv.Value;
            }

            // コントロールパネル取得
            var (panel, failMessage) = this.GetAny(out var kind, r);
            if (panel == null)
            {
                return (null, failMessage);
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
            var tv = this.Root.TrackSelector.Get(r);
            if (tv.Value == null)
            {
                return (null, tv.Message);
            }
            var trackSelector = tv.Value;

            // トーク用トラックを選択する
            int? selectedIndex = null;
            try
            {
                // selector の Items[N].Category でトーク用/ソング用を判別できるが、
                // CeVIO定義の型を参照することになるのでやめておく。

                selectedIndex = trackSelector.SelectedIndex;

                var count = trackSelector.ItemCount;
                for (int ii = 0; ii < count; ++ii)
                {
                    // 元々選択していたものはトーク用ではないのでスキップ
                    if (ii == selectedIndex.Value)
                    {
                        continue;
                    }

                    // トラック選択変更
                    trackSelector.EmulateChangeSelectedIndex(ii);

                    // 改めてコントロールパネル取得
                    (panel, failMessage) = this.GetAny(out kind, r);
                    if (panel == null)
                    {
                        return (null, failMessage);
                    }

                    if (kind == ControlPanelKind.Talk)
                    {
                        selectedIndex = null; // finally での戻し処理を行わせない
                        return panel;
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
                        trackSelector.EmulateChangeSelectedIndex(selectedIndex.Value);
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
        /// 操作対象アプリの VisualTreeHelper 型オブジェクト取得デリゲートを取得する。
        /// </summary>
        private Func<dynamic> VisualTreeHelperGetter { get; }

        /// <summary>
        /// トラック選択変更可否取得デリゲートを取得する。
        /// </summary>
        private Func<bool> CanChangeTrackGetter { get; }
    }
}
