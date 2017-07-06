using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Codeer.Friendly;
using Codeer.Friendly.Windows.Grasp;
using Ong.Friendly.FormsStandardControls;
using RucheHome.Diagnostics;

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
    public sealed class Talker : FriendlyProcessTalkerBase<ParameterId>
    {
        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="product">製品種別。</param>
        private Talker(Product product)
            :
            base(
                ClrVersion.V2,
                product.GetProcessFileName() ?? @"dummy",   // ベースクラスでの例外回避
                product.GetProcessProduct() ?? @"dummy",    // ベースクラスでの例外回避
                product.GetTalkerName(),
                canSaveBlankText: false,
                hasCharacters: false)
        {
            // 例外回避発動時はここで例外になる
            ArgumentValidation.IsEnumDefined(product, nameof(product));
        }

        /// <summary>
        /// 製品種別ごとに一意のインスタンスを取得する。
        /// </summary>
        /// <param name="product">製品種別。</param>
        /// <returns><see cref="Talker"/> インスタンス。</returns>
        public static Talker Get(Product product)
        {
            ArgumentValidation.IsEnumDefined(product, nameof(product));

            Talker talker = null;

            lock (TalkerCacheLock)
            {
                talker = TalkerCache.GetOrAdd(product, p => new Talker(p));

                // 破棄済みなら新しいインスタンスに差し換え
                if (talker?.IsDisposed != false)
                {
                    talker = new Talker(product);
                    TalkerCache[product] = talker;
                }
            }

            return talker;
        }

        /// <summary>
        /// キャッシュされている全インスタンスを破棄する。
        /// </summary>
        public static void DisposeAll()
        {
            lock (TalkerCacheLock)
            {
                foreach (var kv in TalkerCache)
                {
                    kv.Value?.Dispose();
                }

                TalkerCache.Clear();
            }
        }

        /// <summary>
        /// インスタンスキャッシュディクショナリを取得する。
        /// </summary>
        private static ConcurrentDictionary<Product, Talker> TalkerCache { get; } =
            new ConcurrentDictionary<Product, Talker>();

        /// <summary>
        /// <see cref="TalkerCache"/> の排他制御用オブジェクト。
        /// </summary>
        private static readonly object TalkerCacheLock = new object();

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
        /// 文章入力欄下にあるボタン群の親コントロールを取得する。
        /// </summary>
        /// <param name="mainWindow">メインウィンドウ。</param>
        /// <returns>コントロール。取得できなかった場合は null 。</returns>
        /// <remarks>
        /// Zインデックスによって対象を特定する。
        /// </remarks>
        private static WindowControl GetMainButtonsParent(WindowControl mainWindow) =>
            GetControlFromZIndex<WindowControl>(mainWindow, 2, 0, 0, 1, 0, 1, 0);

        /// <summary>
        /// 文章入力欄下にあるボタンコントロールを取得する。
        /// </summary>
        /// <param name="parent">ボタン群の親コントロール。</param>
        /// <param name="button">ボタン種別。</param>
        /// <returns>コントロール。取得できなかった場合は null 。</returns>
        private static FormsButton GetMainButton(WindowControl parent, MainButton button) =>
            GetControlFromZIndex<FormsButton>(parent, (int)button);

        /// <summary>
        /// 文章入力欄コントロール群を取得する。
        /// </summary>
        /// <param name="mainWindow">メインウィンドウ。</param>
        /// <returns>コントロール群。取得できなかった場合は null 。</returns>
        /// <remarks>
        /// 複数の文章入力欄が存在する状態になることがあるため、すべて取得する。
        /// </remarks>
        private static FormsRichTextBox[] GetMainRichTextBoxes(WindowControl mainWindow)
        {
            var parent = GetControlFromZIndex<WindowControl>(mainWindow, 2, 0, 0, 1, 0, 1, 1);
            if (parent == null)
            {
                return null;
            }

            var textBoxes = new List<FormsRichTextBox>();

            // Zインデックス順にすべての子を調べる
            for (int zi = 0; ; ++zi)
            {
                try
                {
                    var c = parent.IdentifyFromZIndex(zi);

                    // ウィンドウクラス名に "RichEdit" が含まれるか否かで判断
                    if (c.WindowClassName.Contains(@"RichEdit"))
                    {
                        textBoxes.Add(new FormsRichTextBox(c));
                    }
                }
                catch (WindowIdentifyException)
                {
                    // Zインデックス範囲外だとここに来る
                    break;
                }
                catch (Exception ex)
                {
                    ThreadTrace.WriteException(ex);
                    return null;
                }
            }

            return (textBoxes.Count > 0) ? textBoxes.ToArray() : null;
        }

        /// <summary>
        /// ウィンドウ下部のタブコントロールを取得する。
        /// </summary>
        /// <param name="mainWindow">メインウィンドウ。</param>
        /// <returns>コントロール。取得できなかった場合は null 。</returns>
        private static FormsTabControl GetTabControl(WindowControl mainWindow) =>
            GetControlFromZIndex<FormsTabControl>(mainWindow, 2, 0, 0, 0, 0);

        /// <summary>
        /// ウィンドウ下部のタブコントロールから、
        /// パラメータを保持するテキストボックスのディクショナリを取得する。
        /// </summary>
        /// <param name="tabControl">タブコントロール。</param>
        /// <returns>
        /// テキストボックスのディクショナリ。取得できなかった場合は null 。
        /// </returns>
        private static Result<Dictionary<ParameterId, FormsTextBox>>
        GetTabControlParameterTextBoxes(FormsTabControl tabControl)
        {
            if (tabControl == null)
            {
                return (null, @"本体のタブコントロールが見つかりません。");
            }

            var dict = new Dictionary<ParameterId, FormsTextBox>();
            int tabIndex = -1;

            try
            {
                // 元のタブインデックスを保存
                tabIndex = tabControl.SelectedIndex;

                // 音声効果タブ
                {
                    var name = @"音声効果";

                    // 音声効果タブページ取得
                    var page = FindTabPage(tabControl, name);
                    if (page == null)
                    {
                        // 一度も開いていない場合は取得できないので開いてみる
                        if (!WaitAsyncAction(async => tabControl.EmulateTabSelect(2, async)))
                        {
                            return (null, name + @"タブ選択処理がタイムアウトしました。");
                        }
                        tabControl.Refresh();
                        page = FindTabPage(tabControl, name);
                    }
                    var panel = GetControlFromZIndex<WindowControl>(page, 0);
                    if (panel == null)
                    {
                        return (null, $@"本体の{name}タブが見つかりません。");
                    }

                    // 各テキストボックス取得
                    dict.Add(
                        ParameterId.Volume,
                        GetControlFromZIndex<FormsTextBox>(panel, 8));
                    dict.Add(
                        ParameterId.Speed,
                        GetControlFromZIndex<FormsTextBox>(panel, 9));
                    dict.Add(
                        ParameterId.Tone,
                        GetControlFromZIndex<FormsTextBox>(panel, 10));
                    dict.Add(
                        ParameterId.Intonation,
                        GetControlFromZIndex<FormsTextBox>(panel, 11));
                    if (dict.Any(kv => kv.Value == null))
                    {
                        return (null, $@"本体の{name}タブ内の数値入力欄が見つかりません。");
                    }
                }

                // ポーズタブ
                {
                    var name = @"ポーズ";

                    // ポーズタブページ取得
                    var page = FindTabPage(tabControl, name);
                    if (page == null)
                    {
                        // 一度も開いていない場合は取得できないので開いてみる
                        if (!WaitAsyncAction(async => tabControl.EmulateTabSelect(3, async)))
                        {
                            return (null, name + @"タブ選択処理がタイムアウトしました。");
                        }
                        tabControl.Refresh();
                        page = FindTabPage(tabControl, name);
                    }
                    var panel = GetControlFromZIndex<WindowControl>(page, 0);
                    if (panel == null)
                    {
                        return (null, $@"本体の{name}タブが見つかりません。");
                    }

                    // 各テキストボックス取得
                    dict.Add(
                        ParameterId.PauseShort,
                        GetControlFromZIndex<FormsTextBox>(panel, 3, 0));
                    dict.Add(
                        ParameterId.PauseLong,
                        GetControlFromZIndex<FormsTextBox>(panel, 5, 0));
                    dict.Add(
                        ParameterId.PauseSentence,
                        GetControlFromZIndex<FormsTextBox>(panel, 1, 0));
                    dict.Add(
                        ParameterId.PauseBegin,
                        GetControlFromZIndex<FormsTextBox>(panel, 7, 0));
                    dict.Add(
                        ParameterId.PauseEnd,
                        GetControlFromZIndex<FormsTextBox>(panel, 8, 0));
                    if (dict.Any(kv => kv.Value == null))
                    {
                        return (null, $@"本体の{name}タブ内の数値入力欄が見つかりません。");
                    }
                }
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
                return (null, @"パラメータの取得に失敗しました。");
            }
            finally
            {
                // 元のタブページに戻す
                // 失敗してもよい
                try
                {
                    if (tabIndex >= 0 && tabIndex != tabControl.SelectedIndex)
                    {
                        WaitAsyncAction(
                            async => tabControl.EmulateTabSelect(tabIndex, async));
                    }
                }
                catch (Exception ex)
                {
                    ThreadDebug.WriteException(ex);
                }
            }

            return dict;
        }

        /// <summary>
        /// 音声ファイル保存ダイアログのウィンドウタイトル。
        /// </summary>
        private const string SaveFileDialogTitle = @"音声ファイルの保存";

        /// <summary>
        /// 音声保存進捗ウィンドウのウィンドウタイトル。
        /// </summary>
        private const string SaveProgressWindowTitle = @"音声保存";

        #region FriendlyProcessTalkerBase<Talker.ParameterId> のオーバライド

        /// <summary>
        /// ウィンドウタイトル種別を調べる。
        /// </summary>
        /// <param name="title">ウィンドウタイトル。</param>
        /// <returns>ウィンドウタイトル種別。</returns>
        protected override WindowTitleKind CheckWindowTitleKind(string title)
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
            if (title.StartsWith(this.ProcessProduct))
            {
                return WindowTitleKind.Main;
            }

            return WindowTitleKind.Others;
        }

        /// <summary>
        /// メインウィンドウがトップレベルである前提で、操作対象アプリの状態を調べる。
        /// </summary>
        /// <param name="mainWindow">メインウィンドウ。必ずトップレベル。</param>
        /// <returns>状態値。</returns>
        protected override Result<TalkerState> CheckState(WindowControl mainWindow)
        {
            // 音声保存ボタンを探す
            var saveButton = GetMainButton(GetMainButtonsParent(mainWindow), MainButton.Save);
            if (saveButton == null)
            {
                // ウィンドウ構築途中or破棄途中であると判断
                // 即ち起動中or終了中
                return
                    (this.State == TalkerState.None || this.State == TalkerState.Startup) ?
                        TalkerState.Startup : TalkerState.Cleanup;
            }

            // 音声保存ボタンが無効ならば読み上げ中と判断する
            return (saveButton.Enabled ? TalkerState.Idle : TalkerState.Speaking);
        }

        #endregion

        #region ProcessTalkerBase<Talker.ParameterId> のオーバライド

        /// <summary>
        /// 現在設定されている文章を取得する。
        /// </summary>
        /// <returns>文章。取得できなかった場合は null 。</returns>
        protected override Result<string> GetTextImpl()
        {
            // メインウィンドウを検索
            var mainWin = this.FindMainWindow();
            if (mainWin == null)
            {
                return (null, @"本体のウィンドウが見つかりません。");
            }

            // 文章入力欄を取得
            var textBoxes = GetMainRichTextBoxes(mainWin);
            if (textBoxes == null || textBoxes.Length == 0)
            {
                return (null, @"本体の文章入力欄が見つかりません。");
            }

            try
            {
                // 末尾の文章入力欄からテキストを得る
                return textBoxes[textBoxes.Length - 1].Text;
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
            }
            return (null, @"本体の文章入力欄から文章を取得できませんでした。");
        }

        /// <summary>
        /// 文章を設定する。
        /// </summary>
        /// <param name="text">
        /// 文章。
        /// <see cref="ProcessTalkerBase{TParameterId}"/> 実装から null が渡されることはない。
        /// </param>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        protected override Result<bool> SetTextImpl(string text)
        {
            // メインウィンドウを検索
            var mainWin = this.FindMainWindow();
            if (mainWin == null)
            {
                return (false, @"本体のウィンドウが見つかりません。");
            }

            // 文章入力欄を取得
            var textBoxes = GetMainRichTextBoxes(mainWin);
            if (textBoxes == null || textBoxes.Length == 0)
            {
                return (false, @"本体の文章入力欄が見つかりません。");
            }

            // 500文字あたり1ミリ秒をタイムアウト値に追加
            var timeout = StandardTimeoutMilliseconds + (text.Length / 500);

            try
            {
                // すべての文章入力欄にテキストを設定
                foreach (var textBox in textBoxes)
                {
                    bool done =
                        WaitAsyncAction(
                            async => textBox.EmulateChangeText(text, async),
                            timeout);
                    if (!done)
                    {
                        return (false, @"文章設定処理がタイムアウトしました。");
                    }
                }
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
                return (false, @"本体の文章入力欄に文章を設定できませんでした。");
            }

            return true;
        }

        /// <summary>
        /// 現在のパラメータ一覧を取得する。
        /// </summary>
        /// <returns>
        /// パラメータIDとその値のディクショナリ。取得できなかった場合は null 。
        /// </returns>
        protected override Result<Dictionary<ParameterId, decimal>> GetParametersImpl()
        {
            // メインウィンドウを検索
            var mainWin = this.FindMainWindow();
            if (mainWin == null)
            {
                return (null, @"本体のウィンドウが見つかりません。");
            }

            // タブコントロールを取得
            var tabControl = GetTabControl(mainWin);
            if (tabControl == null)
            {
                return (null, @"本体のタブページが見つかりません。");
            }

            // パラメータテキストボックス群を取得
            var (textBoxes, failMessage) = GetTabControlParameterTextBoxes(tabControl);
            if (textBoxes == null)
            {
                return (null, failMessage);
            }

            var dict = new Dictionary<ParameterId, decimal>();

            try
            {
                foreach (var kv in textBoxes)
                {
                    if (!decimal.TryParse(kv.Value.Text, out var d))
                    {
                        return (null, kv.Key.GetInfo().DisplayName + @"の値が不正です。");
                    }
                    dict.Add(kv.Key, d);
                }
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
                return (null, @"パラメータの取得に失敗しました。");
            }

            return dict;
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
        protected override Result<Dictionary<ParameterId, Result<bool>>> SetParametersImpl(
            IEnumerable<KeyValuePair<ParameterId, decimal>> parameters)
        {
            // メインウィンドウを検索
            var mainWin = this.FindMainWindow();
            if (mainWin == null)
            {
                return (null, @"本体のウィンドウが見つかりません。");
            }

            // タブコントロールを取得
            var tabControl = GetTabControl(mainWin);
            if (tabControl == null)
            {
                return (null, @"本体のタブページが見つかりません。");
            }

            // パラメータテキストボックス群を取得
            var (textBoxes, failMessage) = GetTabControlParameterTextBoxes(tabControl);
            if (textBoxes == null)
            {
                return (null, failMessage);
            }

            var dict = new Dictionary<ParameterId, Result<bool>>();

            foreach (var kv in parameters)
            {
                // 存在するパラメータのみ設定
                if (!textBoxes.TryGetValue(kv.Key, out var textBox))
                {
                    continue;
                }

                var id = kv.Key;
                var value = kv.Value;
                var info = id.GetInfo();
                var format = @"F" + info.Digits;

                // 範囲チェック
                if (value < info.MinValue)
                {
                    dict.Add(
                        id,
                        (
                            false,
                            $@"最小許容値 {info.MinValue.ToString(format)} " +
                            $@"より小さい値 {value.ToString(format)} は設定できません。"
                        ));
                    continue;
                }
                if (value > info.MaxValue)
                {
                    dict.Add(
                        id,
                        (
                            false,
                            $@"最大許容値 {info.MaxValue.ToString(format)} " +
                            $@"より大きい値 {value.ToString(format)} は設定できません。"
                        ));
                    continue;
                }

                try
                {
                    var text = value.ToString(format);
                    var ok = WaitAsyncAction(async => textBox.EmulateChangeText(text, async));
                    dict.Add(
                        id,
                        (
                            ok,
                            ok ?
                                null :
                                (info.DisplayName + @"設定処理がタイムアウトしました。")
                        ));
                }
                catch (Exception ex)
                {
                    ThreadTrace.WriteException(ex);
                    dict.Add(
                        id,
                        (
                            false,
                            ex.Message ?? (ex.GetType().Name + @" 例外が発生しました。")
                        ));
                }
            }

            // テキストボックスにフォーカスがあるうちはスライダーが更新されないので
            // タブコントロールにフォーカスを移す
            // 失敗してもよい
            try
            {
                tabControl.SetFocus();
            }
            catch (Exception ex)
            {
                ThreadDebug.WriteException(ex);
            }

            return dict;
        }

        /// <summary>
        /// 現在の文章の読み上げを開始させる。
        /// </summary>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        protected override Result<bool> SpeakImpl()
        {
            // メインウィンドウを検索
            var mainWin = this.FindMainWindow();
            if (mainWin == null)
            {
                return (false, @"本体のウィンドウが見つかりません。");
            }

            // ボタン群の親を取得
            var parent = GetMainButtonsParent(mainWin);
            if (parent == null)
            {
                return (false, @"本体のボタンが見つかりません。");
            }

            // 再生ボタン取得
            var play = GetMainButton(parent, MainButton.Play);
            if (play == null)
            {
                return (false, @"本体の再生ボタンが見つかりません。");
            }

            try
            {
                if (!play.Enabled)
                {
                    return (false, @"本体の再生ボタンがクリックできない状態です。");
                }

                // 再生ボタンクリック
                var async = new Async();
                play.EmulateClick(async);

                // フレーズ編集未保存の場合等はダイアログが出るためそれを待つ
                // ダイアログが出ずに完了した場合は成功
                var modalWin = mainWin.WaitForNextModal(async);
                if (modalWin != null)
                {
                    var title = modalWin.GetWindowText();
                    return (false, $@"本体側で{title}ダイアログが表示されました。");
                }
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
                return (false, @"本体の再生ボタンをクリックできませんでした。");
            }

            return true;
        }

        /// <summary>
        /// 読み上げを停止させる。
        /// </summary>
        /// <returns>成功したか既に停止中ならば true 。そうでなければ false 。</returns>
        protected override Result<bool> StopImpl()
        {
            // メインウィンドウを検索
            var mainWin = this.FindMainWindow();
            if (mainWin == null)
            {
                return (false, @"本体のウィンドウが見つかりません。");
            }

            // ボタン群の親を取得
            var parent = GetMainButtonsParent(mainWin);
            if (parent == null)
            {
                return (false, @"本体のボタンが見つかりません。");
            }

            // 停止ボタン取得
            var stop = GetMainButton(parent, MainButton.Stop);
            if (stop == null)
            {
                return (false, @"本体の停止ボタンが見つかりません。");
            }

            try
            {
                if (!stop.Enabled)
                {
                    return (false, @"本体の停止ボタンがクリックできない状態です。");
                }

                // 停止ボタンクリック
                if (!WaitAsyncAction(stop.EmulateClick))
                {
                    return (false, @"停止処理がタイムアウトしました。");
                }
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
                return (false, @"本体の停止ボタンをクリックできませんでした。");
            }

            return true;
        }

        /// <summary>
        /// 現在の文章の音声ファイル保存を行わせる。
        /// </summary>
        /// <param name="filePath">
        /// 音声ファイルの保存先希望パス。
        /// <see cref="ProcessTalkerBase{TParameterId}"/>
        /// 実装からは <see cref="Path.GetFullPath"/> に成功したフルパスが渡される。
        /// また、親ディレクトリは必ず作成済みとなる。
        /// </param>
        /// <returns>
        /// 実際に保存された音声ファイルのパス。分割されている場合はそのうちの1ファイル。
        /// 保存に失敗した場合は null 。
        /// </returns>
        protected override Result<string> SaveFileImpl(string filePath)
        {
            // 拡張子 ".wav" のファイルパス作成
            var waveFilePath = filePath;
            if (Path.GetExtension(waveFilePath).ToLower() != @".wav")
            {
                waveFilePath += @".wav";
            }

            // メインウィンドウを検索
            var mainWin = this.FindMainWindow();
            if (mainWin == null)
            {
                return (null, @"本体のウィンドウが見つかりません。");
            }

            var saveButtonAsync = new Async();

            // 音声保存ボタンをクリックして音声ファイル保存ダイアログを取得
            var (fileDialog, fileDialogMessage) =
                this.SaveFileImpl_ClickSaveButton(mainWin, saveButtonAsync);
            if (fileDialog == null)
            {
                return (null, fileDialogMessage);
            }

            // 音声ファイル保存ダイアログにファイルパスを設定してOKボタンをクリック
            var result = this.SaveFileImpl_InputFilePath(fileDialog, waveFilePath);
            if (!result.Value)
            {
                return (null, result.Message);
            }

            // 音声保存処理完了待ち
            result = this.SaveFileImpl_WaitSaving(mainWin, saveButtonAsync);
            if (!result.Value)
            {
                return (null, result.Message);
            }

            // ファイル保存確認
            result = this.SaveFileImpl_CheckFileSaved(waveFilePath);
            if (!result.Value)
            {
                return (null, result.Message);
            }

            return waveFilePath;
        }

        /// <summary>
        /// 音声保存ボタンをクリックし、音声ファイル保存ダイアログを取得する。
        /// </summary>
        /// <param name="mainWindow">メインウィンドウ。</param>
        /// <param name="saveButtonAsync">
        /// 音声保存ボタンクリック処理用の非同期オブジェクト。
        /// </param>
        /// <returns>音声ファイル保存ダイアログ。表示されなかった場合は null 。</returns>
        private Result<WindowControl> SaveFileImpl_ClickSaveButton(
            WindowControl mainWindow,
            Async saveButtonAsync)
        {
            Debug.Assert(mainWindow != null);
            Debug.Assert(saveButtonAsync != null);

            // ボタン群の親を取得
            var parent = GetMainButtonsParent(mainWindow);
            if (parent == null)
            {
                return (null, @"本体のボタンが見つかりません。");
            }

            // 音声保存ボタン取得
            var button = GetMainButton(parent, MainButton.Save);
            if (button == null)
            {
                return (null, message: @"本体の音声保存ボタンが見つかりません。");
            }

            WindowControl fileDialog = null;

            try
            {
                if (!button.Enabled)
                {
                    return (null, @"本体の音声保存ボタンがクリックできない状態です。");
                }

                // 音声保存ボタンクリック
                button.EmulateClick(saveButtonAsync);

                // ファイルダイアログ(or 警告ダイアログ)を待つ
                fileDialog = mainWindow.WaitForNextModal(saveButtonAsync);
                if (fileDialog == null)
                {
                    // 空白文だとダイアログが出ない
                    return (null, @"空白文を音声保存することはできません。");
                }

                // 音声ファイル保存ダイアログか？
                var title = fileDialog.GetWindowText();
                if (title != SaveFileDialogTitle)
                {
                    return (null, $@"本体側で{title}ダイアログが表示されました。");
                }
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
                return (null, @"本体の音声保存ボタンをクリックできませんでした。");
            }

            return fileDialog;
        }

        /// <summary>
        /// 音声ファイル保存ダイアログにファイルパスを設定して保存ボタンをクリックする。
        /// </summary>
        /// <param name="fileDialog">音声ファイル保存ダイアログ。</param>
        /// <param name="filePath">音声ファイルパス。</param>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        private Result<bool> SaveFileImpl_InputFilePath(
            WindowControl fileDialog,
            string filePath)
        {
            Debug.Assert(fileDialog != null);
            Debug.Assert(!string.IsNullOrEmpty(filePath));

            // ファイル名コンボボックス、決定ボタンを取得
            var fileNameCombo = GetFileDialogFileNameComboBox(fileDialog);
            if (fileNameCombo == null)
            {
                return (false, @"ダイアログのファイル名入力欄が見つかりません。");
            }
            var okButton = GetFileDialogOkButton(fileDialog);
            if (okButton == null)
            {
                return (false, @"ダイアログの保存ボタンが見つかりません。");
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
                return (false, @"ダイアログの保存ボタンをクリックできませんでした。");
            }

            // トップレベルウィンドウがファイルダイアログ以外になるまで待つ
            try
            {
                var done =
                    WaitUntil(
                        () =>
                        {
                            var topWin = WindowControl.FromZTop(this.TargetApp);
                            return (topWin.GetWindowText() != SaveFileDialogTitle);
                        });
                if (!done)
                {
                    return (false, @"ダイアログの終了待機処理がタイムアウトしました。");
                }
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
                return (false, @"ダイアログの終了待機処理に失敗しました。");
            }

            return true;
        }

        /// <summary>
        /// 音声保存処理の完了を待機する。
        /// </summary>
        /// <param name="mainWindow">メインウィンドウ。</param>
        /// <param name="saveButtonAsync">
        /// 音声保存ボタンクリック処理に用いた非同期オブジェクト。
        /// </param>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        private Result<bool> SaveFileImpl_WaitSaving(
            WindowControl mainWindow,
            Async saveButtonAsync)
        {
            Debug.Assert(mainWindow != null);
            Debug.Assert(saveButtonAsync != null);

            try
            {
                // 音声保存進捗ウィンドウが表示される場合があるので待つ
                var progressWin = mainWindow.WaitForNextModal(saveButtonAsync);
                if (progressWin != null)
                {
                    // 音声保存進捗ウィンドウか？
                    var title = progressWin.GetWindowText();
                    if (title != SaveProgressWindowTitle)
                    {
                        return (
                            false,
                            title + @"ダイアログが表示されたため処理を中止しました。");
                    }

                    // 終了待ち
                    progressWin.WaitForDestroy(saveButtonAsync);

                    // エラーダイアログが表示される場合があるので待つ
                    var errorDialog = mainWindow.WaitForNextModal(saveButtonAsync);
                    if (errorDialog != null)
                    {
                        title = errorDialog.GetWindowText();
                        return (
                            false,
                            title + @"ダイアログが表示されたため処理を中止しました。");
                    }
                }

                // 音声保存ボタン処理完了待ち
                if (!WaitUntil(() => saveButtonAsync.IsCompleted))
                {
                    return (false, @"音声保存ボタン処理がタイムアウトしました。");
                }
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
                return (false, @"音声保存処理の完了待機に失敗しました。");
            }

            return true;
        }

        /// <summary>
        /// 音声ファイルが保存されていることを確認する。
        /// </summary>
        /// <param name="filePath">音声ファイルパス。</param>
        /// <returns>確認できたならば true 。そうでなければ false 。</returns>
        private Result<bool> SaveFileImpl_CheckFileSaved(string filePath)
        {
            try
            {
                // 音声ファイル存在確認
                if (!File.Exists(filePath))
                {
                    return (false, @"ファイル保存を確認できませんでした。");
                }

                // テキストファイルが保存される場合があるので少し待つ
                // 保存されなくとも失敗にはしない
                var textFilePath = Path.ChangeExtension(filePath, @".txt");
                WaitUntil(() => File.Exists(textFilePath), 250);
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
                return (false, @"ファイル保存の確認処理に失敗しました。");
            }

            return true;
        }

        #endregion
    }
}
