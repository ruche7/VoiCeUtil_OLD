using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Codeer.Friendly;
using Codeer.Friendly.Windows;
using Codeer.Friendly.Windows.Grasp;
using Ong.Friendly.FormsStandardControls;
using RucheHome.Util;

using static RucheHome.Util.ArgumentValidater;

namespace RucheHome.Talker.AITalkEx
{
    /// <summary>
    /// AITalkExベースのプロセスを操作する <see cref="IProcessTalker"/> 実装クラス。
    /// </summary>
    /// <remarks>
    /// <para>下記の製品シリーズの一部に対応する。</para>
    /// <list type="bullet">
    /// <item><description>株式会社AHSの VOICEROID+ EX シリーズ</description></item>
    /// <item><description>株式会社インターネットの Talk Ex シリーズ</description></item>
    /// </list>
    /// </remarks>
    public sealed class Talker : ProcessTalkerBase<ParameterId>, IDisposable
    {
        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="product">製品種別。</param>
        private Talker(Product product)
            :
            base(
                product.GetProcessFileName() ?? @"dummy",   // ベースクラスでの例外回避
                product.GetProcessProduct() ?? @"dummy",    // ベースクラスでの例外回避
                product.GetTalkerName(),
                canSaveBlankText: false,
                hasCharacters: false)
        {
            // 例外回避発動時はここで例外になる
            ValidateArgumentInvalidEnum(product, nameof(product));
        }

        /// <summary>
        /// デストラクタ。
        /// </summary>
        ~Talker()
        {
            this.Dispose(false);
        }

        /// <summary>
        /// 製品種別ごとに一意のインスタンスを取得する。
        /// </summary>
        /// <param name="product">製品種別。</param>
        /// <returns><see cref="Talker"/> インスタンス。</returns>
        public static Talker Get(Product product)
        {
            ValidateArgumentInvalidEnum(product, nameof(product));

            return TalkerCache.GetOrAdd(product, p => new Talker(p));
        }

        /// <summary>
        /// インスタンスキャッシュディクショナリを取得する。
        /// </summary>
        private static ConcurrentDictionary<Product, Talker> TalkerCache { get; } =
            new ConcurrentDictionary<Product, Talker>();

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
        private static bool WaitAsyncAction(
            Action<Async> action,
            int timeoutMilliseconds = StandardTimeoutMilliseconds)
        {
            var async = new Async();

            action(async);

            for (
                var sw = Stopwatch.StartNew();
                !async.IsCompleted &&
                (timeoutMilliseconds < 0 || sw.ElapsedMilliseconds < timeoutMilliseconds); )
            {
                Thread.Sleep(1);
            }

            return async.IsCompleted;
        }

        /// <summary>
        /// 操作対象プロセスからアプリインスタンスを生成する。
        /// </summary>
        /// <param name="process">操作対象プロセス。</param>
        /// <returns>操作対象アプリ。引数値が不正ならば null 。</returns>
        private static WindowsAppFriend CreateApp(Process process) =>
            (process?.HasExited == false) ?
                (new WindowsAppFriend(process, @"v2.0.50727")) : null;

        /// <summary>
        /// 文章入力欄下にあるボタン群の親コントロールを取得する。
        /// </summary>
        /// <param name="mainWindow">メインウィンドウ。</param>
        /// <returns>コントロール。取得できなかった場合は null 。</returns>
        /// <remarks>
        /// Zインデックスによって対象を特定する。
        /// </remarks>
        private static WindowControl GetMainButtonsParent(WindowControl mainWindow)
        {
            try
            {
                return mainWindow?.IdentifyFromZIndex(2, 0, 0, 1, 0, 1, 0);
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
            }
            return null;
        }

        /// <summary>
        /// 文章入力欄下にあるボタンの種別列挙。
        /// </summary>
        /// <remarks>
        /// 値がそのままZインデックスを表す。
        /// </remarks>
        private enum MainButton
        {
            /// <summary>
            /// 再生時間
            /// </summary>
            Time = 0,

            /// <summary>
            /// 音声保存
            /// </summary>
            Save = 1,

            /// <summary>
            /// 停止
            /// </summary>
            Stop = 2,

            /// <summary>
            /// 再生/一時停止/再開
            /// </summary>
            Play = 3,
        }

        /// <summary>
        /// 文章入力欄下にあるボタンコントロールを取得する。
        /// </summary>
        /// <param name="parent">ボタン群の親コントロール。</param>
        /// <param name="button">ボタン種別。</param>
        /// <returns>コントロール。取得できなかった場合は null 。</returns>
        private static FormsButton GetMainButton(WindowControl parent, MainButton button)
        {
            if (parent != null)
            {
                try
                {
                    return new FormsButton(parent.IdentifyFromZIndex((int)button));
                }
                catch (Exception ex)
                {
                    ThreadTrace.WriteException(ex);
                }
            }

            return null;
        }

        /// <summary>
        /// 文章入力欄コントロールを取得する。
        /// </summary>
        /// <param name="mainWindow">メインウィンドウ。</param>
        /// <returns>コントロール。取得できなかった場合は null 。</returns>
        private static FormsRichTextBox GetMainRichTextBox(WindowControl mainWindow)
        {
            if (mainWindow == null)
            {
                return null;
            }

            try
            {
                return
                    new FormsRichTextBox(
                        mainWindow.IdentifyFromZIndex(2, 0, 0, 1, 0, 1, 1, 1));
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
            }
            return null;
        }

        /// <summary>
        /// 操作対象アプリを取得または設定する。
        /// </summary>
        private WindowsAppFriend TargetApp { get; set; } = null;

        /// <summary>
        /// 音声ファイル保存ダイアログのウィンドウタイトル。
        /// </summary>
        private const string SaveFileDialogTitle = @"音声ファイルの保存";

        /// <summary>
        /// 音声保存進捗ウィンドウのウィンドウタイトル。
        /// </summary>
        private const string SaveProgressWindowTitle = @"音声保存";

        /// <summary>
        /// ウィンドウタイトル種別列挙。
        /// </summary>
        enum WindowTitleKind
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
        /// ウィンドウタイトル種別を調べる。
        /// </summary>
        /// <param name="title">ウィンドウタイトル。</param>
        /// <returns>ウィンドウタイトル種別。</returns>
        private WindowTitleKind CheckWindowTitleKind(string title)
        {
            if (title == null)
            {
                return WindowTitleKind.Others;
            }

            if (title.Length == 0)
            {
                return WindowTitleKind.StartupOrCleanup;
            }
            if (title == SaveFileDialogTitle || title == SaveProgressWindowTitle)
            {
                return WindowTitleKind.FileSaving;
            }
            if (title.StartsWith(this.ProcessProduct) == true)
            {
                return WindowTitleKind.Main;
            }

            return WindowTitleKind.Others;
        }

        /// <summary>
        /// メインウィンドウを取得する。
        /// </summary>
        /// <returns>メインウィンドウ。取得できなかった場合は null 。</returns>
        private WindowControl GetMainWindow()
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
                                    var kind = this.CheckWindowTitleKind(win.GetWindowText());
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

        #region ProcessTalkerBase<Talker.ParameterId> のオーバライド

        /// <summary>
        /// プロパティ値変更時に呼び出される。
        /// </summary>
        /// <param name="name">プロパティ名。</param>
        /// <param name="newValue">変更後の値。</param>
        /// <param name="oldValue">変更前の値。</param>
        protected override void OnPropertyChanged(
            string name,
            object newValue,
            object oldValue)
        {
            switch (name)
            {
            case nameof(TargetProcess):
                // 操作対象アプリ更新
                {
                    var process = newValue as Process;
                    if (process?.Id != this.TargetApp?.ProcessId)
                    {
                        this.TargetApp?.Dispose();
                        this.TargetApp = (process == null) ? null : CreateApp(process);
                    }
                }
                break;
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
        /// <remarks>
        /// このメソッドの戻り値によって
        /// <see cref="ProcessTalkerBase{TParameterId}.State"/> プロパティ等が更新される。
        /// 状態値が <see cref="TalkerState.Fail"/> の場合は付随メッセージも利用される。
        /// </remarks>
        protected override Result<TalkerState> CheckState(Process process)
        {
            // ウィンドウタイトルから状態を決定するローカルメソッド
            TalkerState? decideStateByWindowTitle(string title)
            {
                switch (this.CheckWindowTitleKind(title))
                {
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

                return null;
            }

            WindowsAppFriend app = null;

            try
            {
                // メインウィンドウタイトルから状態決定を試みる
                var state = decideStateByWindowTitle(process.MainWindowTitle);
                if (state.HasValue)
                {
                    return MakeResult(state.Value);
                }

                // 操作対象アプリ取得or作成
                // TargetApp とプロセスIDが同じなら TargetApp を使う
                app =
                    (this.TargetApp?.ProcessId == process.Id) ?
                        this.TargetApp : CreateApp(process);

                // トップレベルウィンドウ取得
                var topWin = WindowControl.FromZTop(app);

                // トップレベルウィンドウタイトルから状態決定を試みる
                state = decideStateByWindowTitle(topWin.GetWindowText());
                if (state.HasValue)
                {
                    return MakeResult(state.Value);
                }

                // 音声保存ボタンを探す
                var saveButton = GetMainButton(GetMainButtonsParent(topWin), MainButton.Save);
                if (saveButton == null)
                {
                    // ウィンドウ構築途中or破棄途中であると判断
                    // 即ち起動中or終了中
                    return
                        MakeResult(
                            (this.State == TalkerState.None ||
                             this.State == TalkerState.Startup) ?
                                TalkerState.Startup : TalkerState.Cleanup);
                }

                // 音声保存ボタンが無効ならば読み上げ中と判断する
                return
                    MakeResult(
                        saveButton.Enabled ? TalkerState.Idle : TalkerState.Speaking);
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
                return MakeResult(
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
        /// 現在設定されている文章を取得する。
        /// </summary>
        /// <returns>文章。取得できなかった場合は null 。</returns>
        /// <remarks>
        /// <see cref="ProcessTalkerBase{TParameterId}"/> 実装からは、
        /// <see cref="ProcessTalkerBase{TParameterId}.CanOperate"/> が
        /// true の時のみ呼び出される。
        /// </remarks>
        protected override Result<string> GetTextImpl()
        {
            // メインウィンドウを取得
            var mainWin = this.GetMainWindow();
            if (mainWin == null)
            {
                return MakeResult<string>(message: @"本体のウィンドウが見つかりません。");
            }

            // 文章入力欄を取得
            var textBox = GetMainRichTextBox(mainWin);
            if (textBox == null)
            {
                return MakeResult<string>(message: @"本体の文章入力欄が見つかりません。");
            }

            try
            {
                return MakeResult(textBox.Text);
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
            }
            return
                MakeResult<string>(
                    message: @"本体の文章入力欄から文章を取得できませんでした。");
        }

        /// <summary>
        /// 文章を設定する。
        /// </summary>
        /// <param name="text">
        /// 文章。
        /// <see cref="ProcessTalkerBase{TParameterId}"/> 実装から null が渡されることはない。
        /// </param>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        /// <remarks>
        /// <see cref="ProcessTalkerBase{TParameterId}"/> 実装からは、
        /// <see cref="ProcessTalkerBase{TParameterId}.CanOperate"/> が
        /// true の時のみ呼び出される。
        /// </remarks>
        protected override Result<bool> SetTextImpl(string text)
        {
            // メインウィンドウを取得
            var mainWin = this.GetMainWindow();
            if (mainWin == null)
            {
                return MakeResult(false, @"本体のウィンドウが見つかりません。");
            }

            // 文章入力欄を取得
            var textBox = GetMainRichTextBox(mainWin);
            if (textBox == null)
            {
                return MakeResult(false, @"本体の文章入力欄が見つかりません。");
            }

            // 500文字あたり1ミリ秒をタイムアウト値に追加
            var timeout = StandardTimeoutMilliseconds + (text.Length / 500);

            try
            {
                bool done =
                    WaitAsyncAction(
                        async => textBox.EmulateChangeText(text, async),
                        timeout);

                return
                    MakeResult(
                        done,
                        done ?
                            null :
                            @"文章設定処理がタイムアウトしました。");
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
            }
            return MakeResult(false, @"本体の文章入力欄に文章を設定できませんでした。");
        }

        /// <summary>
        /// 現在のパラメータ一覧を取得する。
        /// </summary>
        /// <returns>
        /// パラメータIDとその値のディクショナリ。取得できなかった場合は null 。
        /// </returns>
        /// <remarks>
        /// <see cref="ProcessTalkerBase{TParameterId}"/> 実装からは、
        /// <see cref="ProcessTalkerBase{TParameterId}.CanOperate"/> が
        /// true の時のみ呼び出される。
        /// </remarks>
        protected override Result<Dictionary<ParameterId, decimal>> GetParametersImpl()
        {
            // TODO: 要実装
            return MakeResult<Dictionary<ParameterId, decimal>>(message: @"未実装です。");
        }

        /// <summary>
        /// パラメータ群を設定する。
        /// </summary>
        /// <param name="parameters">
        /// 設定するパラメータIDとその値の列挙。
        /// <see cref="ProcessTalkerBase{TParameterId}"/> 実装から null が渡されることはない。
        /// </param>
        /// <returns>
        /// 個々のパラメータIDとその設定成否を保持するディクショナリ。
        /// 処理を行えない状態ならば null 。
        /// </returns>
        /// <remarks>
        /// <para>
        /// <see cref="ProcessTalkerBase{TParameterId}"/> 実装からは、
        /// <see cref="ProcessTalkerBase{TParameterId}.CanOperate"/> が
        /// true の時のみ呼び出される。
        /// </para>
        /// <para>設定処理自体行わなかったパラメータIDは戻り値のキーに含めないこと。</para>
        /// </remarks>
        protected override Result<Dictionary<ParameterId, Result<bool>>> SetParametersImpl(
            IEnumerable<KeyValuePair<ParameterId, decimal>> parameters)
        {
            // TODO: 要実装
            return MakeResult<Dictionary<ParameterId, Result<bool>>>(message: @"未実装です。");
        }

        /// <summary>
        /// 現在の文章の読み上げを開始させる。
        /// </summary>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        /// <remarks>
        /// <para>
        /// <see cref="ProcessTalkerBase{TParameterId}"/> 実装からは、
        /// <see cref="ProcessTalkerBase{TParameterId}.State"/> が
        /// <see cref="TalkerState.Idle"/> または
        /// <see cref="TalkerState.Speaking"/> の時のみ呼び出される。
        /// </para>
        /// <para>
        /// <see cref="ProcessTalkerBase{TParameterId}.State"/> が
        /// <see cref="TalkerState.Speaking"/> の場合、事前に呼び出し元で
        /// <see cref="StopImpl"/> を呼び出し、その成功を確認済みの状態で呼び出される。
        /// </para>
        /// <para>
        /// 読み上げ開始の成否を確認するまでブロッキングする。読み上げ完了は待たない。
        /// </para>
        /// </remarks>
        protected override Result<bool> SpeakImpl()
        {
            // メインウィンドウを取得
            var mainWin = this.GetMainWindow();
            if (mainWin == null)
            {
                return MakeResult(false, @"本体のウィンドウが見つかりません。");
            }

            // ボタン群の親を取得
            var parent = GetMainButtonsParent(mainWin);
            if (parent == null)
            {
                return MakeResult(false, @"本体のボタンが見つかりません。");
            }

            // 再生ボタン取得
            var play = GetMainButton(parent, MainButton.Play);
            if (play == null)
            {
                return MakeResult(false, @"本体の再生ボタンが見つかりません。");
            }

            try
            {
                if (!play.Enabled)
                {
                    return MakeResult(false, @"本体の再生ボタンが押せない状態です。");
                }

                // 再生ボタン押下
                var async = new Async();
                play.EmulateClick(async);

                // フレーズ編集未保存の場合等はダイアログが出るためそれを待つ
                // ダイアログが出ずに完了した場合は成功
                if (mainWin.WaitForNextModal(async) != null)
                {
                    return MakeResult(false, @"本体側でダイアログが表示されました。。");
                }
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
                return MakeResult(false, @"本体の再生ボタンを押下できませんでした。");
            }

            return MakeResult(true);
        }

        /// <summary>
        /// 読み上げを停止させる。
        /// </summary>
        /// <returns>成功したか既に停止中ならば true 。そうでなければ false 。</returns>
        /// <remarks>
        /// <para>
        /// <see cref="ProcessTalkerBase{TParameterId}"/> 実装からは、
        /// <see cref="ProcessTalkerBase{TParameterId}.State"/> が
        /// <see cref="TalkerState.Speaking"/> の時のみ呼び出される。
        /// </para>
        /// <para>読み上げ停止の成否を確認するまでブロッキングする。</para>
        /// </remarks>
        protected override Result<bool> StopImpl()
        {
            // メインウィンドウを取得
            var mainWin = this.GetMainWindow();
            if (mainWin == null)
            {
                return MakeResult(false, @"本体のウィンドウが見つかりません。");
            }

            // ボタン群の親を取得
            var parent = GetMainButtonsParent(mainWin);
            if (parent == null)
            {
                return MakeResult(false, @"本体のボタンが見つかりません。");
            }

            // 停止ボタン取得
            var stop = GetMainButton(parent, MainButton.Stop);
            if (stop == null)
            {
                return MakeResult(false, @"本体の停止ボタンが見つかりません。");
            }

            try
            {
                if (!stop.Enabled)
                {
                    return MakeResult(false, @"本体の停止ボタンが押せない状態です。");
                }

                // 停止ボタン押下
                if (!WaitAsyncAction(stop.EmulateClick))
                {
                    return MakeResult(false, @"停止処理がタイムアウトしました。");
                }
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
                return MakeResult(false, @"本体の停止ボタンを押下できませんでした。");
            }

            return MakeResult(true);
        }

        /// <summary>
        /// 現在の文章の音声ファイル保存を行わせる。
        /// </summary>
        /// <param name="filePath">音声ファイルの保存先希望パス。 null も渡されうる。</param>
        /// <returns>
        /// 実際に保存された音声ファイルのパス。分割されている場合はそのうちの1ファイル。
        /// 保存に失敗した場合は null 。
        /// </returns>
        /// <remarks>
        /// <para>
        /// <see cref="ProcessTalkerBase{TParameterId}"/> 実装からは、
        /// <see cref="ProcessTalkerBase{TParameterId}.CanOperate"/> が
        /// true の時のみ呼び出される。
        /// </para>
        /// <para>音声ファイル保存の成否を確認するまでブロッキングする。</para>
        /// </remarks>
        protected override Result<string> SaveFileImpl(string filePath)
        {
            // TODO: 要実装
            return MakeResult<string>(message: @"未実装です。");
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
        private void Dispose(bool disposing)
        {
            this.TargetApp?.Dispose();
            this.TargetApp = null;
        }

        #endregion
    }
}
