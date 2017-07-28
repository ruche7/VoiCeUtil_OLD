using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Media;
using Codeer.Friendly;
using Codeer.Friendly.Dynamic;
using Codeer.Friendly.Windows.Grasp;
using RM.Friendly.WPFStandardControls;
using RucheHome.Diagnostics;
using RucheHome.Automation.Talkers.CeVIO.Internal.Controls;
using RucheHome.Automation.Talkers.Friendly;

namespace RucheHome.Automation.Talkers.CeVIO
{
    /// <summary>
    /// CeVIO Creative Studio S プロセスを操作する <see cref="IProcessTalker"/> 実装クラス。
    /// </summary>
    public class Talker : WpfProcessTalkerBase<ParameterId>, ICreativeStudioOperationSetting
    {
        /// <summary>
        /// コンストラクタ。
        /// </summary>
        public Talker()
            :
            base(
                ClrVersion.V4,
                @"CeVIO Creative Studio",
                @"CeVIO Creative Studio",
                @"CeVIO Creative Studio S",
                false,
                true)
        {
            this.Root =
                new Root(
                    this.GetMainWindow,
                    () => this.TargetVisualTreeHelper,
                    () => this.CanChangeTrack);
        }

        /// <summary>
        /// ビジー表示中であるか否かを取得する。
        /// </summary>
        /// <param name="mainWindow">メインウィンドウ。</param>
        /// <returns>ビジー表示中ならば true 。そうでなければ false 。</returns>
        private static bool IsBusyIndicatorVisible(AppVar mainWindow)
        {
            if (mainWindow != null)
            {
                try
                {
                    // Xceed.Wpf.Toolkit.BusyIndicator.IsBusy により調べる
                    return (bool)mainWindow.Dynamic().Content.Children[1].IsBusy;
                }
                catch (Exception ex)
                {
                    ThreadDebug.WriteException(ex);
                }
            }

            return false;
        }

        /// <summary>
        /// パラメータIDに対応するパラメータ値名称を作成する。
        /// </summary>
        /// <param name="id">パラメータID。</param>
        /// <returns>パラメータ値名称。</returns>
        private static string MakeParameterValueName(ParameterId id)
        {
            ArgumentValidation.IsEnumDefined(id, nameof(id));

            return $@"{id.GetInfo().DisplayName}の{(id.IsEmotion() ? @"感情" : @"")}値";
        }

        /// <summary>
        /// 操作対象アプリの VisualTreeHelper 型オブジェクトを取得または設定する。
        /// </summary>
        /// <remarks>
        /// <see cref="Friendly.ProcessTalkerBase{TParameterId}.TargetApp"/>
        /// プロパティの変更時に更新される。
        /// </remarks>
        private dynamic TargetVisualTreeHelper { get; set; } = null;

        /// <summary>
        /// ルートコントロール取得用オブジェクトを取得する。
        /// </summary>
        private Root Root { get; }

        /// <summary>
        /// ウィンドウ上部のトラックセレクタ取得用オブジェクトを取得する。
        /// </summary>
        private TrackSelector TrackSelector => this.Root.TrackSelector;

        /// <summary>
        /// ウィンドウ下部のコントロールパネル取得用オブジェクトを取得する。
        /// </summary>
        private ControlPanel ControlPanel => this.Root.ControlPanel;

        /// <summary>
        /// トーク用コントロールパネル左側のセリフデータグリッド取得用オブジェクトを取得する。
        /// </summary>
        private SpeechDataGrid SpeechDataGrid => this.ControlPanel.SpeechDataGrid;

        /// <summary>
        /// トーク用コントロールパネル右側の操作パネル取得用オブジェクトを取得する。
        /// </summary>
        private OperationPanel OperationPanel => this.ControlPanel.OperationPanel;

        /// <summary>
        /// 試聴/停止トグルボタン取得用オブジェクトを取得する。
        /// </summary>
        private PlayStopToggle PlayStopToggle => this.OperationPanel.PlayStopToggle;

        /// <summary>
        /// パラメータスライダー群取得用オブジェクトを取得する。
        /// </summary>
        private ParameterSliders ParameterSliders => this.OperationPanel.ParameterSliders;

        /// <summary>
        /// キャストやセリフの入力のために適切なセリフデータグリッド行を選択する。
        /// </summary>
        /// <param name="speechDataGrid">セリフデータグリッド。</param>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        private Result<bool> SelectSpeechDataGridRowForInput(WPFDataGrid speechDataGrid)
        {
            if (speechDataGrid == null)
            {
                return (false, @"本体のセリフ一覧表が見つかりません。");
            }

            switch (this.CastSpeechInputRow)
            {
            case CastSpeechInputRow.Blank:
                try
                {
                    var selectedIndex = speechDataGrid.CurrentItemIndex;

                    // 現在行のセリフセルが空なら現状のままでよい
                    var cell = speechDataGrid.GetCell(selectedIndex, 1);
                    if (string.IsNullOrWhiteSpace((string)cell.Dynamic().Content.Text))
                    {
                        return true;
                    }

                    // セリフセルが空の行を探す
                    var count = (int)speechDataGrid.Dynamic().Items.Count;
                    for (int ri = selectedIndex + 1; ri < count; ++ri)
                    {
                        cell = speechDataGrid.GetCell(ri, 1);
                        if (string.IsNullOrWhiteSpace((string)cell.Dynamic().Content.Text))
                        {
                            // 選択
                            speechDataGrid.Dynamic().SelectedIndex = ri;
                            return true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    ThreadTrace.WriteException(ex);
                    return (false, @"本体のセリフ一覧表を操作できませんでした。");
                }
                return (false, @"本体のセリフ一覧表にセリフ挿入可能な行がありません。");

            case CastSpeechInputRow.Current:
                // 現状のまま
                return true;
            }

            return (false, @"セリフ挿入先設定が不正な状態です。");
        }

        /// <summary>
        /// セリフグリッド挿入用のセリフ文字列を作成する。
        /// </summary>
        /// <param name="src">作成元となる文字列。</param>
        /// <returns>セリフ文字列。引数値が null ならば null 。</returns>
        private string MakeSpeechText(string src)
        {
            // 改行で区切るなら半角スペース、そうでなければ空文字列に置換する
            var lineBreak = this.IsTextSeparatedByLineBreaks ? @" " : @"";

            // タブ文字等は本体側で半角スペースに置換してくれるのでそのままでよい
            return
                src?
                    .Replace("\r\n", lineBreak)
                    .Replace("\r", lineBreak)
                    .Replace("\n", lineBreak);
        }

        /// <summary>
        /// スプラッシュスクリーンのウィンドウタイトル。
        /// </summary>
        private const string SplashScreenWindowTitle =
            @"アプリケーションを開く間、お待ちください";

        /// <summary>
        /// 音声ファイル保存ダイアログのウィンドウタイトル。
        /// </summary>
        private const string SaveFileDialogTitle = @"保存";

        /// <summary>
        /// 音声ファイル保存上書き確認ダイアログのウィンドウタイトル。
        /// </summary>
        private const string SaveOverwriteDialogTitle = @"名前を付けて保存の確認";

        /// <summary>
        /// 音声ファイル連続保存ダイアログのウィンドウタイトル。
        /// </summary>
        private const string SaveSomeFilesDialogTitle = @"セリフの連続WAV書き出し";

        /// <summary>
        /// 音声ファイルミックスダウン保存ダイアログのウィンドウタイトル。
        /// </summary>
        private const string SaveMixDownDialogTitle = @"ミックスダウンの書き出し";

        /// <summary>
        /// 音声完了ダイアログのウィンドウタイトル。
        /// </summary>
        private const string SaveCompleteDialogTitle = @"CeVIO Creative Studio S";

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

            if (title.Length == 0 || title == SplashScreenWindowTitle)
            {
                return WindowTitleKind.StartupOrCleanup;
            }
            if (title.EndsWith(@" - " + this.TalkerName))
            {
                return WindowTitleKind.Main;
            }
            if (
                title == SaveFileDialogTitle ||
                title == SaveOverwriteDialogTitle ||
                title == SaveSomeFilesDialogTitle ||
                title == SaveMixDownDialogTitle)
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
            // ビジー表示中なら音声保存中と判断する
            if (IsBusyIndicatorVisible(mainWindow.AppVar))
            {
                return TalkerState.FileSaving;
            }

            var (root, _) = this.Root.Get(mainWindow.AppVar);
            if (root != null)
            {
                var (panel, _) = this.ControlPanel.GetAny(out var kind, root);
                if (panel != null)
                {
                    // コントロールパネルがトーク用以外ならアイドル状態扱い
                    if (kind != ControlPanelKind.Talk)
                    {
                        return TalkerState.Idle;
                    }

                    (panel, _) = this.OperationPanel.Get(panel);
                    if (panel != null)
                    {
                        var (button, _) = this.PlayStopToggle.Get(panel);
                        if (button != null)
                        {
                            // 試聴/停止トグルボタンがONなら読み上げ中、OFFならアイドル状態
                            return
                                (button.IsChecked == true) ?
                                    TalkerState.Speaking : TalkerState.Idle;
                        }
                    }
                }
            }

            // いずれかのコントロールが見つからないとここに来る
            // ウィンドウ構築途中or破棄途中であると判断する
            // 即ち起動中or終了中
            return
                (this.State == TalkerState.None || this.State == TalkerState.Startup) ?
                    TalkerState.Startup : TalkerState.Cleanup;
        }

        /// <summary>
        /// <see cref="Friendly.ProcessTalkerBase{TParameterId}.TargetApp"/>
        /// プロパティ値の変更時に呼び出される。
        /// </summary>
        protected override void OnTargetAppChanged()
        {
            this.TargetVisualTreeHelper = this.TargetApp?.Type(typeof(VisualTreeHelper));
        }

        #endregion

        #region ProcessTalkerBase<ParameterId> のオーバライド

        /// <summary>
        /// 文章の最大許容文字数を取得する。
        /// </summary>
        public override int TextLengthLimit { get; } = 200;

        /// <summary>
        /// 有効キャラクターの一覧を取得する。
        /// </summary>
        /// <returns>有効キャラクター配列。取得できなかった場合は null 。</returns>
        protected override Result<ReadOnlyCollection<string>> GetAvailableCharactersImpl()
        {
            // セリフデータグリッドを取得
            var (grid, failMessage) = this.SpeechDataGrid.Get();
            if (grid == null)
            {
                return (null, failMessage);
            }

            var casts = new List<string>();

            dynamic combo = null;
            int selectedIndex = -1;

            try
            {
                // 選択行のキャストセル取得
                var cell = grid.GetCell(grid.CurrentItemIndex, 0);

                // コンボボックス取得
                combo = cell.Dynamic().Content;

                // combo の Items[N].Name でキャスト名を取得できるが、
                // CeVIO定義の型を参照することになるのでやめておく。

                // 選択中インデックスを保存
                selectedIndex = (int)combo.SelectedIndex;

                // 各キャストを選択して ComboBox.Text からキャスト名を取得
                var count = (int)combo.Items.Count;
                for (int ii = 0; ii < count; ++ii)
                {
                    combo.SelectedIndex = ii;
                    casts.Add((string)combo.Text);
                }
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
                return (null, @"キャスト名一覧の取得に失敗しました。");
            }
            finally
            {
                // コンボボックスの選択キャストを元に戻す
                // 失敗してもよい
                if (selectedIndex >= 0)
                {
                    try
                    {
                        combo.SelectedIndex = selectedIndex;
                    }
                    catch (Exception ex)
                    {
                        ThreadDebug.WriteException(ex);
                    }
                }
            }

            return casts.AsReadOnly();
        }

        /// <summary>
        /// 現在選択されているキャラクターを取得する。
        /// </summary>
        /// <returns>キャラクター。取得できなかった場合は null 。</returns>
        protected override Result<string> GetCharacterImpl()
        {
            // セリフデータグリッドを取得
            var (grid, failMessage) = this.SpeechDataGrid.Get();
            if (grid == null)
            {
                return (null, failMessage);
            }

            try
            {
                // 選択行のキャストセル取得
                var cell = grid.GetCell(grid.CurrentItemIndex, 0);

                return (string)cell.Dynamic().Content.Text;
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
            }
            return (null, @"選択中キャスト名の取得に失敗しました。");
        }

        /// <summary>
        /// キャラクターを選択させる。
        /// </summary>
        /// <param name="character">
        /// キャラクター。
        /// <see cref="ProcessTalkerBase{TParameterId}"/> 実装から null が渡されることはない。
        /// 有効でないキャラクターは渡されうる。
        /// </param>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        protected override Result<bool> SetCharacterImpl(string character)
        {
            // セリフデータグリッドを取得
            var (grid, failMessage) = this.SpeechDataGrid.Get();
            if (grid == null)
            {
                return (false, failMessage);
            }

            dynamic combo = null;
            int selectedIndex = -1;

            try
            {
                // 選択行のキャストセル取得
                var cell = grid.GetCell(grid.CurrentItemIndex, 0);

                // 既に対象キャスト選択中なら何もせず終える
                // CastSpeechInputRow 設定が Blank の場合は SetTextImpl で帳尻合わせされる
                if ((string)cell.Dynamic().Content.Text == character)
                {
                    return true;
                }

                // 適切な行を選択
                var v = this.SelectSpeechDataGridRowForInput(grid);
                if (!v.Value)
                {
                    return (false, v.Message);
                }

                // 改めて選択行のキャストセル取得
                cell = grid.GetCell(grid.CurrentItemIndex, 0);

                // コンボボックス取得
                combo = cell.Dynamic().Content;

                // 既に対象キャスト選択中なら完了
                if ((string)combo.Text == character)
                {
                    return true;
                }

                // combo の Items[N].Name でキャスト名を取得できるが、
                // CeVIO定義の型を参照することになるのでやめておく。

                // 選択中インデックスを保存
                selectedIndex = (int)combo.SelectedIndex;

                // 各キャストを選択して ComboBox.Text からキャスト名を取得
                var count = (int)combo.Items.Count;
                for (int ii = 0; ii < count; ++ii)
                {
                    combo.SelectedIndex = ii;
                    if ((string)combo.Text == character)
                    {
                        // 見つかったので完了
                        selectedIndex = -1; // finally での戻し処理を行わせない
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
                return (false, @"キャストの選択に失敗しました。");
            }
            finally
            {
                // コンボボックスの選択キャストを元に戻す
                // 失敗してもよい
                if (selectedIndex >= 0)
                {
                    try
                    {
                        combo.SelectedIndex = selectedIndex;
                    }
                    catch (Exception ex)
                    {
                        ThreadDebug.WriteException(ex);
                    }
                }
            }

            return (false, $@"本体に {character} が登録されていません。");
        }

        /// <summary>
        /// 現在設定されている文章を取得する。
        /// </summary>
        /// <returns>文章。取得できなかった場合は null 。</returns>
        protected override Result<string> GetTextImpl()
        {
            // セリフデータグリッドを取得
            var (grid, failMessage) = this.SpeechDataGrid.Get();
            if (grid == null)
            {
                return (null, failMessage);
            }

            try
            {
                // 選択行のセリフセル取得
                var cell = grid.GetCell(grid.CurrentItemIndex, 1);

                return (string)cell.Dynamic().Content.Text;
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
            }
            return (null, @"選択中セリフの取得に失敗しました。");
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
            // セリフ文字列化
            var speechText = this.MakeSpeechText(text);

            // メインウィンドウを取得
            var mainWin = this.GetMainWindow();
            if (mainWin == null)
            {
                return (false, @"本体のウィンドウが見つかりません。");
            }

            // セリフデータグリッドを取得
            var (grid, failMessage) = this.SpeechDataGrid.Get();
            if (grid == null)
            {
                return (false, failMessage);
            }

            // 選択中キャストインデックスを取得
            int castIndex = -1;
            try
            {
                var cell = grid.GetCell(grid.CurrentItemIndex, 0);
                castIndex = (int)cell.Dynamic().Content.SelectedIndex;
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
                return (false, @"選択中キャストの取得に失敗しました。");
            }

            // 適切な行を選択
            var v = this.SelectSpeechDataGridRowForInput(grid);
            if (!v.Value)
            {
                return (false, v.Message);
            }

            try
            {
                var row = grid.CurrentItemIndex;

                // 選択行のキャストインデックス設定
                if (castIndex >= 0)
                {
                    grid.EmulateChangeCellComboSelect(row, 0, castIndex);
                }

                // 選択行のセリフセルに文字列設定
                var async = new Async();
                grid.EmulateChangeCellText(row, 1, speechText, async);

                // ダイアログが出る可能性があるためそれを待つ
                // ダイアログが出ずに完了した場合は成功
                var modalWin = new WindowControl(mainWin).WaitForNextModal(async);
                if (modalWin != null)
                {
                    return (false, @"本体側でダイアログが表示されました。");
                }
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
                return (false, @"セリフの設定に失敗しました。");
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
            // パラメータスライダー群を取得
            var (sliders, failMessage) = this.ParameterSliders.Get();
            if (sliders == null)
            {
                return (null, failMessage);
            }

            var dict = new Dictionary<ParameterId, decimal>();

            foreach (var idSlider in sliders)
            {
                var id = idSlider.Key;
                try
                {
                    dict.Add(id, (decimal)idSlider.Value.Value);
                }
                catch (Exception ex)
                {
                    ThreadTrace.WriteException(ex);
                    return (null, MakeParameterValueName(id) + @"を取得できませんでした。");
                }
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
            // パラメータスライダー群を取得
            var (sliders, failMessage) =
                this.ParameterSliders.Get(parameters.Select(kv => kv.Key));
            if (sliders == null)
            {
                return (null, failMessage);
            }

            var dict = new Dictionary<ParameterId, Result<bool>>();

            foreach (var idSlider in sliders)
            {
                var id = idSlider.Key;
                var value = parameters.First(kv => kv.Key == id).Value;
                var info = id.GetInfo();
                var format = @"F" + info.Digits;

                // 範囲チェック
                if (value < info.MinValue)
                {
                    dict.Add(
                        id,
                        (
                            false,
                            MakeParameterValueName(id) + @"に " +
                            info.MinValue.ToString(format) + @" より小さい値 " +
                            value.ToString(format) + @" は設定できません。"
                        ));
                    continue;
                }
                if (value > info.MaxValue)
                {
                    dict.Add(
                        id,
                        (
                            false,
                            MakeParameterValueName(id) + @"に " +
                            info.MaxValue.ToString(format) + @" より大きい値 " +
                            value.ToString(format) + @" は設定できません。"
                        ));
                    continue;
                }

                try
                {
                    idSlider.Value.EmulateChangeValue((double)value);
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
                    continue;
                }

                dict.Add(id, true);
            }

            return dict;
        }

        /// <summary>
        /// 現在の文章の読み上げを開始させる。
        /// </summary>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        protected override Result<bool> SpeakImpl()
        {
            // 空白文チェック
            var (text, failText) = this.GetTextImpl();
            if (string.IsNullOrWhiteSpace(text))
            {
                return (
                    false,
                    (text == null) ? failText : @"空白文を試聴することはできません。");
            }

            // メインウィンドウを取得
            var mainWin = this.GetMainWindow();
            if (mainWin == null)
            {
                return (false, @"本体のウィンドウが見つかりません。");
            }

            // 試聴/停止トグルボタンを取得
            var (button, failButton) = this.PlayStopToggle.Get();
            if (button == null)
            {
                return (false, failButton);
            }

            try
            {
                if (!button.IsEnabled)
                {
                    return (false, @"本体の試聴/停止ボタンがクリックできない状態です。");
                }

                // WPFToggleButton.EmulateCheck だと動作はするが再生されない。
                // WPFButtonBase.EmulateClick を使う。

                // 試聴/停止トグルボタンをクリック
                // 試聴中ならば2回クリック
                var buttonBase = new WPFButtonBase(button.AppVar);
                var async = new Async();
                if (button.IsChecked == true)
                {
                    buttonBase.EmulateClick();
                }
                buttonBase.EmulateClick(async);

                // ダイアログが出る可能性があるためそれを待つ
                // ダイアログが出ずに完了した場合は成功
                var modalWin = new WindowControl(mainWin).WaitForNextModal(async);
                if (modalWin != null)
                {
                    return (false, @"本体側でダイアログが表示されました。");
                }
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
                return (false, @"本体の試聴/停止ボタンをクリックできませんでした。");
            }

            return true;
        }

        /// <summary>
        /// 読み上げを停止させる。
        /// </summary>
        /// <returns>成功したか既に停止中ならば true 。そうでなければ false 。</returns>
        protected override Result<bool> StopImpl()
        {
            // 試聴/停止トグルボタンを取得
            var (button, failButton) = this.PlayStopToggle.Get();
            if (button == null)
            {
                return (false, failButton);
            }

            try
            {
                if (!(bool)button.Dynamic().IsEnabled)
                {
                    return (false, @"本体の試聴/停止ボタンがクリックできない状態です。");
                }
                if (button.IsChecked == false)
                {
                    // 既に停止しているので何もしない
                    return (true, @"停止済みです。");
                }

                // WPFToggleButton.EmulateCheck だと動作はするが停止されない。
                // WPFButtonBase.EmulateClick を使う。

                // 試聴/停止トグルボタンをクリック
                var buttonBase = new WPFButtonBase(button.AppVar);
                buttonBase.EmulateClick();
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
                return (false, @"本体の試聴/停止ボタンをクリックできませんでした。");
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

            // メインウィンドウを取得
            var mainWin = this.GetMainWindow();
            if (mainWin == null)
            {
                return (null, @"本体のウィンドウが見つかりません。");
            }

            // セリフデータグリッドを取得して単一行を選択
            var (grid, gridMessage) = this.SaveFileImpl_SelectSingleDataGridRow();
            if (grid == null)
            {
                return (null, gridMessage);
            }

            var saveMenuAsync = new Async();

            // WAV書き出しメニューをクリックして音声ファイル保存ダイアログを取得
            var (fileDialog, fileDialogMessage) =
                this.SaveFileImpl_ClickSaveMenu(mainWin, grid, saveMenuAsync);
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
            result = this.SaveFileImpl_WaitSaving(mainWin, saveMenuAsync);
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
        /// セリフデータグリッドを取得し、単一行を選択する。
        /// </summary>
        /// <returns>セリフデータグリッド。失敗したならば null 。</returns>
        /// <remarks>
        /// セリフデータグリッド自体は単一行選択設定なのだが、
        /// CeVIO独自のコントロール拡張により追加の行選択が行われている場合がある。
        /// そのまま音声保存しようとすると連続書き出しになってしまうため、
        /// 一旦別の行を選択することで追加の行選択状態を解除する。
        /// </remarks>
        private Result<WPFDataGrid> SaveFileImpl_SelectSingleDataGridRow()
        {
            // セリフデータグリッドを取得
            var (grid, failMessage) = this.SpeechDataGrid.Get();
            if (grid == null)
            {
                return (null, failMessage);
            }

            int selectedIndex = -1;
            try
            {
                selectedIndex = grid.CurrentItemIndex;

                // 一旦別の行を選択して元の行に戻す
                var g = grid.Dynamic();
                g.SelectedIndex = (selectedIndex > 0) ? (selectedIndex - 1) : 1;
                g.SelectedIndex = selectedIndex;

                selectedIndex = -1;
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
                return (null, @"本体のセリフ一覧表を操作できませんでした。");
            }
            finally
            {
                // 元の行に戻す
                // 失敗してもよい
                if (selectedIndex >= 0)
                {
                    try
                    {
                        grid.Dynamic().SelectedIndex = selectedIndex;
                    }
                    catch (Exception ex)
                    {
                        ThreadDebug.WriteException(ex);
                    }
                }
            }

            return grid;
        }

        /// <summary>
        /// WAV書き出しメニューをクリックし、音声ファイル保存ダイアログを取得する。
        /// </summary>
        /// <param name="mainWindow">メインウィンドウ。</param>
        /// <param name="speechDataGrid">セリフデータグリッド。</param>
        /// <param name="saveMenuAsync">
        /// WAV書き出しメニュークリック処理用の非同期オブジェクト。
        /// </param>
        /// <returns>音声ファイル保存ダイアログ。表示されなかった場合は null 。</returns>
        private Result<WindowControl> SaveFileImpl_ClickSaveMenu(
            AppVar mainWindow,
            WPFDataGrid speechDataGrid,
            Async saveMenuAsync)
        {
            Debug.Assert(mainWindow != null);
            Debug.Assert(speechDataGrid != null);
            Debug.Assert(saveMenuAsync != null);

            WindowControl fileDialog = null;

            try
            {
                // WAVE書き出しメニューアイテムを取得
                var menuItem =
                    new WPFMenuItem(speechDataGrid.Dynamic().ContextMenu.Items[12]);

                if (!menuItem.IsEnabled)
                {
                    return (null, @"本体のWAV書き出しメニューがクリックできない状態です。");
                }

                // WAVE書き出しメニュークリック
                menuItem.EmulateClick(saveMenuAsync);

                // ファイルダイアログを待つ
                fileDialog = new WindowControl(mainWindow).WaitForNextModal(saveMenuAsync);
                if (fileDialog == null)
                {
                    return (null, @"本体の音声保存ダイアログが見つかりません。");
                }

                // ダイアログタイトルを確認
                switch (fileDialog.GetWindowText())
                {
                case SaveFileDialogTitle:
                    // OK
                    break;

                case SaveSomeFilesDialogTitle:
                    // 連続書き出しダイアログになってしまっている
                    return (null, @"本体側で連続WAV書き出しダイアログが表示されました。");

                default:
                    return (null, @"本体側でダイアログが表示されました。");
                }
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
                return (null, @"本体のWAV書き出しメニューをクリックできませんでした。");
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
        /// <param name="saveMenuAsync">
        /// WAV書き出しメニュークリック処理に用いた非同期オブジェクト。
        /// </param>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        private Result<bool> SaveFileImpl_WaitSaving(AppVar mainWindow, Async saveMenuAsync)
        {
            Debug.Assert(mainWindow != null);
            Debug.Assert(saveMenuAsync != null);

            try
            {
                // WAV書き出しメニュークリック処理とビジー表示が両方完了するまで待機
                while (!saveMenuAsync.IsCompleted || IsBusyIndicatorVisible(mainWindow))
                {
                    Thread.Yield();
                }

                // 保存完了ダイアログを探す
                var completeDialog =
                    WaitUntil(
                        () =>
                        {
                            var win = WindowControl.FromZTop(this.TargetApp);
                            return
                                (win.GetWindowText() == SaveCompleteDialogTitle) ?
                                    win : null;
                        },
                        win => win != null);

                if (completeDialog == null)
                {
                    return (false, @"本体の保存完了ダイアログが見つかりません。");
                }

                // 保存完了ダイアログのOKボタンを押下する
                // 失敗してもよい
                try
                {
                    var vtree = this.TargetVisualTreeHelper;

                    var border = vtree.GetChild(completeDialog, 0);
                    var buttonParent =
                        border
                            .Child          // StackPanel
                            .Children[1]    // Border
                            .Child          // StackPanel
                            .Children[1];   // Control
                    var button = new WPFButtonBase(vtree.GetChild(buttonParent, 0));

                    button.EmulateClick();
                }
                catch (Exception ex)
                {
                    ThreadDebug.WriteException(ex);
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

        #region ICreativeStudioOperationSetting の実装

        /// <summary>
        /// トラックの選択変更を許容するか否かを取得または設定する。
        /// </summary>
        /// <remarks>
        /// <para>
        /// トーク用トラックが選択されていない状態で各種操作を行おうとした際、
        /// 自動的にトーク用トラックを選択してよいか否かの判断に利用される。
        /// </para>
        /// <para>
        /// このプロパティ値が false であれば自動選択は行われず、操作は失敗する。
        /// </para>
        /// </remarks>
        public bool CanChangeTrack
        {
            get => this.canChangeTrack;
            set => this.SetProperty(ref this.canChangeTrack, value);
        }
        private bool canChangeTrack = true;

        /// <summary>
        /// セリフグリッドに入力する際の入力対象行設定を取得または設定する。
        /// </summary>
        public CastSpeechInputRow CastSpeechInputRow
        {
            get => this.castSpeechInputRow;
            set =>
                this.SetProperty(
                    ref castSpeechInputRow,
                    Enum.IsDefined(value.GetType(), value) ?
                        value : CastSpeechInputRow.Blank);
        }
        private CastSpeechInputRow castSpeechInputRow = CastSpeechInputRow.Blank;

        /// <summary>
        /// 改行で文章を区切るか否かを取得または設定する。
        /// </summary>
        /// <remarks>
        /// <para>
        /// セリフグリッドは改行を受け付けないため、設定時に削除もしくは置換する必要がある。
        /// </para>
        /// <para>
        /// このプロパティ値が true ならば改行を半角スペースに置換する。
        /// そうでなければ改行を削除する。
        /// </para>
        /// </remarks>
        public bool IsTextSeparatedByLineBreaks
        {
            get => this.textSeparatedByLineBreaks;
            set => this.SetProperty(ref this.textSeparatedByLineBreaks, value);
        }
        private bool textSeparatedByLineBreaks = false;

        #endregion
    }
}
