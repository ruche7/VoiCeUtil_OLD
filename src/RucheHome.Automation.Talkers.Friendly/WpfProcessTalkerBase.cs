using System;
using System.Windows;
using Codeer.Friendly;
using Codeer.Friendly.Dynamic;
using RucheHome.Automation.Friendly.Wpf;
using RucheHome.Diagnostics;

namespace RucheHome.Automation.Talkers.Friendly
{
    /// <summary>
    /// WPFプロセス対象の、 Codeer.Friendly ライブラリを用いた
    /// <see cref="IProcessTalker"/> インタフェースの抽象実装クラス。
    /// </summary>
    /// <typeparam name="TParameterId">パラメータID型。</typeparam>
    /// <remarks>
    /// <see cref="IProcessTalker"/> インタフェースの各メソッドは互いに排他制御されている。
    /// そのため、派生クラスで抽象メソッドを実装する際、それらのメソッドを呼び出さないこと。
    /// </remarks>
    public abstract class WpfProcessTalkerBase<TParameterId>
        : ProcessTalkerBase<TParameterId>
    {
        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="processClrVersion">操作対象プロセスのCLRバージョン種別。</param>
        /// <param name="processFileName">
        /// 操作対象プロセスの実行ファイル名(拡張子なし)。
        /// </param>
        /// <param name="processProduct">操作対象プロセスの製品名情報。</param>
        /// <param name="talkerName">名前。 null ならば processProduct を使う。</param>
        /// <param name="canSaveBlankText">空白文を音声ファイル保存可能ならば true 。</param>
        /// <param name="hasCharacters">キャラクター設定を保持しているならば true 。</param>
        protected WpfProcessTalkerBase(
            ClrVersion processClrVersion,
            string processFileName,
            string processProduct,
            string talkerName = null,
            bool canSaveBlankText = false,
            bool hasCharacters = false)
            :
            base(
                processClrVersion,
                processFileName,
                processProduct,
                talkerName,
                canSaveBlankText,
                hasCharacters)
        {
        }

        /// <summary>
        /// ボタンの押下操作をエミュレートする。
        /// </summary>
        /// <param name="button">ボタン。</param>
        /// <param name="async">非同期オブジェクト。 null ならば同期処理。</param>
        protected static void PerformClick(dynamic button, Async async = null)
        {
            ArgumentValidation.IsNotNull(button, nameof(button));

            button.Focus();

            if (async == null)
            {
                button.OnClick();
            }
            else
            {
                button.OnClick(async);
            }
        }

        /// <summary>
        /// ビジュアルツリー走査用オブジェクトを取得する。
        /// </summary>
        /// <remarks>
        /// <see cref="ProcessTalkerBase{TParameterId}.TargetApp"/>
        /// プロパティの変更時に更新される。
        /// </remarks>
        protected AppVisualTree TargetAppVisualTree
        {
            get => this.targetAppVisualTree;
            private set => this.SetProperty(ref this.targetAppVisualTree, value);
        }
        private AppVisualTree targetAppVisualTree = null;

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
        /// <see cref="ProcessTalkerBase{TParameterId}.TargetApp"/>
        /// プロパティ値の変更時に呼び出される。
        /// </summary>
        protected override void OnTargetAppChanged()
        {
            var app = this.TargetApp;

            // TargetAppVisualTree を更新
            try
            {
                this.TargetAppVisualTree = (app == null) ? null : new AppVisualTree(app);
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
