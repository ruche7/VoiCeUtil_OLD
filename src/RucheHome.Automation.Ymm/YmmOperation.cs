using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Automation;
using Codeer.Friendly;
using Codeer.Friendly.Dynamic;
using Codeer.Friendly.Windows;
using Codeer.Friendly.Windows.Grasp;
using RucheHome.Automation.Friendly;
using RucheHome.Automation.Friendly.Wpf;
using RucheHome.Diagnostics;

namespace RucheHome.Automation.Ymm
{
    /// <summary>
    /// <see cref="IYmmOperation"/> 実装クラス。
    /// </summary>
    public class YmmOperation : ProcessOperationBase, IYmmOperation
    {
        /// <summary>
        /// コンストラクタ。
        /// </summary>
        public YmmOperation() : base() { }

        /// <summary>
        /// デストラクタ。
        /// </summary>
        ~YmmOperation() => this.Dispose(false);

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
        /// 操作対象アプリを取得または設定する。
        /// </summary>
        private WindowsAppFriend TargetApp { get; set; } = null;

        /// <summary>
        /// 操作対象プロセスからアプリインスタンスを生成する。
        /// </summary>
        /// <param name="process">操作対象プロセス。</param>
        /// <returns>操作対象アプリ。生成できなければ null 。</returns>
        private static WindowsAppFriend CreateApp(Process process) =>
            AppFactory.Create(process, ClrVersion.V4);

        /// <summary>
        /// ウィンドウタイトル種別列挙。
        /// </summary>
        private enum WindowTitleKind
        {
            /// <summary>
            /// メインウィンドウ。
            /// </summary>
            Main,

            /// <summary>
            /// タイムラインウィンドウ。
            /// </summary>
            Timeline,

            /// <summary>
            /// 起動中もしくは終了中。
            /// </summary>
            StartupOrCleanup,

            /// <summary>
            /// 他の定義以外。
            /// </summary>
            Others,
        }

        /// <summary>
        /// スプラッシュスクリーンのウィンドウタイトル。
        /// </summary>
        private const string SplashScreenWindowTitle =
            @"アプリケーションを開く間、お待ちください";

        /// <summary>
        /// メインウィンドウタイトルのプレフィックス。
        /// </summary>
        private const string MainWindowTitlePrefix = @"ゆっくりMovieMaker v3";

        /// <summary>
        /// タイムラインウィンドウタイトルのプレフィックス。
        /// </summary>
        private const string TimelineWindowTitlePrefix = @"タイムライン [";

        /// <summary>
        /// ウィンドウタイトル種別を調べる。
        /// </summary>
        /// <param name="title">ウィンドウタイトル。</param>
        /// <returns>ウィンドウタイトル種別。</returns>
        private static WindowTitleKind CheckWindowTitleKind(string title)
        {
            if (title == null)
            {
                return WindowTitleKind.Others;
            }

            if (title.Length == 0 || title == SplashScreenWindowTitle)
            {
                return WindowTitleKind.StartupOrCleanup;
            }
            if (title.StartsWith(MainWindowTitlePrefix))
            {
                return WindowTitleKind.Main;
            }
            if (title.StartsWith(TimelineWindowTitlePrefix))
            {
                return WindowTitleKind.Timeline;
            }

            return WindowTitleKind.Others;
        }

        /// <summary>
        /// コントロールタイプが Window であればヒットするUI検索条件オブジェクト。
        /// </summary>
        private static readonly Condition WindowControlTypeCondition =
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Window);

        /// <summary>
        /// モーダルダイアログのクラス名。
        /// </summary>
        private const string ModalDialogClassName = @"#32770";

        /// <summary>
        /// 操作対象プロセスの状態を調べる。
        /// </summary>
        /// <param name="process">操作対象プロセス。</param>
        /// <param name="timelineVisible">タイムライン表示状態の設定先。</param>
        /// <remarks>状態値。</remarks>
        private YmmState CheckState(Process process, out bool timelineVisible)
        {
            timelineVisible = false;

            if (this.IsDisposed || process.HasExited)
            {
                return YmmState.None;
            }

            try
            {
                // メインウィンドウハンドル取得
                var handle = process.MainWindowHandle;

                // メインウィンドウタイトル種別をチェック
                var kind = CheckWindowTitleKind(process.MainWindowTitle);

                if (handle == IntPtr.Zero || kind != WindowTitleKind.Main)
                {
                    // 未起動or起動中なら起動中、そうでなければ終了中
                    return
                        (this.State == YmmState.None || this.State == YmmState.Startup) ?
                            YmmState.Startup : YmmState.Cleanup;
                }

                // 子ウィンドウ群検索
                var mainWin = AutomationElement.FromHandle(handle);
                var wins =
                    mainWin
                        .FindAll(TreeScope.Children, WindowControlTypeCondition)
                        .OfType<AutomationElement>();

                // タイムラインウィンドウ表示状態設定
                timelineVisible =
                    wins
                        .Select(win => win.Current.Name)
                        .Any(name => CheckWindowTitleKind(name) == WindowTitleKind.Timeline);

                // モーダルダイアログがいるならブロッキング中
                if (wins.Any(win => win.Current.ClassName == ModalDialogClassName))
                {
                    return YmmState.Blocking;
                }

                return timelineVisible ? YmmState.Idle : YmmState.TimelineHidden;
            }
            catch (Exception ex)
            {
                ThreadDebug.WriteException(ex);
            }
            return YmmState.None;
        }

        /// <summary>
        /// 現在の <see cref="State"/> では処理を行えないことを示すメッセージを作成する。
        /// </summary>
        /// <returns>エラーメッセージ。アイドル状態である場合は null 。</returns>
        private string MakeStateErrorMessage()
        {
            var state = this.State;
            if (state == YmmState.Idle)
            {
                return null;
            }

            if (this.IsDisposed)
            {
                return @"オブジェクトを破棄済みです。";
            }

            switch (state)
            {
            case YmmState.None:
                return @"本体が起動していません。";

            case YmmState.Startup:
                return @"本体が起動完了していません。";

            case YmmState.Cleanup:
                return @"本体が終了処理中です。";

            case YmmState.TimelineHidden:
                return @"本体のタイムラインウィンドウが非表示です。";

            case YmmState.Blocking:
                return @"本体が処理できない状態です。";

            case YmmState.Idle:
                return null;

            default:
                break;
            }

            ThreadTrace.WriteLine($@"Invalid YMM state. ({(int)this.State})");
            return @"不正な状態です。";
        }

        /// <summary>
        /// 状態エラーメッセージを付随メッセージとする <see cref="Result{T}"/> 値を作成する。
        /// </summary>
        /// <typeparam name="T">戻り値の型。</typeparam>
        /// <param name="value">メソッドの戻り値。既定では default(T) 。</param>
        /// <returns><see cref="Result{T}"/> 値。</returns>
        private Result<T> MakeStateErrorResult<T>(T value = default(T)) =>
            (value, this.MakeStateErrorMessage());

        /// <summary>
        /// 指定したウィンドウタイトル種別のウィンドウを検索する。
        /// </summary>
        /// <param name="kind">ウィンドウタイトル種別。</param>
        /// <returns>ウィンドウ。見つからなければ null 。</returns>
        private dynamic FindWindow(WindowTitleKind kind)
        {
            var app = this.TargetApp;
            if (app == null)
            {
                return null;
            }

            try
            {
                // Application.Current.MainWindow はタイムラインウィンドウ等に
                // なっている場合があるので、 WindowTitleKind.Main の場合も検索する。

                var wins = app.Type().System.Windows.Application.Current.Windows;

                foreach (var win in wins)
                {
                    if (CheckWindowTitleKind((string)win.Title) == kind)
                    {
                        return win;
                    }
                }
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
            }

            return null;
        }

        /// <summary>
        /// 表示メニューのタイムラインメニューアイテムを取得する。
        /// </summary>
        /// <param name="mainWindow">
        /// メインウィンドウ。 null ならばメソッド内で取得される。
        /// </param>
        /// <returns>タイムラインメニューアイテム。見つからなければ null 。</returns>
        private Result<dynamic> GetViewMenuTimelineItem(dynamic mainWindow = null)
        {
            var mainWin = mainWindow ?? this.FindWindow(WindowTitleKind.Main);
            if (mainWin == null)
            {
                return (null, @"本体のメインウィンドウが見つかりません。");
            }

            try
            {
                return (mainWin.Content.Children[0].Items[2].Items[0], null);
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
            }
            return (null, @"本体の表示メニューが見つかりません。");
        }

        /// <summary>
        /// タイムラインウィンドウの操作コントロール群のルートコントロールを取得する。
        /// </summary>
        /// <param name="timelineWindow">
        /// タイムラインウィンドウ。 null ならばメソッド内で取得される。
        /// </param>
        /// <returns>コントロール。見つからなければ null 。</returns>
        private Result<dynamic> GetTimelineControlsRoot(dynamic timelineWindow = null)
        {
            var tlWin = timelineWindow ?? this.FindWindow(WindowTitleKind.Timeline);
            if (tlWin == null)
            {
                return (null, @"本体のタイムラインウィンドウが非表示です。");
            }

            try
            {
                return (tlWin.Content.Content.Children[3], null);
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
            }
            return (null, @"本体のタイムラインウィンドウが見つかりません。");
        }

        /// <summary>
        /// タイムラインウィンドウの操作コントロールを取得する。
        /// </summary>
        /// <param name="index">ルートコントロール内インデックス。</param>
        /// <param name="controlsRoot">
        /// 操作コントロール群のルートコントロール。 null ならばメソッド内で取得される。
        /// </param>
        /// <param name="controlName">
        /// コントロール名。取得失敗時のメッセージに利用される。
        /// null ならば "タイムラインウィンドウ" と同義。
        /// </param>
        /// <returns>コントロール。見つからなければ null 。</returns>
        private Result<dynamic> GetTimelineControl(
            int index,
            dynamic controlsRoot = null,
            string controlName = null)
        {
            var root = controlsRoot;
            if (root == null)
            {
                var rv = this.GetTimelineControlsRoot();
                if (rv.Value == null)
                {
                    return (null, rv.Message);
                }
                root = rv.Value;
            }

            try
            {
                return (root.Children[index], null);
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
            }
            return (
                null,
                $@"本体の{controlName ?? @"タイムラインウィンドウ"}が見つかりません。");
        }

        /// <summary>
        /// タイムラインウィンドウのキャラクターコンボボックスを取得する。
        /// </summary>
        /// <param name="controlsRoot">
        /// コントロール群のルートコントロール。 null ならばメソッド内で取得される。
        /// </param>
        /// <returns>コンボボックス。見つからなければ null 。</returns>
        private Result<dynamic> GetTimelineCharacterComboBox(dynamic controlsRoot = null) =>
            this.GetTimelineControl(0, (DynamicAppVar)controlsRoot, @"キャラクター選択欄");

        /// <summary>
        /// タイムラインウィンドウのセリフテキストボックスを取得する。
        /// </summary>
        /// <param name="controlsRoot">
        /// コントロール群のルートコントロール。 null ならばメソッド内で取得される。
        /// </param>
        /// <returns>テキストボックス。見つからなければ null 。</returns>
        private Result<dynamic> GetTimelineSpeechTextBox(dynamic controlsRoot = null) =>
            this.GetTimelineControl(2, (DynamicAppVar)controlsRoot, @"セリフ入力欄");

        /// <summary>
        /// タイムラインウィンドウの追加ボタンを取得する。
        /// </summary>
        /// <param name="controlsRoot">
        /// コントロール群のルートコントロール。 null ならばメソッド内で取得される。
        /// </param>
        /// <returns>ボタン。見つからなければ null 。</returns>
        private Result<dynamic> GetTimelineAddButton(dynamic controlsRoot = null) =>
            this.GetTimelineControl(5, (DynamicAppVar)controlsRoot, @"セリフ追加ボタン");

        #region ProcessOperationBase のオーバライド

        /// <summary>
        /// 操作対象プロセスの製品名情報を取得する。
        /// </summary>
        public override string ProcessProduct { get; } = @"ゆっくりMovieMaker3";

        /// <summary>
        /// 操作対象プロセスの実行ファイル名(拡張子なし)を取得する。
        /// </summary>
        public override string ProcessFileName { get; } = @"YukkuriMovieMaker_v3";

        /// <summary>
        /// 操作対象が生存状態であるか否かを取得する。
        /// </summary>
        /// <remarks>
        /// <see cref="State"/> が <see cref="YmmState.None"/>,
        /// <see cref="YmmState.Startup"/>, <see cref="YmmState.Cleanup"/>
        /// のいずれでもなければ true を返す。
        /// </remarks>
        public override bool IsAlive
        {
            get
            {
                // State の評価を1回にするために一旦変数に入れる
                var state = this.State;
                return (
                    state != YmmState.None &&
                    state != YmmState.Startup &&
                    state != YmmState.Cleanup);
            }
        }

        /// <summary>
        /// 操作対象が操作可能な状態であるか否かを取得する。
        /// </summary>
        /// <remarks>
        /// <see cref="State"/> が <see cref="YmmState.Idle"/> または
        /// <see cref="YmmState.TimelineHidden"/> ならば true を返す。
        /// </remarks>
        public override bool CanOperate
        {
            get
            {
                // State の評価を1回にするために一旦変数に入れる
                var state = this.State;
                return (state == YmmState.Idle || state == YmmState.TimelineHidden);
            }
        }

        /// <summary>
        /// 操作対象プロセスを基に、プロパティ値を変更するデリゲートを作成する。
        /// </summary>
        /// <param name="targetProcess">
        /// 操作対象プロセス。見つからなかった場合は null 。
        /// </param>
        /// <returns>プロパティ値を変更するデリゲート。</returns>
        /// <remarks>
        /// <see cref="CheckState"/> を呼び出し、その戻り値によって
        /// <see cref="ProcessOperationBase.TargetProcess"/>, <see cref="State"/>,
        /// <see cref="IsTimelineVisible"/> を変更するデリゲートを作成する。
        /// </remarks>
        protected override sealed Action MakeUpdatePropertiesAction(Process targetProcess) =>
            () =>
            {
                // ベースクラス処理によって TargetProcess を更新
                base.MakeUpdatePropertiesAction(targetProcess)?.Invoke();
                var process = this.TargetProcess;

                // 状態確認
                bool timelineVisible = false;
                var state =
                    (process != null) ?
                        this.CheckState(process, out timelineVisible) : YmmState.None;

                // state が None なら TargetProcess を null に更新
                if (state == YmmState.None && process != null)
                {
                    base.MakeUpdatePropertiesAction(null)?.Invoke();
                }

                this.State = state;
                this.IsTimelineVisible = timelineVisible;
            };

        /// <summary>
        /// プロパティ値を変更するデリゲートを呼び出し、
        /// 呼び出しの前後で値の変化したプロパティ名のコレクションを返す。
        /// </summary>
        /// <param name="updateProperties">プロパティ値を変更するデリゲート。</param>
        /// <returns>値の変化したプロパティ名のコレクション。</returns>
        /// <remarks>
        /// ベースクラスの監視対象に加えて
        /// <see cref="State"/>, <see cref="IsTimelineVisible"/> の変更を監視する。
        /// また、 <see cref="TargetApp"/> の更新を行う。
        /// </remarks>
        protected override sealed IReadOnlyCollection<string> UpdatePropertiesByAction(
            Action updateProperties)
        {
            var oldState = this.State;
            var oldTimelineVisible = this.IsTimelineVisible;

            // ベースクラス処理
            var propNames =
                base.UpdatePropertiesByAction(updateProperties)?.ToList() ??
                new List<string>();

            if (this.State != oldState)
            {
                propNames.Add(nameof(State));
            }
            if (this.IsTimelineVisible != oldTimelineVisible)
            {
                propNames.Add(nameof(IsTimelineVisible));
            }

            // TargetProcess, IsAlive 更新時に TargetApp を更新
            if (
                propNames.Contains(nameof(TargetProcess)) ||
                propNames.Contains(nameof(IsAlive)))
            {
                var process = this.IsAlive ? this.TargetProcess : null;
                if (process?.Id != this.TargetApp?.ProcessId)
                {
                    this.TargetApp?.Dispose();
                    this.TargetApp = (process == null) ? null : CreateApp(process);
                }
            }

            return propNames;
        }

        /// <summary>
        /// <see cref="ProcessOperationBase.RunProcess"/> メソッドの実処理を行う。
        /// </summary>
        /// <param name="processFilePath">実行ファイルパス。</param>
        /// <param name="raisePropertyChanged">
        /// プロパティ値変更通知を行うデリゲートの設定先。通知不要ならば null が設定される。
        /// </param>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        protected override Result<bool> RunProcessCore(
            string processFilePath,
            out Action raisePropertyChanged)
        {
            raisePropertyChanged = null;

            if (this.State != YmmState.None)
            {
                // 既に起動しているので何もしない
                return (true, @"既に起動しています。");
            }

            return base.RunProcessCore(processFilePath, out raisePropertyChanged);
        }

        /// <summary>
        /// <see cref="ProcessOperationBase.RunProcessCore"/>
        /// メソッドの既定の実装によってプロセスを起動させた後の処理を行う。
        /// </summary>
        /// <param name="process">起動済みプロセス。製品情報の一致も確認済み。</param>
        /// <param name="raisePropertyChanged">
        /// プロパティ値変更通知を行うデリゲートの設定先。通知不要ならば null が設定される。
        /// </param>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        protected override Result<bool> RunProcessImpl(
            Process process,
            out Action raisePropertyChanged)
        {
            var result = base.RunProcessImpl(process, out raisePropertyChanged);

            if (!result.Value && this.State == YmmState.Startup)
            {
                // 起動途中でも成功扱い
                return true;
            }

            return result;
        }

        /// <summary>
        /// <see cref="ProcessOperationBase.ExitProcess"/> メソッドの実処理を行う。
        /// </summary>
        /// <param name="raisePropertyChanged">
        /// プロパティ値変更通知を行うデリゲートの設定先。通知不要ならば null が設定される。
        /// </param>
        /// <returns>
        /// 成功したならば true 。
        /// 終了通知には成功したがプロセス側で終了が抑止されたならば null 。
        /// 失敗したならば false 。
        /// </returns>
        protected override sealed Result<bool?> ExitProcessCore(
            out Action raisePropertyChanged)
        {
            raisePropertyChanged = null;

            switch (this.State)
            {
            case YmmState.None:
                // 既に終了しているので何もしない
                return (true, @"終了済みです。");

            case YmmState.Startup:
            case YmmState.Cleanup:
            case YmmState.Idle:
            case YmmState.TimelineHidden:
                break;

            default:
                return MakeStateErrorResult<bool?>(false);
            }

            return base.ExitProcessCore(out raisePropertyChanged);
        }

        /// <summary>
        /// 処理対象プロセスに対して終了通知を行う。
        /// </summary>
        /// <param name="targetProcess">処理対象プロセス。</param>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        protected override sealed Result<bool> RequestProcessExit(Process targetProcess)
        {
            // TargetApp を破棄
            this.TargetApp?.Dispose();
            this.TargetApp = null;

            // Cleanup 状態以外ならば終了通知
            return
                (this.State == YmmState.Cleanup) ?
                    true : base.RequestProcessExit(targetProcess);
        }

        /// <summary>
        /// 処理対象プロセスが終了するか終了不可能な状態になるまで待機する。
        /// </summary>
        /// <param name="targetProcess">終了通知成功済みの処理対象プロセス。</param>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        protected override Result<bool> WaitForProcessExitedOrBlocking(Process targetProcess)
        {
            // 終了orブロッキング状態まで待つ
            var done =
                WaitUntil(
                    () =>
                    {
                        if (targetProcess.WaitForExit(0))
                        {
                            return true;
                        }

                        switch (this.CheckState(targetProcess, out _))
                        {
                        case YmmState.None:
                        case YmmState.Blocking:
                            return true;
                        }

                        return false;
                    });

            if (!done)
            {
                return (false, @"終了状態へ遷移しませんでした。");
            }

            return true;
        }

        /// <summary>
        /// <see cref="ExitProcessCore"/>
        /// メソッドの既定の実装によって終了または終了不可状態まで待機した後の処理を行う。
        /// </summary>
        /// <param name="raisePropertyChanged">
        /// プロパティ値変更通知を行うデリゲートの設定先。通知不要ならば null が設定される。
        /// </param>
        /// <returns>
        /// 終了が確認できたならば true 。
        /// 終了不可状態になったことが確認できたならば null 。
        /// 失敗したならば false 。
        /// </returns>
        protected override sealed Result<bool?> ExitProcessImpl(
            out Action raisePropertyChanged)
        {
            base.ExitProcessImpl(out raisePropertyChanged);

            // ベースクラス処理の戻り値は無視して State で判定
            if (this.State == YmmState.Blocking)
            {
                return (null, @"本体側で終了が保留されました。");
            }

            // Startup, Idle, TimelineHidden は終了後即再起動したものと判断

            return true;
        }

        #endregion

        #region IYmmOperation の実装

        /// <summary>
        /// プロセスの状態を取得する。
        /// </summary>
        public YmmState State { get; private set; }

        /// <summary>
        /// タイムラインウィンドウが表示されているか否かを取得する。
        /// </summary>
        /// <remarks>
        /// <see cref="State"/> が <see cref="YmmState.Idle"/> ならば必ず true を返す。
        /// <see cref="State"/> が <see cref="YmmState.Blocking"/> の場合は
        /// true にも false にもなりうる。
        /// それ以外の場合は必ず false を返す。
        /// </remarks>
        public bool IsTimelineVisible { get; private set; }

        /// <summary>
        /// タイムラインウィンドウの表示状態を設定する。
        /// </summary>
        /// <param name="visible">表示させるならば true 。非表示にするならば false 。</param>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        public Result<bool> SetTimelineVisible(bool visible)
        {
            Result<bool> result;
            Action raisePropChanged = null;

            try
            {
                lock (this.LockObject)
                {
                    if (!this.CanOperate)
                    {
                        return this.MakeStateErrorResult(false);
                    }

                    // メニューアイテム取得
                    var (item, itemMessage) = this.GetViewMenuTimelineItem();
                    if (item == null)
                    {
                        return (false, itemMessage);
                    }

                    // チェック状態が目的の表示状態と異なるか？
                    if ((bool)item.IsChecked != visible)
                    {
                        // クリック
                        WpfClicker.Click(item);

                        // 状態更新
                        raisePropChanged = this.UpdateByCurrentTargetProcess();
                    }
                }

                result = true;
            }
            finally
            {
                try
                {
                    raisePropChanged?.Invoke();
                }
                catch (Exception ex)
                {
                    ThreadTrace.WriteException(ex);
                    result = (
                        false,
                        ex.Message ?? (ex.GetType().Name + @" 例外が発生しました。"));
                }
            }

            return result;
        }

        /// <summary>
        /// タイムラインウィンドウのコンボボックスからキャラクターの一覧を取得する。
        /// </summary>
        /// <returns>有効キャラクター配列。取得できなかった場合は null 。</returns>
        /// <remarks>
        /// タイムラインウィンドウが非表示の場合は必ず失敗する。
        /// </remarks>
        public Result<ReadOnlyCollection<string>> GetAvailableCharacters()
        {
            lock (this.LockObject)
            {
                // CanOperate は TimelineHidden も含まれるため、直接 State で判断
                if (this.State != YmmState.Idle)
                {
                    return this.MakeStateErrorResult<ReadOnlyCollection<string>>();
                }

                // コンボボックス取得
                var (combo, comboMessage) = this.GetTimelineCharacterComboBox();
                if (combo == null)
                {
                    return (null, comboMessage);
                }

                try
                {
                    var characters = new List<string>();

                    // 各アイテムの Name プロパティからキャラクター名取得
                    foreach (var item in combo.Items)
                    {
                        characters.Add((string)item.Name);
                    }

                    return characters.AsReadOnly();
                }
                catch (Exception ex)
                {
                    ThreadTrace.WriteException(ex);
                }
            }

            return (null, @"キャラクター一覧の取得に失敗しました。");
        }

        /// <summary>
        /// タイムラインウィンドウのコンボボックスで選択されているキャラクターを取得する。
        /// </summary>
        /// <returns>キャラクター。取得できなかった場合は null 。</returns>
        /// <remarks>
        /// タイムラインウィンドウが非表示の場合は必ず失敗する。
        /// </remarks>
        public Result<string> GetCharacter()
        {
            lock (this.LockObject)
            {
                // CanOperate は TimelineHidden も含まれるため、直接 State で判断
                if (this.State != YmmState.Idle)
                {
                    return this.MakeStateErrorResult<string>();
                }

                // コンボボックス取得
                var (combo, comboMessage) = this.GetTimelineCharacterComboBox();
                if (combo == null)
                {
                    return (null, comboMessage);
                }

                try
                {
                    return (string)combo.Text;
                }
                catch (Exception ex)
                {
                    ThreadTrace.WriteException(ex);
                }
            }

            return (null, @"選択中キャラクターの取得に失敗しました。");
        }

        /// <summary>
        /// タイムラインウィンドウのコンボボックスからキャラクターを選択する。
        /// </summary>
        /// <param name="character">キャラクター。</param>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        /// <remarks>
        /// タイムラインウィンドウが非表示の場合は必ず失敗する。
        /// </remarks>
        public Result<bool> SetCharacter(string character)
        {
            lock (this.LockObject)
            {
                // CanOperate は TimelineHidden も含まれるため、直接 State で判断
                if (this.State != YmmState.Idle)
                {
                    return this.MakeStateErrorResult(false);
                }

                // コンボボックス取得
                var (combo, comboMessage) = this.GetTimelineCharacterComboBox();
                if (combo == null)
                {
                    return (false, comboMessage);
                }

                try
                {
                    // キャラクター名が重複している場合がある
                    // 既に選択中の場合はそのままにしておく
                    if ((string)combo.Text == character)
                    {
                        return true;
                    }

                    // 各アイテムの Name プロパティから一致するキャラクター名を探す
                    int index = 0;
                    foreach (var item in combo.Items)
                    {
                        if ((string)item.Name == character)
                        {
                            // 見つかったので選択
                            combo.SelectedIndex = index;
                            return true;
                        }
                        ++index;
                    }
                }
                catch (Exception ex)
                {
                    ThreadTrace.WriteException(ex);
                    return (false, @"キャラクターの選択に失敗しました。");
                }
            }

            return (false, @"本体にキャラクターが登録されていません。");
        }

        /// <summary>
        /// タイムラインウィンドウのテキストボックスに設定されているセリフを取得する。
        /// </summary>
        /// <returns>セリフ。取得できなかった場合は null 。</returns>
        /// <remarks>
        /// タイムラインウィンドウが非表示の場合は必ず失敗する。
        /// </remarks>
        public Result<string> GetSpeechText()
        {
            lock (this.LockObject)
            {
                // CanOperate は TimelineHidden も含まれるため、直接 State で判断
                if (this.State != YmmState.Idle)
                {
                    return this.MakeStateErrorResult<string>();
                }

                // テキストボックス取得
                var (textBox, textBoxMessage) = this.GetTimelineSpeechTextBox();
                if (textBox == null)
                {
                    return (null, textBoxMessage);
                }

                try
                {
                    return textBox.Text;
                }
                catch (Exception ex)
                {
                    ThreadTrace.WriteException(ex);
                }
            }

            return (null, @"セリフ文の取得に失敗しました。");
        }

        /// <summary>
        /// タイムラインウィンドウのテキストボックスにセリフを設定する。
        /// </summary>
        /// <param name="text">セリフ。</param>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        /// <remarks>
        /// タイムラインウィンドウが非表示の場合は必ず失敗する。
        /// </remarks>
        public Result<bool> SetSpeechText(string text)
        {
            lock (this.LockObject)
            {
                // CanOperate は TimelineHidden も含まれるため、直接 State で判断
                if (this.State != YmmState.Idle)
                {
                    return this.MakeStateErrorResult(false);
                }

                // テキストボックス取得
                var (textBox, textBoxMessage) = this.GetTimelineSpeechTextBox();
                if (textBox == null)
                {
                    return (false, textBoxMessage);
                }

                var speechText = text ?? @"";

                // 500文字あたり1ミリ秒をタイムアウト値に追加
                var timeout = StandardTimeoutMilliseconds + (speechText.Length / 500);

                try
                {
                    // 文章入力欄にテキストを設定
                    textBox.Focus();
                    bool waitOk =
                        AsyncActionWaiter.Wait(
                            async => textBox.Text(async, speechText),
                            timeout);
                    if (!waitOk)
                    {
                        return (false, @"文章設定処理がタイムアウトしました。");
                    }
                }
                catch (Exception ex)
                {
                    ThreadTrace.WriteException(ex);
                    return (false, @"セリフ文の設定に失敗しました。");
                }
            }

            return true;
        }

        /// <summary>
        /// タイムラインウィンドウの追加ボタンをクリックする。
        /// </summary>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        /// <remarks>
        /// タイムラインウィンドウが非表示の場合は必ず失敗する。
        /// </remarks>
        public Result<bool> ClickTimelineAddButton()
        {
            lock (this.LockObject)
            {
                // CanOperate は TimelineHidden も含まれるため、直接 State で判断
                if (this.State != YmmState.Idle)
                {
                    return this.MakeStateErrorResult(false);
                }

                // タイムラインウィンドウ取得
                var tlWin = this.FindWindow(WindowTitleKind.Timeline);
                if (tlWin == null)
                {
                    return (false, @"本体のタイムラインウィンドウが非表示です。");
                }

                // 操作コントロール群ルート取得
                var (root, rootMessage) = this.GetTimelineControlsRoot((DynamicAppVar)tlWin);
                if (root == null)
                {
                    return (false, rootMessage);
                }

                // 追加ボタン取得
                var (button, buttonMessage) = this.GetTimelineAddButton((DynamicAppVar)root);
                if (button == null)
                {
                    return (false, buttonMessage);
                }

                try
                {
                    // クリック
                    var async = new Async();
                    WpfClicker.Click(button, async);

                    // ダイアログが出ずに完了した場合は成功
                    var modalWin = new WindowControl(tlWin).WaitForNextModal(async);
                    if (modalWin != null)
                    {
                        var title = modalWin.GetWindowText();
                        return (false, $@"本体側で{title}ダイアログが表示されました。");
                    }
                }
                catch (Exception ex)
                {
                    ThreadTrace.WriteException(ex);
                    return (false, @"本体の追加ボタンを操作できませんでした。");
                }

                return true;
            }
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
