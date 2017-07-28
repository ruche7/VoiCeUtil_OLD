using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Codeer.Friendly;
using Codeer.Friendly.Dynamic;
using Codeer.Friendly.Windows.Grasp;
using Ong.Friendly.FormsStandardControls;
using RucheHome.Diagnostics;
using RucheHome.Automation.Talkers.Friendly;

namespace RucheHome.Automation.Talkers.AITalkEx
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
    public class Talker : FormsProcessTalkerBase<ParameterId>
    {
        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="product">製品種別。</param>
        public Talker(Product product)
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
        /// 文章入力欄下にあるボタンの種別列挙。
        /// </summary>
        private enum MainButton
        {
            /// <summary>
            /// 再生/一時停止/再開
            /// </summary>
            Play,

            /// <summary>
            /// 停止
            /// </summary>
            Stop,

            /// <summary>
            /// 音声保存
            /// </summary>
            Save,

            /// <summary>
            /// 再生時間
            /// </summary>
            Time,
        }

        /// <summary>
        /// 文章入力欄下にあるボタンのテキスト値ディクショナリ。
        /// </summary>
        private static readonly Dictionary<MainButton, string> MainButtonTexts =
            new Dictionary<MainButton, string>
            {
                { MainButton.Play, @" 再生" },
                { MainButton.Stop, @" 停止" },
                { MainButton.Save, @" 音声保存" },
                { MainButton.Time, @" 再生時間" },
            };

        /// <summary>
        /// 文章入力欄下にあるボタン群の親コントロールを取得する。
        /// </summary>
        /// <param name="mainWindow">メインウィンドウ。</param>
        /// <returns>コントロール。取得できなかった場合は null 。</returns>
        private static AppVar GetMainButtonsParent(AppVar mainWindow) =>
            GetControlFromControlsTree(mainWindow, 0, 1, 0, 0, 0, 0, 1);

        /// <summary>
        /// 文章入力欄下にあるボタンコントロールを取得する。
        /// </summary>
        /// <param name="parent">ボタン群の親コントロール。</param>
        /// <param name="button">ボタン種別。</param>
        /// <returns>コントロール。取得できなかった場合は null 。</returns>
        /// <remarks>
        /// 前後に別ボタンが追加されることがあるため、テキスト値で目的のボタンを探す。
        /// </remarks>
        private static FormsButton GetMainButton(AppVar parent, MainButton button) =>
            MainButtonTexts.TryGetValue(button, out var text) ?
                FindChildControlByText<FormsButton>(parent, text) : null;

        /// <summary>
        /// 文章入力欄コントロール群を列挙する。
        /// </summary>
        /// <param name="mainWindow">メインウィンドウ。</param>
        /// <returns>コントロール群。取得できなかった場合は空の列挙。</returns>
        /// <remarks>
        /// 複数の文章入力欄が存在する状態になることがあるため、すべて列挙する。
        /// </remarks>
        private static IEnumerable<AppVar> EnumerateMainRichTextBoxes(AppVar mainWindow)
        {
            var parent = GetControlFromControlsTree(mainWindow, 0, 1, 0, 0, 0, 0, 0);
            if (parent != null)
            {
                // すべての子を調べる
                foreach (var c in parent.Dynamic().Controls)
                {
                    // ウィンドウクラス名に "RichEdit" が含まれるか否かで判断
                    if (new WindowControl(c).WindowClassName.Contains(@"RichEdit"))
                    {
                        yield return c;
                    }
                }
            }
        }

        /// <summary>
        /// ウィンドウ下部のタブコントロールを取得する。
        /// </summary>
        /// <param name="mainWindow">メインウィンドウ。</param>
        /// <returns>コントロール。取得できなかった場合は null 。</returns>
        private static FormsTabControl GetTabControl(AppVar mainWindow) =>
            GetControlFromControlsTree<FormsTabControl>(mainWindow, 0, 1, 0, 1, 0);

        /// <summary>
        /// パラメータを保持するタブページの情報配列。
        /// </summary>
        private static readonly
        (int index, string name, Func<ParameterId, bool> idSelector)[]
        ParameterTabPageInfos =
            {
                (2, @"音声効果", id => id.IsEffect()),
                (3, @"ポーズ", id => id.IsPause()),
            };

        /// <summary>
        /// パラメータIDごとの、タブページを基準とするテキストボックスの
        /// Controls プロパティツリーインデックス配列ディクショナリ。
        /// </summary>
        private static readonly Dictionary<ParameterId, int[]> ParameterControlsTreeIndices =
            new Dictionary<ParameterId, int[]>
            {
                // 音声効果
                { ParameterId.Volume, new[] { 0, 7 } },
                { ParameterId.Speed, new[] { 0, 6 } },
                { ParameterId.Tone, new[] { 0, 5 } },
                { ParameterId.Intonation, new[] { 0, 4 } },

                // ポーズ
                { ParameterId.PauseShort, new[] { 0, 13, 1 } },
                { ParameterId.PauseLong, new[] { 0, 11, 1 } },
                { ParameterId.PauseSentence, new[] { 0, 15, 1 } },
                { ParameterId.PauseBegin, new[] { 0, 9, 1 } },
                { ParameterId.PauseEnd, new[] { 0, 8, 1 } },
            };
        /// <summary>
        /// ウィンドウ下部のタブコントロールから、
        /// パラメータを保持するテキストボックスのディクショナリを取得する。
        /// </summary>
        /// <param name="targetParameterIds">
        /// 取得対象パラメータID列挙。 null ならばすべて対象。
        /// </param>
        /// <param name="mainWindow">
        /// メインウィンドウ。 null ならばメソッド内で取得される。
        /// </param>
        /// <returns>
        /// テキストボックスのディクショナリ。取得できなかった場合は null 。
        /// </returns>
        private Result<Dictionary<ParameterId, FormsTextBox>> GetTabControlParameterTextBoxes(
            IEnumerable<ParameterId> targetParameterIds = null,
            AppVar mainWindow = null)
        {
            // メインウィンドウを検索
            var mainWin = mainWindow ?? this.FindMainWindow();
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

            var dict = new Dictionary<ParameterId, FormsTextBox>();

            // 現在選択中のタブアイテムインデックスを保存
            int tabIndex = -1;
            try
            {
                tabIndex = tabControl.SelectedIndex;
                if (tabIndex < 0 || tabControl.TabCount < 4)
                {
                    return (null, @"本体のタブページが見つかりません");
                }
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
                return (null, @"本体のタブページから情報を取得できませんでした。");
            }

            try
            {
                var allIds = targetParameterIds ?? ParameterIdExtension.AllValues;

                foreach (var tab in ParameterTabPageInfos)
                {
                    // 対象パラメータID列挙取得
                    var ids = allIds.Where(tab.idSelector);
                    if (!ids.Any())
                    {
                        continue;
                    }

                    // タブページ取得
                    var page = FindTabPage(tabControl.AppVar, tab.name);
                    if (page == null)
                    {
                        // 一度も開いていない場合は取得できないので開いてみる
                        tabControl.EmulateTabSelect(tab.index);

                        page = FindTabPage(tabControl.AppVar, tab.name);
                        if (page == null)
                        {
                            return (null, @"本体のタブページが見つかりません。");
                        }
                    }

                    // 各テキストボックス取得
                    foreach (var id in ids)
                    {
                        var textBox =
                            GetControlFromControlsTree<FormsTextBox>(
                                page,
                                ParameterControlsTreeIndices[id]);
                        if (textBox == null)
                        {
                            return (
                                null,
                                $@"本体の{tab.name}タブの" +
                                $@"{id.GetInfo().DisplayName}入力欄が見つかりません。");
                        }

                        dict.Add(id, textBox);
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
                // 元のタブページを選択する
                // 失敗してもよい
                try
                {
                    if (tabIndex != tabControl.SelectedIndex)
                    {
                        tabControl.EmulateTabSelect(tabIndex);
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

        #region Friendly.ProcessTalkerBase<ParameterId> のオーバライド

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
            if (title.StartsWith(this.ProcessProduct))
            {
                return WindowTitleKind.Main;
            }
            if (title == SaveFileDialogTitle || title == SaveProgressWindowTitle)
            {
                return WindowTitleKind.FileSaving;
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
            var saveButton =
                GetMainButton(GetMainButtonsParent(mainWindow.AppVar), MainButton.Save);
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

        #region ProcessTalkerBase<ParameterId> のオーバライド

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

            try
            {
                // 文章入力欄を列挙
                var textBoxes = EnumerateMainRichTextBoxes(mainWin);
                if (textBoxes?.Any() != true)
                {
                    return (null, @"本体の文章入力欄が見つかりません。");
                }

                // 先頭の文章入力欄からテキストを得る
                return (string)textBoxes.First().Dynamic().Text;
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
        /// <see cref="ProcessTalkerBase{TParameterId}"/> 実装から
        /// <see cref="ProcessTalkerBase{TParameterId}.TextLengthLimit"/>
        /// を超える文字数の値や null が渡されることはない。
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

            // 500文字あたり1ミリ秒をタイムアウト値に追加
            var timeout = StandardTimeoutMilliseconds + (text.Length / 500);

            try
            {
                // 文章入力欄を列挙
                var textBoxes = EnumerateMainRichTextBoxes(mainWin);
                if (textBoxes?.Any() != true)
                {
                    return (false, @"本体の文章入力欄が見つかりません。");
                }

                // すべての文章入力欄にテキストを設定
                foreach (var tb in textBoxes)
                {
                    var textBox = new FormsRichTextBox(tb);
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
            // パラメータテキストボックス群を取得
            var (textBoxes, failMessage) = this.GetTabControlParameterTextBoxes();
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

            // パラメータテキストボックス群を取得
            var (textBoxes, failMessage) =
                this.GetTabControlParameterTextBoxes(
                    parameters.Select(kv => kv.Key),
                    mainWin);
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
                    textBox.EmulateChangeText(text);
                    dict.Add(id, true);
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
            // ウィンドウにフォーカスを移す
            // 失敗してもよい
            try
            {
                mainWin.Dynamic().Focus();
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

            // カーソルを先頭へ持っていく
            // 失敗しても先へ進む
            try
            {
                foreach (var textBox in EnumerateMainRichTextBoxes(mainWin))
                {
                    textBox.Dynamic().Select(0, 0);
                }
            }
            catch (Exception ex)
            {
                ThreadDebug.WriteException(ex);
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
                var modalWin = new WindowControl(mainWin).WaitForNextModal(async);
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
                stop.EmulateClick();
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
            AppVar mainWindow,
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
                fileDialog = new WindowControl(mainWindow).WaitForNextModal(saveButtonAsync);
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

            // ファイルパス設定＆決定ボタンクリック処理
            var r = OperateFileDialog(fileDialog, filePath);
            if (!r.Value)
            {
                return r;
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
            AppVar mainWindow,
            Async saveButtonAsync)
        {
            Debug.Assert(mainWindow != null);
            Debug.Assert(saveButtonAsync != null);

            try
            {
                var mainWin = new WindowControl(mainWindow);

                // 音声保存進捗ウィンドウが表示される場合があるので待つ
                var progressWin = mainWin.WaitForNextModal(saveButtonAsync);
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
                    var errorDialog = mainWin.WaitForNextModal(saveButtonAsync);
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
