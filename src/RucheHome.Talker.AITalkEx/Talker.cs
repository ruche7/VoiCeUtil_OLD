using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
        /// 音声ファイル保存ダイアログのウィンドウタイトル。
        /// </summary>
        private const string SaveFileDialogTitle = @"音声ファイルの保存";

        /// <summary>
        /// 音声保存進捗ウィンドウのウィンドウタイトル。
        /// </summary>
        private const string SaveProgressWindowTitle = @"音声保存";

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
        /// <param name="app">操作対象アプリ。</param>
        /// <returns>コントロール。取得できなかった場合は null 。</returns>
        /// <remarks>
        /// Zインデックスによって対象を特定する。
        /// </remarks>
        private static WindowControl GetMainButtonsParent(WindowsAppFriend app)
        {
            try
            {
                return WindowControl.FromZTop(app).IdentifyFromZIndex(2, 0, 0, 1, 0, 1, 0);
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
        /// 文章入力欄コントロールを取得する。
        /// </summary>
        /// <returns>コントロール。取得できなかった場合は null 。</returns>
        private FormsRichTextBox GetMainRichTextBox()
        {
            if (this.TargetApp == null)
            {
                return null;
            }

            try
            {
                return
                    new FormsRichTextBox(
                        WindowControl.FromZTop(this.TargetApp)
                            .IdentifyFromZIndex(2, 0, 0, 1, 0, 1, 1, 1));
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
            WindowsAppFriend app = null;

            try
            {
                var title = process.MainWindowTitle;

                // メインウィンドウタイトルが音声保存関連なら音声保存中
                if (title == SaveFileDialogTitle || title == SaveProgressWindowTitle)
                {
                    return MakeResult(TalkerState.FileSaving);
                }

                // メインウィンドウタイトルがプロセス製品名で始まっていない？
                if (!title.StartsWith(this.ProcessProduct))
                {
                    // 未起動or起動中なら起動中、そうでなければブロッキング中
                    return
                        MakeResult(
                            (this.State == TalkerState.None ||
                             this.State == TalkerState.Startup) ?
                                TalkerState.Startup : TalkerState.Blocking);
                }

                // 操作対象アプリ取得or作成
                // TargetApp とプロセスIDが同じなら TargetApp を使う
                app =
                    (this.TargetApp?.ProcessId == process.Id) ?
                        this.TargetApp : CreateApp(process);

                // 音声保存ボタンを探す
                var saveButtonWin = GetMainButtonsParent(app)?.IdentifyFromZIndex(1);
                if (saveButtonWin?.TypeFullName != @"System.Windows.Forms.Button")
                {
                    return
                        MakeResult(
                            TalkerState.Fail,
                            @"本体の音声保存ボタンが見つかりません。");
                }
                var saveButton = new FormsButton(saveButtonWin);

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
            var textBox = this.GetMainRichTextBox();
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
            var textBox = this.GetMainRichTextBox();
            if (textBox == null)
            {
                return MakeResult(false, @"本体の文章入力欄が見つかりません。");
            }

            // 500文字あたり1ミリ秒をタイムアウト値に追加
            var timeout = StandardTimeoutMilliseconds + (text.Length / 500);

            try
            {
                // テキスト変更を非同期で開始
                var async = new Async();
                textBox.EmulateChangeText(text, async);

                // 完了待機
                for (
                    var sw = Stopwatch.StartNew();
                    !async.IsCompleted && sw.ElapsedMilliseconds < timeout; )
                {
                    Thread.Sleep(1);
                }

                return
                    async.IsCompleted ?
                        MakeResult(true) :
                        MakeResult(
                            false,
                            @"本体の文章入力欄への文章設定処理がタイムアウトしました。");
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
        /// <see cref="ProcessTalkerBase{TParameterId}.CanOperate"/> が
        /// true の時のみ呼び出される。
        /// </para>
        /// <para>
        /// 読み上げ開始の成否を確認するまでブロッキングする。読み上げ完了は待たない。
        /// </para>
        /// </remarks>
        protected override Result<bool> SpeakImpl()
        {
            // TODO: 要実装
            return MakeResult(false, @"未実装です。");
        }

        /// <summary>
        /// 読み上げを停止させる。
        /// </summary>
        /// <returns>成功したか既に停止中ならば true 。そうでなければ false 。</returns>
        /// <remarks>
        /// <para>
        /// <see cref="ProcessTalkerBase{TParameterId}"/> 実装からは、
        /// <see cref="ProcessTalkerBase{TParameterId}.CanOperate"/> が
        /// true の時のみ呼び出される。
        /// </para>
        /// <para>読み上げ停止の成否を確認するまでブロッキングする。</para>
        /// </remarks>
        protected override Result<bool> StopImpl()
        {
            // TODO: 要実装
            return MakeResult(false, @"未実装です。");
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
