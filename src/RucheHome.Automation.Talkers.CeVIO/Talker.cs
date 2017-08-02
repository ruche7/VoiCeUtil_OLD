using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Codeer.Friendly;
using Codeer.Friendly.Dynamic;
using Codeer.Friendly.Windows.Grasp;
using RucheHome.Automation.Friendly.Wpf;
using RucheHome.Automation.Talkers.CeVIO.Internal.Controls;
using RucheHome.Automation.Talkers.Friendly;
using RucheHome.Caches;
using RucheHome.Diagnostics;

namespace RucheHome.Automation.Talkers.CeVIO
{
    /// <summary>
    /// CeVIO Creative Studio S プロセスを操作する <see cref="IProcessTalker"/> 実装クラス。
    /// </summary>
    public class Talker : WpfTalkerBase<ParameterId>, ICreativeStudioOperation
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
                canSetBlankText: false,
                canSaveBlankText: false,
                hasCharacters: true)
        {
            this.Root =
                new Root(
                    this.GetMainWindow,
                    () => this.TargetAppVisualTree,
                    () => this.CanChangeTrack);
        }

        /// <summary>
        /// ビジー表示中であるか否かを取得する。
        /// </summary>
        /// <param name="mainWindow">メインウィンドウ。</param>
        /// <returns>ビジー表示中ならば true 。そうでなければ false 。</returns>
        private static bool IsBusyIndicatorVisible(dynamic mainWindow)
        {
            if (mainWindow != null)
            {
                try
                {
                    // Xceed.Wpf.Toolkit.BusyIndicator.IsBusy により調べる
                    return (bool)mainWindow.Content.Children[1].IsBusy;
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
        /// 自動試聴トグルボタン取得用オブジェクトを取得する。
        /// </summary>
        private AutoPlayToggle AutoPlayToggle => this.OperationPanel.AutoPlayToggle;

        /// <summary>
        /// パラメータスライダー群取得用オブジェクトを取得する。
        /// </summary>
        private ParameterSliders ParameterSliders => this.OperationPanel.ParameterSliders;

        /// <summary>
        /// キャスト名キャッシュを取得または設定する。
        /// </summary>
        /// <remarks>
        /// <see cref="OnTargetAppChanged"/> でクリアされる。
        /// </remarks>
        private ReadOnlyCollection<string> CastNamesCache { get; set; } = null;

        /// <summary>
        /// キャスト名キャッシュが有効ならそのまま取得する。
        /// そうでなければキャストコンボボックスからキャスト名キャッシュを作成する。
        /// </summary>
        /// <param name="castComboBox">キャストコンボボックス。</param>
        /// <returns>キャスト名キャッシュ。作成に失敗したならば null 。</returns>
        private Result<ReadOnlyCollection<string>> GetOrCreateCastNamesCache(
            dynamic castComboBox)
        {
            var cache = this.CastNamesCache;
            if (cache != null)
            {
                return cache;
            }

            var (ok, failMessage) = this.CreateCastNamesCache((DynamicAppVar)castComboBox);
            if (!ok)
            {
                return (null, failMessage);
            }

            return this.CastNamesCache;
        }

        /// <summary>
        /// キャストコンボボックスからキャスト名キャッシュを作成する。
        /// </summary>
        /// <param name="castComboBox">キャストコンボボックス。</param>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        private Result<bool> CreateCastNamesCache(dynamic castComboBox)
        {
            if (castComboBox == null)
            {
                return (false, @"本体のキャスト一覧が見つかりません。");
            }

            // castComboBox.Items[N].Name でキャスト名を取得できるが、
            // CeVIO定義の型を参照することになるのでやめておく。

            var casts = new List<string>();
            int selectedIndex = -1;

            try
            {
                // 選択中インデックスを保存
                selectedIndex = (int)castComboBox.SelectedIndex;

                // 各キャストを選択して ComboBox.Text からキャスト名を取得
                var count = (int)castComboBox.Items.Count;
                for (int ii = 0; ii < count; ++ii)
                {
                    castComboBox.SelectedIndex = ii;
                    casts.Add((string)castComboBox.Text);
                }
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
                return (false, @"キャスト一覧の取得に失敗しました。");
            }
            finally
            {
                // 選択中インデックスを元に戻す
                // 失敗してもよい
                if (selectedIndex >= 0)
                {
                    try
                    {
                        castComboBox.SelectedIndex = selectedIndex;
                    }
                    catch (Exception ex)
                    {
                        ThreadDebug.WriteException(ex);
                    }
                }
            }

            this.CastNamesCache = casts.AsReadOnly();
            return true;
        }

        /// <summary>
        /// キャストやセリフの入力のために適切なセリフデータグリッド行を選択する。
        /// </summary>
        /// <param name="speechDataGrid">セリフデータグリッド。</param>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        private Result<bool> SelectSpeechDataGridRowForInput(AppDataGrid speechDataGrid)
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
                    var selectedRow = speechDataGrid.SelectedRow;

                    // 現在行のセリフセルが空なら現状のままでよい
                    var cellText = speechDataGrid.GetCellText(selectedRow, 1);
                    if (string.IsNullOrWhiteSpace(cellText))
                    {
                        return true;
                    }

                    // セリフセルが空の行を探す
                    var rowCount = speechDataGrid.RowCount;
                    for (int ri = selectedRow + 1; ri < rowCount; ++ri)
                    {
                        cellText = speechDataGrid.GetCellText(ri, 1);
                        if (string.IsNullOrWhiteSpace(cellText))
                        {
                            // 選択
                            speechDataGrid.Focus();
                            speechDataGrid.SelectedRow = ri;
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

        #region WpfTalkerBase<ParameterId> のオーバライド

        /// <summary>
        /// <see cref="TalkerBase{TParameterId}.TargetApp"/>
        /// プロパティ値の変更時に呼び出される。
        /// </summary>
        protected override void OnTargetAppChanged()
        {
            // WpfTalkerBase の処理
            base.OnTargetAppChanged();

            // キャスト名キャッシュをクリア
            this.CastNamesCache = null;
        }

        #endregion

        #region Friendly.TalkerBase<ParameterId> のオーバライド

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
            var mainWin = mainWindow.Dynamic();

            // ビジー表示中なら音声保存中と判断する
            if (IsBusyIndicatorVisible(mainWin))
            {
                return TalkerState.FileSaving;
            }

            try
            {
                // ビジュアルツリー走査用オブジェクト取得or作成
                var vtree =
                    (mainWindow.App == this.TargetApp) ? this.TargetAppVisualTree : null;
                vtree = vtree ?? new AppVisualTree(mainWindow.App);

                (DynamicAppVar c, var rootMessage) =
                    this.Root.Get(out bool compacted, (DynamicAppVar)mainWin, vtree);
                if (c != null)
                {
                    (c, _) = this.ControlPanel.GetAny(out var kind, c, vtree);
                    if (c != null)
                    {
                        // コントロールパネルがトーク用以外ならアイドル状態扱い
                        if (kind != ControlPanelKind.Talk)
                        {
                            return TalkerState.Idle;
                        }

                        (c, _) = this.OperationPanel.Get(c);
                        if (c != null)
                        {
                            var (button, _) = this.PlayStopToggle.Get(c);
                            if (button != null)
                            {
                                // 試聴/停止トグルボタンがONなら読み上げ中
                                // OFFならアイドル状態
                                return
                                    ((bool?)button.IsChecked == true) ?
                                        TalkerState.Speaking : TalkerState.Idle;
                            }
                        }
                    }
                }
                else if (compacted)
                {
                    // コンパクト表示はブロッキング中扱い
                    return (TalkerState.Blocking, rootMessage);
                }
            }
            catch (Exception ex)
            {
                ThreadDebug.WriteException(ex);
            }

            // いずれかのコントロールが見つからないとここに来る
            // ウィンドウ構築途中or破棄途中であると判断する
            // 即ち起動中or終了中
            return
                (this.State == TalkerState.None || this.State == TalkerState.Startup) ?
                    TalkerState.Startup : TalkerState.Cleanup;
        }

        #endregion

        #region ProcessTalkerBase<ParameterId> のオーバライド

        /// <summary>
        /// 文章の最大許容文字数を取得する。
        /// </summary>
        public override int TextLengthLimit { get; } = TextFormatter.TextLengthLimit;

        /// <summary>
        /// 有効キャラクターの一覧を取得する。
        /// </summary>
        /// <returns>有効キャラクター配列。取得できなかった場合は null 。</returns>
        protected override Result<ReadOnlyCollection<string>> GetAvailableCharactersImpl()
        {
            // キャスト名キャッシュが有効ならそれを返す
            var cache = this.CastNamesCache;
            if (cache != null)
            {
                return cache;
            }

            // セリフデータグリッドを取得
            var (grid, gridMessage) = this.SpeechDataGrid.Get();
            if (grid == null)
            {
                return (null, gridMessage);
            }

            var casts = new List<string>();

            DynamicAppVar combo = null;
            try
            {
                // 選択行のキャストセルコンボボックス取得
                combo = grid.GetCellContent(grid.SelectedRow, 0);
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
                return (null, @"本体のキャスト一覧の取得に失敗しました。");
            }

            // キャスト名キャッシュ作成
            var (cacheOk, cacheMessage) = this.CreateCastNamesCache(combo);
            if (!cacheOk)
            {
                return (null, cacheMessage);
            }

            return this.CastNamesCache;
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
                // 選択行のキャストセルテキスト取得
                return grid.GetCellText(grid.SelectedRow, 0);
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
            var (grid, gridMessage) = this.SpeechDataGrid.Get();
            if (grid == null)
            {
                return (false, gridMessage);
            }

            try
            {
                // 選択行のキャストセルコンボボックス取得
                var combo = grid.GetCellContent(grid.SelectedRow, 0);

                // 既に対象キャスト選択中なら何もせず終える
                // CastSpeechInputRow 設定が Blank の場合は SetTextImpl で帳尻合わせされる
                if ((string)combo.Text == character)
                {
                    return true;
                }

                // キャスト名キャッシュを取得or作成
                var (cache, cacheMessage) =
                    this.GetOrCreateCastNamesCache((DynamicAppVar)combo);
                if (cache == null)
                {
                    return (false, cacheMessage);
                }

                // キャスト検索
                var castIndex = cache.IndexOf(character);
                if (castIndex < 0)
                {
                    return (false, $@"本体に {character} が登録されていません。");
                }

                // 適切な行を選択
                var (ok, failMessage) = this.SelectSpeechDataGridRowForInput(grid);
                if (!ok)
                {
                    return (false, failMessage);
                }

                // 改めて選択行のキャストセルコンボボックス取得
                combo = grid.GetCellContent(grid.SelectedRow, 0);

                // キャスト選択
                combo.SelectedIndex = castIndex;
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
                return (false, @"キャストの選択に失敗しました。");
            }

            return true;
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
                // 選択行のセリフセルテキスト取得
                return grid.GetCellText(grid.SelectedRow, 1);
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
            // 文章を整形
            var speechText = TextFormatter.Format(text, this.IsTextSeparatingByLineBreaks);
            if (string.IsNullOrWhiteSpace(speechText))
            {
                return (false, @"空白文を設定することはできません。");
            }

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
                var combo = grid.GetCellContent(grid.SelectedRow, 0);
                castIndex = (int)combo.SelectedIndex;
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
                var row = grid.SelectedRow;

                // 選択行のキャストインデックス設定
                if (castIndex >= 0)
                {
                    grid.GetCellContent(row, 0).SelectedIndex = castIndex;
                }

                // 選択行のセリフセルに文字列設定
                var async = new Async();
                var result = grid.EditCellText(row, 1, speechText, async);

                // ダイアログが出る可能性があるためそれを待つ
                // ダイアログが出ずに完了した場合は成功
                var modalWin = new WindowControl(mainWin).WaitForNextModal(async);
                if (modalWin != null)
                {
                    return (false, @"本体側でダイアログが表示されました。");
                }

                // 完了済みのはずだが念のため待機
                async.WaitForCompletion();

                // 処理結果を確認
                if (!(bool)result)
                {
                    return (false, @"本体のセリフ一覧表を編集できません。");
                }
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex, true);
                return (false, @"セリフの設定に失敗しました。");
            }

            return true;
        }

        /// <summary>
        /// 現在のパラメータ一覧を取得する。
        /// </summary>
        /// <param name="targetParameterIds">
        /// 取得対象のパラメータID列挙。 null ならば存在する全パラメータを対象とする。
        /// </param>
        /// <returns>
        /// パラメータIDとその値のディクショナリ。取得できなかった場合は null 。
        /// </returns>
        protected override Result<Dictionary<ParameterId, decimal>> GetParametersImpl(
            IEnumerable<ParameterId> targetParameterIds)
        {
            // パラメータスライダー群を取得
            var (sliders, failMessage) = this.ParameterSliders.Get(targetParameterIds);
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
                    dict.Add(id, (decimal)(double)idSlider.Value.Value);
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
            var (sliders, slidersMessage) =
                this.ParameterSliders.Get(parameters.Select(kv => kv.Key));
            if (sliders == null)
            {
                return (null, slidersMessage);
            }

            // 感情パラメータ値はすべて最小値にすることができないため、
            // 大きい値から順に設定することで極力想定通りの値が設定できるようにする。

            // スライダーと設定値のペアにして、感情関連を設定値の大きい順にソート
            var idSliderValues =
                sliders
                    .Select(
                        idSlider =>
                            (
                                id: idSlider.Key,
                                slider: idSlider.Value,
                                value: parameters.First(kv => kv.Key == idSlider.Key).Value
                            ))
                    .OrderByDescending(
                        isv => isv.id.IsEmotion() ? isv.value : decimal.MaxValue);

            // 自動試聴トグルボタンを取得
            var (autoPlayButton, autoPlayButtonMessage) = this.AutoPlayToggle.Get();
            if (autoPlayButton == null)
            {
                return (null, autoPlayButtonMessage);
            }

            var dict = new Dictionary<ParameterId, Result<bool>>();
            bool? autoPlay = null;

            try
            {
                // 自動試聴を無効化する
                autoPlay = (bool?)autoPlayButton.IsChecked;
                if (autoPlay != false)
                {
                    autoPlayButton.IsChecked = (bool?)false;
                }

                foreach (var (id, slider, value) in idSliderValues)
                {
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
                        slider.Focus();
                        slider.Value = (double)value;
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
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
                return (null, @"本体のパラメータスライダー群の操作に失敗しました。");
            }
            finally
            {
                // 自動試聴を元に戻す
                // 失敗してもよい
                if (autoPlay != false)
                {
                    autoPlayButton.IsChecked = autoPlay;
                }
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
                if (!(bool)button.IsEnabled)
                {
                    return (false, @"本体の試聴/停止ボタンがクリックできない状態です。");
                }

                // IsChecked = true だと見た目は変化するが再生されない。
                // PerformClick を使う。

                // 試聴/停止トグルボタンをクリック
                var async = new Async();
                if ((bool?)button.IsChecked == true)
                {
                    // 試聴中ならば一旦停止させるためにクリック
                    PerformClick(button);
                }
                PerformClick(button, async);

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
                if (!(bool)button.IsEnabled)
                {
                    return (false, @"本体の試聴/停止ボタンがクリックできない状態です。");
                }
                if ((bool?)button.IsChecked == false)
                {
                    // 既に停止しているので何もしない
                    return (true, @"停止済みです。");
                }

                // IsChecked = false だと見た目は変化するが停止されない。
                // PerformClick を使う。

                // 試聴/停止トグルボタンをクリック
                PerformClick(button);
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
                this.SaveFileImpl_ClickSaveMenu((DynamicAppVar)mainWin, grid, saveMenuAsync);
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
            result = this.SaveFileImpl_WaitSaving((DynamicAppVar)mainWin, saveMenuAsync);
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
        private Result<AppDataGrid> SaveFileImpl_SelectSingleDataGridRow()
        {
            // セリフデータグリッドを取得
            var (grid, failMessage) = this.SpeechDataGrid.Get();
            if (grid == null)
            {
                return (null, failMessage);
            }

            int selectedRow = -1;
            try
            {
                selectedRow = grid.SelectedRow;

                // 一旦別の行を選択して元の行に戻す
                grid.Focus();
                grid.SelectedRow = (selectedRow > 0) ? (selectedRow - 1) : 1;
                grid.SelectedRow = selectedRow;

                selectedRow = -1;
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
                if (selectedRow >= 0)
                {
                    try
                    {
                        grid.SelectedRow = selectedRow;
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
            dynamic mainWindow,
            AppDataGrid speechDataGrid,
            Async saveMenuAsync)
        {
            Debug.Assert((DynamicAppVar)mainWindow != null);
            Debug.Assert(speechDataGrid != null);
            Debug.Assert(saveMenuAsync != null);

            WindowControl fileDialog = null;

            try
            {
                // WAVE書き出しメニューアイテムを取得
                var menuItem = speechDataGrid.Control.ContextMenu.Items[12];

                if (!(bool)menuItem.IsEnabled)
                {
                    return (null, @"本体のWAV書き出しメニューがクリックできない状態です。");
                }

                // WAVE書き出しメニュークリック
                PerformClick(menuItem, saveMenuAsync);

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
        private Result<bool> SaveFileImpl_WaitSaving(dynamic mainWindow, Async saveMenuAsync)
        {
            Debug.Assert((DynamicAppVar)mainWindow != null);
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
                    var vtree = this.TargetAppVisualTree;

                    var border = vtree.GetDescendant(completeDialog, 0);
                    var buttonParent =
                        border
                            .Child          // StackPanel
                            .Children[1]    // Border
                            .Child          // StackPanel
                            .Children[1];   // Control
                    var button = vtree.GetDescendant(buttonParent, 0);

                    PerformClick(button);
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

        #region ICreativeStudioOperation の実装

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
                    ref this.castSpeechInputRow,
                    EnumCache<CastSpeechInputRow>.HashSet.Contains(value) ?
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
        public bool IsTextSeparatingByLineBreaks
        {
            get => this.textSeparatedByLineBreaks;
            set => this.SetProperty(ref this.textSeparatedByLineBreaks, value);
        }
        private bool textSeparatedByLineBreaks = false;

        /// <summary>
        /// 有効キャストの一覧を取得する。
        /// </summary>
        /// <returns>有効キャスト配列。取得できなかった場合は null 。</returns>
        public Result<ReadOnlyCollection<Cast>> GetAvailableCasts()
        {
            var (names, failMessage) = this.GetAvailableCharacters();
            if (names == null)
            {
                return (null, failMessage);
            }

            return
                Array.AsReadOnly(
                    names
                        .Select(name => CastExtension.FindByName(name))
                        .Where(cast => cast.HasValue)
                        .Select(cast => cast.Value)
                        .ToArray());
        }

        /// <summary>
        /// 現在選択されているキャストを取得する。
        /// </summary>
        /// <returns>キャスト。取得できなかった場合は null 。</returns>
        public Result<Cast?> GetCast()
        {
            var (name, failMessage) = this.GetCharacter();
            if (name == null)
            {
                return (null, failMessage);
            }

            var cast = CastExtension.FindByName(name);
            if (!cast.HasValue)
            {
                return (null, @"非対応のキャストが選択されています。");
            }

            return cast.Value;
        }

        /// <summary>
        /// キャストを選択させる。
        /// </summary>
        /// <param name="cast">キャスト。</param>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        public Result<bool> SetCast(Cast cast)
        {
            var name = cast.GetName();
            if (name == null)
            {
                return (false, @"不正なキャストは設定できません。");
            }

            return this.SetCharacter(name);
        }

        #endregion
    }
}
