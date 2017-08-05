using System;
using System.Windows;
using Codeer.Friendly;
using Codeer.Friendly.Dynamic;
using RucheHome.Automation.Friendly;
using RucheHome.Automation.Friendly.Wpf;
using RucheHome.Diagnostics;

namespace RucheHome.Automation.Talkers.Friendly
{
    /// <summary>
    /// WPFプロセス対象の、 Codeer.Friendly ライブラリを用いた
    /// <see cref="IProcessTalker{TParameterId}"/> インタフェースの抽象実装クラス。
    /// </summary>
    /// <typeparam name="TParameterId">パラメータID型。</typeparam>
    /// <remarks>
    /// <see cref="IProcessTalker"/> インタフェースの各メソッドは互いに排他制御されている。
    /// そのため、派生クラスで抽象メソッドを実装する際、それらのメソッドを呼び出さないこと。
    /// </remarks>
    public abstract class WpfTalkerBase<TParameterId>
        : TalkerBase<TParameterId>
    {
        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="processClrVersion">操作対象プロセスのCLRバージョン種別。</param>
        protected WpfTalkerBase(ClrVersion processClrVersion) : base(processClrVersion)
        {
        }

        /// <summary>
        /// ボタンのクリック操作をエミュレートする。
        /// </summary>
        /// <param name="button">ボタン。</param>
        /// <param name="async">非同期オブジェクト。 null ならば同期処理。</param>
        protected static void PerformClick(dynamic button, Async async = null) =>
            WpfClicker.Click(button, async);

        /// <summary>
        /// ビジュアルツリー走査用オブジェクトを取得する。
        /// </summary>
        /// <remarks>
        /// <see cref="TalkerBase{TParameterId}.TargetApp"/>
        /// プロパティの変更時に更新される。
        /// </remarks>
        protected WpfVisualTree TargetAppVisualTree
        {
            get => this.targetAppVisualTree;
            private set => this.SetProperty(ref this.targetAppVisualTree, value);
        }
        private WpfVisualTree targetAppVisualTree = null;

        /// <summary>
        /// メインウィンドウを取得する。
        /// </summary>
        /// <returns>メインウィンドウ。見つからなかった場合は null 。</returns>
        /// <remarks>
        /// 戻り値が有効である場合、本体側の
        /// <see cref="Window"/> オブジェクトを参照している。
        /// </remarks>
        protected dynamic GetMainWindow()
        {
            var app = this.TargetApp;
            if (app == null)
            {
                return null;
            }

            try
            {
                var mainWin = app.Type<Application>().Current.MainWindow;

                if (
                    mainWin != null &&
                    this.CheckWindowTitleKind((string)mainWin.Title) == WindowTitleKind.Main)
                {
                    return mainWin;
                }
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
            }
            return null;
        }

        #region ProcessTalkerBase<ParameterId> のオーバライド

        /// <summary>
        /// <see cref="TalkerBase{TParameterId}.TargetApp"/>
        /// プロパティ値の変更時に呼び出される。
        /// </summary>
        protected override void OnTargetAppChanged()
        {
            var app = this.TargetApp;

            // TargetAppVisualTree を更新
            try
            {
                this.TargetAppVisualTree = (app == null) ? null : new WpfVisualTree(app);
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
                this.TargetAppVisualTree = null;
            }
        }

        #endregion
    }
}
