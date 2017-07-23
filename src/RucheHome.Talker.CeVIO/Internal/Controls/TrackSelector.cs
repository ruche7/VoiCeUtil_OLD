using System;
using Codeer.Friendly;
using Codeer.Friendly.Dynamic;
using RM.Friendly.WPFStandardControls;
using RucheHome.Diagnostics;

namespace RucheHome.Talker.CeVIO.Internal.Controls
{
    /// <summary>
    /// ウィンドウ上部のトラックセレクタ取得処理を提供するクラス。
    /// </summary>
    internal sealed class TrackSelector
    {
        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="root">ルートコントロール取得用オブジェクト。</param>
        public TrackSelector(Root root)
        {
            this.Root = root ?? throw new ArgumentNullException(nameof(root));
        }

        /// <summary>
        /// トラックセレクタを取得する。
        /// </summary>
        /// <param name="root">
        /// ルートコントロール。 null ならばメソッド内で取得される。
        /// </param>
        /// <returns>コントロール。見つからないならば null 。</returns>
        public Result<WPFSelector> Get(AppVar root = null)
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

            try
            {
                return
                    new WPFSelector(
                        r.Dynamic()
                            .Children[1]    // Grid
                            .Children[0]    // Timetable
                            .Content        // Grid
                            .Children[1]    // DockPanel
                            .Children[0]    // DockPanel
                            .Children[1]    // ScrollViewer
                            .Content);
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
            }
            return (null, @"本体のトラック群が見つかりませんでした。");
        }

        /// <summary>
        /// ルートコントロール取得用オブジェクト取得する。
        /// </summary>
        private Root Root { get; }
    }
}
