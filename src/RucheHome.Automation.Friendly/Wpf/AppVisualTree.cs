using System;
using Codeer.Friendly;
using Codeer.Friendly.Dynamic;
using RucheHome.Diagnostics;

namespace RucheHome.Automation.Friendly.Wpf
{
    /// <summary>
    /// 操作対象WPFアプリのビジュアルツリー走査処理を提供するクラス。
    /// </summary>
    public sealed class AppVisualTree
    {
        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="app">操作対象アプリ。</param>
        public AppVisualTree(AppFriend app)
        {
            ArgumentValidation.IsNotNull(app, nameof(app));

            this.App = app;
            this.VisualTreeHelper = app.Type().System.Windows.Media.VisualTreeHelper;
        }

        /// <summary>
        /// 操作対象アプリを取得する。
        /// </summary>
        public AppFriend App { get; }

        /// <summary>
        /// ビジュアルツリーを辿ることで子孫コントロールを取得する。
        /// </summary>
        /// <param name="root">ツリーのルートとなるコントロール。</param>
        /// <param name="treeIndices">ビジュアルツリーインデックス配列。</param>
        /// <returns>コントロール。取得できなかった場合は null 。</returns>
        public dynamic GetDescendant(dynamic root, params int[] treeIndices)
        {
            ArgumentValidation.IsNotNull(root, nameof(root));
            ArgumentValidation.IsNotNull(treeIndices, nameof(treeIndices));

            var control = root;

            foreach (var index in treeIndices)
            {
                control = this.VisualTreeHelper.GetChild(control, index);
            }

            return control;
        }

        /// <summary>
        /// 操作対象アプリの <see cref="VisualTreeHelper"/> 型オブジェクトを取得する。
        /// </summary>
        private dynamic VisualTreeHelper { get; }
    }
}
