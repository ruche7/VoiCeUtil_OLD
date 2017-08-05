using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Automation;
using Codeer.Friendly;
using Codeer.Friendly.Windows;
using Codeer.Friendly.Windows.Grasp;
using RucheHome.Automation.Friendly;
using RucheHome.Automation.Friendly.Native;
using RucheHome.Diagnostics;

namespace RucheHome.Automation.Talkers.Friendly
{
    /// <summary>
    /// Codeer.Friendly ライブラリを用いた
    /// <see cref="IProcessTalker{TParameterId}"/> インタフェースの抽象実装クラス。
    /// </summary>
    /// <typeparam name="TParameterId">パラメータID型。</typeparam>
    /// <remarks>
    /// <see cref="IProcessTalker"/> インタフェースの各メソッドは互いに排他制御されている。
    /// そのため、派生クラスで抽象メソッドを実装する際、それらのメソッドを呼び出さないこと。
    /// </remarks>
    public abstract class TalkerBase<TParameterId>
        : ProcessTalkerBase<TParameterId>, IDisposable
    {
        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="processClrVersion">操作対象プロセスのCLRバージョン種別。</param>
        protected TalkerBase(ClrVersion processClrVersion) : base()
        {
            ArgumentValidation.IsEnumDefined(processClrVersion, nameof(processClrVersion));

            this.ProcessClrVersion = processClrVersion;
        }

        /// <summary>
        /// デストラクタ。
        /// </summary>
        ~TalkerBase() => this.Dispose(false);

        /// <summary>
        /// <see cref="Dispose()"/> メソッドによってリソース破棄済みであるか否かを取得する。
        /// </summary>
        public bool IsDisposed
        {
            get => this.disposed;
            private set => this.SetProperty(ref this.disposed, value);
        }
        private bool disposed = false;

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
        /// <see cref="ProcessOperationBase.StandardTimeoutMilliseconds"/> 。
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

            // ファイルダイアログオブジェクト作成
            NativeFileDialog dialog = null;
            try
            {
                dialog = new NativeFileDialog(fileDialog);
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
                return (false, @"ダイアログの情報を取得できませんでした。");
            }

            // ファイルパス設定
            try
            {
                if (!WaitAsyncAction(async => dialog.SetFileName(filePath, async)))
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
                var async = new Async();
                dialog.ClickDecideButton(async);

                // ダイアログが表示されてしまったら失敗
                if (dialog.Base.WaitForNextModal(async) != null)
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
        protected WindowsAppFriend TargetApp { get; private set; }

        /// <summary>
        /// 操作対象プロセスからアプリインスタンスを生成する。
        /// </summary>
        /// <param name="process">操作対象プロセス。</param>
        /// <returns>操作対象アプリ。生成できなければ null 。</returns>
        private WindowsAppFriend CreateApp(Process process) =>
            AppFactory.Create(process, this.ProcessClrVersion);

        #region 要オーバライド

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
        /// <see cref="ProcessTalkerBase{TParameterId}.State"/> 等が更新される。
        /// 付随メッセージも
        /// <see cref="ProcessTalkerBase{TParameterId}.StateMessage"/> に利用される。
        /// </remarks>
        protected abstract Result<TalkerState> CheckState(WindowControl mainWindow);

        /// <summary>
        /// <see cref="TargetApp"/> プロパティ値の変更時に呼び出される。
        /// </summary>
        /// <remarks>
        /// 既定では何も行わない。
        /// </remarks>
        protected virtual void OnTargetAppChanged()
        {
            // 何もしない
        }

        #endregion

        #region ProcessTalkerBase<TParameterId> のオーバライド

        /// <summary>
        /// 操作対象プロセスの状態を調べる。
        /// </summary>
        /// <param name="process">
        /// 操作対象プロセス。
        /// <see cref="TalkerBase{TParameterId}"/> 実装から
        /// null や操作対象外プロセスが渡されることはない。
        /// </param>
        /// <returns>状態値。</returns>
        protected override sealed Result<TalkerState> CheckState(Process process)
        {
            if (this.IsDisposed)
            {
                return (TalkerState.Fail, @"オブジェクトを破棄済みです。");
            }
            if (process.HasExited)
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
                        this.TargetApp : this.CreateApp(process);

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

                // ここまで来たらメインウィンドウしかいないはず
                Debug.Assert(topWins.Length == 1);
                Debug.Assert(
                    this.CheckWindowTitleKind(topWins[0].GetWindowText()) ==
                    WindowTitleKind.Main);

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
        /// <see cref="OnPropertiesChanged"/> から
        /// <see cref="TargetApp"/> の名前を返すための列挙オブジェクト。
        /// </summary>
        private static readonly IEnumerable<string> TargetAppPropertyNameEnumerable =
            new[] { nameof(TargetApp) };

        /// <summary>
        /// <see cref="ProcessTalkerBase{TParameterId}.UpdatePropertiesByAction"/>
        /// メソッドによってプロパティが 1 つ以上更新された時に呼び出される。
        /// </summary>
        /// <param name="changedPropertyNames">
        /// 変更されたプロパティ名のコレクション。必ず要素数 1 以上となる。
        /// </param>
        /// <returns>追加で更新通知するプロパティ名の列挙。不要ならば null 。</returns>
        protected override IEnumerable<string> OnPropertiesChanged(
            IReadOnlyCollection<string> changedPropertyNames)
        {
            if (this.IsDisposed)
            {
                return null;
            }

            // IsAlive, TargetProcess を処理
            if (
                changedPropertyNames.Contains(nameof(IsAlive)) ||
                changedPropertyNames.Contains(nameof(TargetProcess)))
            {
                // IsAlive == true 時のみ TargetApp が有効となるようにする
                var process = this.IsAlive ? this.TargetProcess : null;

                // プロセスIDが異なるなら差し替え
                if (process?.Id != this.TargetApp?.ProcessId)
                {
                    this.TargetApp?.Dispose();
                    this.TargetApp = (process == null) ? null : this.CreateApp(process);

                    // TargetApp 変更時処理
                    this.OnTargetAppChanged();

                    return TargetAppPropertyNameEnumerable;
                }
            }

            return null;
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
        /// コントロールタイプが Window であればヒットするUI検索条件オブジェクト。
        /// </summary>
        private static readonly Condition WindowControlTypeCondition =
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Window);

        /// <summary>
        /// <see cref="Process.CloseMainWindow"/>
        /// 呼び出し後の操作対象プロセスが終了済みか否かを調べる。
        /// </summary>
        /// <param name="process">
        /// <see cref="Process.CloseMainWindow"/> 呼び出し後の操作対象プロセス。
        /// 呼び出し元で <see cref="Process.Refresh"/> 呼び出し済み。
        /// <see cref="TalkerBase{TParameterId}"/> 実装から
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
                        mainWin.FindFirst(TreeScope.Children, WindowControlTypeCondition);
                    if (dialog != null)
                    {
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
            if (!this.IsDisposed)
            {
                this.Dispose(true);
                GC.SuppressFinalize(this);

                this.IsDisposed = true;
            }
        }

        /// <summary>
        /// リソース破棄の実処理を行う。
        /// </summary>
        /// <param name="disposing">
        /// <see cref="Dispose()"/> メソッドから呼び出された場合は true 。
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
            this.TargetApp?.Dispose();
            this.TargetApp = null;
        }

        #endregion
    }
}
