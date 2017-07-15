using System;
using System.Diagnostics;
using System.Linq;
using System.Windows.Automation;
using Codeer.Friendly;
using Codeer.Friendly.Windows;
using Codeer.Friendly.Windows.Grasp;
using Codeer.Friendly.Windows.NativeStandardControls;
using RucheHome.Diagnostics;

namespace RucheHome.Talker
{
    /// <summary>
    /// Codeer.Friendly ライブラリを用いた
    /// <see cref="IProcessTalker"/> インタフェースの抽象実装クラス。
    /// </summary>
    /// <typeparam name="TParameterId">パラメータID型。</typeparam>
    /// <remarks>
    /// <see cref="IProcessTalker"/> インタフェースの各メソッドは互いに排他制御されている。
    /// そのため、派生クラスで抽象メソッドを実装する際、それらのメソッドを呼び出さないこと。
    /// </remarks>
    public abstract class FriendlyProcessTalkerBase<TParameterId>
        : ProcessTalkerBase<TParameterId>, IDisposable
    {
        /// <summary>
        /// CLRバージョン種別列挙。
        /// </summary>
        protected enum ClrVersion
        {
            /// <summary>
            /// v2.0.50727
            /// </summary>
            V2,

            /// <summary>
            /// v4.0.30319
            /// </summary>
            V4,
        }

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
        protected FriendlyProcessTalkerBase(
            ClrVersion processClrVersion,
            string processFileName,
            string processProduct,
            string talkerName = null,
            bool canSaveBlankText = false,
            bool hasCharacters = false)
            :
            base(
                processFileName,
                processProduct,
                talkerName,
                canSaveBlankText,
                hasCharacters)
        {
            ArgumentValidation.IsEnumDefined(processClrVersion, nameof(processClrVersion));

            this.ProcessClrVersion = processClrVersion;
        }

        /// <summary>
        /// デストラクタ。
        /// </summary>
        ~FriendlyProcessTalkerBase() => this.Dispose(false);

        /// <summary>
        /// <see cref="Dispose()"/> メソッドによってリソース破棄済みであるか否かを取得する。
        /// </summary>
        public bool IsDisposed { get; private set; } = false;

        /// <summary>
        /// ウィンドウタイトル種別列挙。
        /// </summary>
        protected enum WindowTitleKind
        {
            /// <summary>
            /// メインウィンドウ。
            /// </summary>
            Main,

            /// <summary>
            /// 起動中もしくは終了中。
            /// </summary>
            StartupOrCleanup,

            /// <summary>
            /// ファイル保存処理関連。
            /// </summary>
            FileSaving,

            /// <summary>
            /// 他の定義以外。
            /// </summary>
            Others,
        }

        /// <summary>
        /// 非同期アクションを開始し、その完了を待機する。
        /// </summary>
        /// <param name="action">非同期アクション。</param>
        /// <param name="timeoutMilliseconds">
        /// タイムアウトミリ秒数。既定値は
        /// <see cref="ProcessTalkerBase{TParameterId}.StandardTimeoutMilliseconds"/> 。
        /// 負数ならば無制限。
        /// </param>
        /// <returns>完了したならば true 。タイムアウトしたならば false 。</returns>
        protected static bool WaitAsyncAction(
            Action<Async> action,
            int timeoutMilliseconds = StandardTimeoutMilliseconds)
        {
            ArgumentValidation.IsNotNull(action, nameof(action));

            var async = new Async();

            action(async);

            if (!WaitUntil(() => async.IsCompleted, timeoutMilliseconds))
            {
                return false;
            }

            if (async.ExecutingException != null)
            {
                throw async.ExecutingException;
            }

            return async.IsCompleted;
        }

        /// <summary>
        /// Zオーダーインデックスを辿ることによって子孫コントロールを取得する。
        /// </summary>
        /// <param name="root">ツリーのルートとなるコントロール。</param>
        /// <param name="zIndices">Zオーダーインデックス配列。</param>
        /// <returns>コントロール。取得できなかった場合は null 。</returns>
        protected static WindowControl GetControlFromZOrder(
            WindowControl root,
            params int[] zIndices)
        {
            if (root != null && zIndices != null)
            {
                try
                {
                    return root.IdentifyFromZIndex(zIndices);
                }
                catch (Exception ex)
                {
                    ThreadDebug.WriteException(ex);
                }
            }

            return null;
        }

        /// <summary>
        /// Zオーダーインデックスを辿ることによって子孫コントロールを取得する。
        /// </summary>
        /// <typeparam name="TControl">
        /// 子孫コントロール型。
        /// <see cref="WindowControl"/> インスタンスを代入可能ではない場合、
        /// <see cref="WindowControl"/> インスタンスを引数に取るコンストラクタが必要。
        /// </typeparam>
        /// <param name="root">ツリーのルートとなるコントロール。</param>
        /// <param name="zIndices">Zオーダーインデックス配列。</param>
        /// <returns>コントロール。取得できなかった場合は null 。</returns>
        protected static TControl GetControlFromZOrder<TControl>(
            WindowControl root,
            params int[] zIndices)
            where TControl : class
        {
            var c = GetControlFromZOrder(root, zIndices);
            if (c != null)
            {
                try
                {
                    return
                        typeof(TControl).IsAssignableFrom(typeof(WindowControl)) ?
                            (TControl)(object)c :
                            (TControl)Activator.CreateInstance(typeof(TControl), c);
                }
                catch (Exception ex)
                {
                    ThreadDebug.WriteException(ex);
                }
            }

            return null;
        }

        /// <summary>
        /// ビジュアルツリーインデックスを辿ることによってWPFの子孫コントロールを取得する。
        /// </summary>
        /// <param name="root">ツリーのルートとなるコントロール。</param>
        /// <param name="treeIndices">ビジュアルツリーインデックス配列。</param>
        /// <returns>WPFコントロール。取得できなかった場合は null 。</returns>
        protected static AppVar GetControlFromVisualTree(
            WindowControl root,
            params int[] treeIndices)
        {
            if (root != null && treeIndices != null)
            {
                try
                {
                    return root.IdentifyFromVisualTreeIndex(treeIndices);
                }
                catch (Exception ex)
                {
                    ThreadDebug.WriteException(ex);
                }
            }

            return null;
        }

        /// <summary>
        /// ビジュアルツリーインデックスを辿ることによってWPFの子孫コントロールを取得する。
        /// </summary>
        /// <typeparam name="TControl">
        /// WPFの子孫コントロール型。
        /// <see cref="AppVar"/> インスタンスを代入可能ではない場合、
        /// <see cref="AppVar"/> インスタンスを引数に取るコンストラクタが必要。
        /// </typeparam>
        /// <param name="root">ツリーのルートとなるコントロール。</param>
        /// <param name="treeIndices">ビジュアルツリーインデックス配列。</param>
        /// <returns>WPFコントロール。取得できなかった場合は null 。</returns>
        protected static TControl GetControlFromVisualTree<TControl>(
            WindowControl root,
            params int[] treeIndices)
            where TControl : class
        {
            var v = GetControlFromVisualTree(root, treeIndices);
            if (v != null)
            {
                try
                {
                    return
                        typeof(TControl).IsAssignableFrom(typeof(AppVar)) ?
                            (TControl)(object)v :
                            (TControl)Activator.CreateInstance(typeof(TControl), v);
                }
                catch (Exception ex)
                {
                    ThreadDebug.WriteException(ex);
                }
            }

            return null;
        }

        /// <summary>
        /// タブコントロールから指定した名前のタブページを検索する。
        /// </summary>
        /// <param name="tabControl">タブコントロール。</param>
        /// <param name="name">検索するタブページ名。</param>
        /// <returns>タブページ。見つからなかった場合は null 。</returns>
        /// <remarks>
        /// 検索対象はWinFormsの TabPage のみ。
        /// </remarks>
        protected static WindowControl FindFormsTabPage(WindowControl tabControl, string name)
        {
            try
            {
                return
                    tabControl?
                        .GetFromWindowText(name)
                        .FirstOrDefault(
                            c => c.TypeFullName == @"System.Windows.Forms.TabPage");
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
            }
            return null;
        }

        /// <summary>
        /// ファイルダイアログからファイル名コンボボックスを取得する。
        /// </summary>
        /// <param name="fileDialog">ファイルダイアログ。</param>
        /// <returns>コンボボックス。取得できなかった場合は null 。</returns>
        protected static NativeComboBox GetFileDialogFileNameComboBox(
            WindowControl fileDialog)
            =>
            GetControlFromZOrder<NativeComboBox>(fileDialog, 11, 0, 4, 0);

        /// <summary>
        /// ファイルダイアログから決定ボタンを取得する。
        /// </summary>
        /// <param name="fileDialog">ファイルダイアログ。</param>
        /// <returns>ボタン。取得できなかった場合は null 。</returns>
        protected static NativeButton GetFileDialogOkButton(WindowControl fileDialog)
        {
            if (fileDialog != null)
            {
                try
                {
                    return new NativeButton(fileDialog.IdentifyFromDialogId(1));
                }
                catch (Exception ex)
                {
                    ThreadTrace.WriteException(ex);
                }
            }

            return null;
        }

        /// <summary>
        /// ファイルダイアログにファイルパスを設定して決定ボタンをクリックする。
        /// </summary>
        /// <param name="fileDialog">ファイルダイアログ。</param>
        /// <param name="filePath">ファイルパス。 null や空文字列であってはならない。</param>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        protected static Result<bool> OperateFileDialog(
            WindowControl fileDialog,
            string filePath)
        {
            ArgumentValidation.IsNotNull(fileDialog, nameof(fileDialog));
            ArgumentValidation.IsNotNullOrEmpty(filePath, nameof(filePath));

            // ファイル名コンボボックス、決定ボタンを取得
            var fileNameCombo = GetFileDialogFileNameComboBox(fileDialog);
            if (fileNameCombo == null)
            {
                return (false, @"ダイアログのファイル名入力欄が見つかりません。");
            }
            var okButton = GetFileDialogOkButton(fileDialog);
            if (okButton == null)
            {
                return (false, @"ダイアログの決定ボタンが見つかりません。");
            }

            // ファイルパス設定
            try
            {
                var ok =
                    WaitAsyncAction(
                        async => fileNameCombo.EmulateChangeEditText(filePath, async));
                if (!ok)
                {
                    return (
                        false,
                        @"ダイアログへのファイルパス設定処理がタイムアウトしました。");
                }
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
                return (false, @"ダイアログへファイルパスを設定できませんでした。");
            }

            try
            {
                // 決定ボタンクリック
                var okAsync = new Async();
                okButton.EmulateClick(okAsync);

                // ダイアログが表示されてしまったら失敗
                var dialog = fileDialog.WaitForNextModal(okAsync);
                if (dialog != null)
                {
                    return (false, @"ダイアログが表示されたため処理を中止しました。");
                }
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
                return (false, @"ダイアログの決定ボタンをクリックできませんでした。");
            }

            return true;
        }

        /// <summary>
        /// 操作対象プロセスのCLRバージョン種別を取得する。
        /// </summary>
        /// <remarks>
        /// インスタンス生成後に値が変化することはない。
        /// </remarks>
        protected ClrVersion ProcessClrVersion { get; }

        /// <summary>
        /// 操作対象アプリを取得する。
        /// </summary>
        /// <remarks>
        /// <see cref="ProcessTalkerBase{TParameterId}.IsAlive"/> が
        /// true の時のみ有効な値を返す。
        /// </remarks>
        protected WindowsAppFriend TargetApp { get; private set; } = null;

        /// <summary>
        /// メインウィンドウを検索する。
        /// </summary>
        /// <returns>メインウィンドウ。見つからなかった場合は null 。</returns>
        protected WindowControl FindMainWindow()
        {
            var app = this.TargetApp;
            if (app == null)
            {
                return null;
            }

            try
            {
                return
                    WindowControl.GetTopLevelWindows(app)
                        .FirstOrDefault(
                            win =>
                            {
                                try
                                {
                                    var kind =
                                        this.CheckWindowTitleKind(win.GetWindowText());
                                    return (kind == WindowTitleKind.Main);
                                }
                                catch (Exception ex)
                                {
                                    ThreadTrace.WriteException(ex);
                                }
                                return false;
                            });
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
            }
            return null;
        }

        /// <summary>
        /// 操作対象プロセスからアプリインスタンスを生成する。
        /// </summary>
        /// <param name="process">操作対象プロセス。</param>
        /// <returns>操作対象アプリ。生成できなければ null 。</returns>
        private WindowsAppFriend CreateApp(Process process)
        {
            try
            {
                if (process?.HasExited == false)
                {
                    switch (this.ProcessClrVersion)
                    {
                    case ClrVersion.V2:
                        return new WindowsAppFriend(process, @"v2.0.50727");

                    case ClrVersion.V4:
                        return new WindowsAppFriend(process, @"v4.0.30319");
                    }
                }
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
            }

            return null;
        }

        #region 要オーバライド

        /// <summary>
        /// プロパティ値変更時に呼び出される。
        /// </summary>
        /// <param name="name">プロパティ名。</param>
        /// <param name="newValue">変更後の値。</param>
        /// <param name="oldValue">変更前の値。</param>
        /// <remarks>
        /// 既定では何も行わない。
        /// </remarks>
        protected virtual void OnPropertyChangedImpl(
            string name,
            object newValue,
            object oldValue)
        {
            // 何もしない
        }

        /// <summary>
        /// ウィンドウタイトル種別を調べる。
        /// </summary>
        /// <param name="title">ウィンドウタイトル。</param>
        /// <returns>ウィンドウタイトル種別。</returns>
        protected abstract WindowTitleKind CheckWindowTitleKind(string title);

        /// <summary>
        /// メインウィンドウがトップレベルである前提で、操作対象アプリの状態を調べる。
        /// </summary>
        /// <param name="mainWindow">メインウィンドウ。必ずトップレベル。</param>
        /// <returns>状態値。</returns>
        /// <remarks>
        /// このメソッドの戻り値によって
        /// <see cref="ProcessTalkerBase{TParameterId}.State"/> プロパティ等が更新される。
        /// 状態値が <see cref="TalkerState.Fail"/> の場合は付随メッセージも利用される。
        /// </remarks>
        protected abstract Result<TalkerState> CheckState(WindowControl mainWindow);

        #endregion

        #region ProcessTalkerBase<TParameterId> のオーバライド

        /// <summary>
        /// プロパティ値変更時に呼び出される。
        /// </summary>
        /// <param name="name">プロパティ名。</param>
        /// <param name="newValue">変更後の値。</param>
        /// <param name="oldValue">変更前の値。</param>
        protected override sealed void OnPropertyChanged(
            string name,
            object newValue,
            object oldValue)
        {
            bool targetAppChanged = false;
            WindowsAppFriend targetAppOld = null;

            // Dispose 未実施時のみ IsAlive, TargetProcess を処理
            // IsAlive == true 時のみ TargetApp が有効となるようにする
            if (!this.IsDisposed)
            {
                Process process = null;
                bool processChanged = false;

                switch (name)
                {
                case nameof(IsAlive):
                    if (newValue is bool alive)
                    {
                        process = alive ? this.TargetProcess : null;
                        processChanged = true;
                    }
                    break;

                case nameof(TargetProcess):
                    process = this.IsAlive ? (newValue as Process) : null;
                    processChanged = true;
                    break;
                }

                if (processChanged && process?.Id != this.TargetApp?.ProcessId)
                {
                    targetAppOld = this.TargetApp;
                    this.TargetApp?.Dispose();
                    this.TargetApp = (process == null) ? null : this.CreateApp(process);

                    targetAppChanged = (this.TargetApp != targetAppOld);
                }
            }

            // 派生クラス処理
            this.OnPropertyChangedImpl(name, newValue, oldValue);

            // TargetApp の変更通知
            if (targetAppChanged)
            {
                this.RaisePropertyChanged(nameof(TargetApp));
                this.OnPropertyChangedImpl(nameof(TargetApp), this.TargetApp, targetAppOld);
            }
        }

        /// <summary>
        /// 操作対象プロセスの状態を調べる。
        /// </summary>
        /// <param name="process">
        /// 操作対象プロセス。
        /// <see cref="ProcessTalkerBase{TParameterId}"/> 実装から
        /// null や操作対象外プロセスが渡されることはない。
        /// </param>
        /// <returns>状態値。</returns>
        protected override sealed Result<TalkerState> CheckState(Process process)
        {
            if (this.IsDisposed || process.HasExited)
            {
                return TalkerState.None;
            }

            // ウィンドウタイトルから状態を決定するローカルメソッド
            TalkerState? decideStateByWindowTitle(string title)
            {
                switch (this.CheckWindowTitleKind(title))
                {
                case WindowTitleKind.Main:
                    // 状態決定不可
                    return null;

                case WindowTitleKind.StartupOrCleanup:
                    // 未起動or起動中なら起動中、そうでなければ終了中
                    return
                        (this.State == TalkerState.None ||
                         this.State == TalkerState.Startup) ?
                            TalkerState.Startup : TalkerState.Cleanup;

                case WindowTitleKind.FileSaving:
                    return TalkerState.FileSaving;

                case WindowTitleKind.Others:
                    // 未起動or起動中なら起動中、そうでなければブロッキング中
                    return
                        (this.State == TalkerState.None ||
                         this.State == TalkerState.Startup) ?
                            TalkerState.Startup : TalkerState.Blocking;
                }

                // ここには来ないはずだが現状維持にしておく
                return this.State;
            }

            WindowsAppFriend app = null;

            try
            {
                // メインウィンドウタイトルから状態決定を試みる
                var state = decideStateByWindowTitle(process.MainWindowTitle);
                if (state.HasValue)
                {
                    return state.Value;
                }

                // 操作対象アプリ取得or作成
                // TargetApp とプロセスIDが同じなら TargetApp を使う
                app =
                    (this.TargetApp?.ProcessId == process.Id) ?
                        this.TargetApp : CreateApp(process);
                if (app == null || process.HasExited)
                {
                    // アプリが終了している
                    return TalkerState.None;
                }

                // トップレベルウィンドウ群取得
                var topWins = WindowControl.GetTopLevelWindows(app);
                if (topWins == null || topWins.Length == 0)
                {
                    // アプリが終了している？
                    return TalkerState.None;
                }

                // トップレベルウィンドウ群のタイトルから状態決定を試みる
                var states =
                    topWins
                        .Select(win => decideStateByWindowTitle(win.GetWindowText()))
                        .Where(s => s.HasValue)
                        .Select(s => s.Value);
                if (states.Any())
                {
                    // メインウィンドウ以外が含まれるならここに来る
                    // より大きい TalkerState を優先する
                    return states.Max();
                }

                // 派生クラス処理で状態決定する
                return this.CheckState(topWins[0]);
            }
            catch (FriendlyOperationException ex)
            {
                // Friendly の処理失敗はアプリ終了直後と判断
                ThreadDebug.WriteException(ex);
                return (TalkerState.None, ex.Message);
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
                return (
                    process.HasExited ? TalkerState.None : TalkerState.Fail,
                    ex.Message ?? (ex.GetType().Name + @" 例外が発生しました。"));
            }
            finally
            {
                // TargetApp と異なる場合は破棄
                if (app != this.TargetApp)
                {
                    app?.Dispose();
                }
            }
        }

        /// <summary>
        /// <see cref="Process.CloseMainWindow"/> 呼び出し直前に呼び出される。
        /// </summary>
        /// <param name="process">
        /// 操作対象プロセス。
        /// <see cref="ProcessTalkerBase{TParameterId}"/> 実装から
        /// null や操作対象外プロセスが渡されることはない。
        /// </param>
        protected override void OnProcessExiting(Process process)
        {
            // TargetApp を破棄
            this.TargetApp?.Dispose();
            this.TargetApp = null;
        }

        /// <summary>
        /// <see cref="Process.CloseMainWindow"/>
        /// 呼び出し後の操作対象プロセスが終了済みか否かを調べる。
        /// </summary>
        /// <param name="process">
        /// <see cref="Process.CloseMainWindow"/> 呼び出し後の操作対象プロセス。
        /// 呼び出し元で <see cref="Process.Refresh"/> 呼び出し済み。
        /// <see cref="ProcessTalkerBase{TParameterId}"/> 実装から
        /// null や操作対象外プロセスが渡されることはない。
        /// </param>
        /// <returns>
        /// 終了を確認できたならば true 。
        /// ブロッキング処理等により終了できないことを確認できたならば null 。
        /// いずれも確認できなければ false 。
        /// </returns>
        /// <remarks>
        /// 終了待機中に <see cref="WindowControl.GetTopLevelWindows"/>
        /// 等が呼び出されると長時間ブロッキングされる場合があるため、
        /// <see cref="CheckState(Process)"/> を用いないようにする。
        /// </remarks>
        protected override bool? CheckProcessExited(Process process)
        {
            if (process.WaitForExit(0))
            {
                return true;
            }

            // メインウィンドウをオーナーとするウィンドウが存在するならば
            // モーダルウィンドウ表示によるブロッキングと判断
            var handle = process.MainWindowHandle;
            if (handle != IntPtr.Zero)
            {
                try
                {
                    var mainWin = AutomationElement.FromHandle(handle);
                    var dialog =
                        mainWin.FindFirst(
                            TreeScope.Children,
                            new PropertyCondition(
                                AutomationElement.ControlTypeProperty,
                                ControlType.Window));
                    if (dialog != null)
                    {
                        ThreadDebug.WriteLine(
                            dialog.Current.ClassName + ":" + dialog.Current.Name);
                        return null;
                    }
                }
                catch { }
            }

            return false;
        }

        #endregion

        #region IDisposable の実装

        /// <summary>
        /// リソースを破棄する。
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// リソース破棄の実処理を行う。
        /// </summary>
        /// <param name="disposing">
        /// Dispose メソッドから呼び出された場合は true 。
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.IsDisposed = true;
            }

            this.TargetApp?.Dispose();
            this.TargetApp = null;
        }

        #endregion
    }
}
