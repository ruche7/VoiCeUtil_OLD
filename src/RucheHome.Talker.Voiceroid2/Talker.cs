using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Codeer.Friendly;
using Codeer.Friendly.Dynamic;
using Codeer.Friendly.Windows.Grasp;
using Codeer.Friendly.Windows.NativeStandardControls;
using RM.Friendly.WPFStandardControls;
using RucheHome.Diagnostics;
using RucheHome.Talker.Friendly;

namespace RucheHome.Talker.Voiceroid2
{
    /// <summary>
    /// VOICEROID2プロセスを操作する <see cref="IProcessTalker"/> 実装クラス。
    /// </summary>
    public class Talker : WpfProcessTalkerBase<ParameterId>
    {
        /// <summary>
        /// コンストラクタ。
        /// </summary>
        public Talker()
            :
            base(
                ClrVersion.V4,
                @"VoiceroidEditor",
                @"VOICEROID2 Editor",
                @"VOICEROID2",
                canSaveBlankText: true,
                hasCharacters: true)
        {
        }

        /// <summary>
        /// 文章入力欄下にあるボタンの種別列挙。
        /// </summary>
        /// <remarks>
        /// 値がそのままロジカルツリーインデックスを表す。
        /// </remarks>
        private enum MainButton
        {
            /// <summary>
            /// 再生
            /// </summary>
            Play = 0,

            /// <summary>
            /// 停止
            /// </summary>
            Stop = 1,

            /// <summary>
            /// 先頭
            /// </summary>
            Head = 2,

            /// <summary>
            /// 末尾
            /// </summary>
            Tail = 3,

            /// <summary>
            /// 音声保存
            /// </summary>
            Save = 5,

            /// <summary>
            /// 再生時間
            /// </summary>
            Time = 6,
        }

        /// <summary>
        /// Func デリゲートを用いてコントロールから要素を取得する。
        /// </summary>
        /// <param name="root">コントロール。</param>
        /// <param name="func">Func デリゲート。</param>
        /// <returns>Func デリゲートの戻り値。 root が null ならば default(T) 。</returns>
        private static T GetByFunc<T>(AppVar root, Func<dynamic, T> func)
        {
            Debug.Assert(func != null);

            if (root != null)
            {
                try
                {
                    return func(root.Dynamic());
                }
                catch (Exception ex)
                {
                    ThreadDebug.WriteException(ex);
                }
            }

            return default(T);
        }

        /// <summary>
        /// 文章入力欄下にあるボタン群の親コントロールを取得する。
        /// </summary>
        /// <param name="mainWindow">メインウィンドウ。</param>
        /// <returns>コントロール。取得できなかった場合は null 。</returns>
        private static AppVar GetMainButtonsParent(AppVar mainWindow) =>
            GetByFunc(
                mainWindow,
                win =>
                    win
                        .Content
                        .Children[1]
                        .Children[0]
                        .Children[2]
                        .Children[0]
                        .Content
                        .Children[1]);

        /// <summary>
        /// 文章入力欄下にあるボタンコントロールを取得する。
        /// </summary>
        /// <param name="parent">ボタン群の親コントロール。</param>
        /// <param name="button">ボタン種別。</param>
        /// <returns>コントロール。取得できなかった場合は null 。</returns>
        private static WPFButtonBase GetMainButton(AppVar parent, MainButton button)
        {
            if (parent == null)
            {
                return null;
            }

            var index = (int)button;
            if (index < 0)
            {
                return null;
            }

            try
            {
                var children = parent.Dynamic().Children;
                if (index < (int)children.Count)
                {
                    return new WPFButtonBase(children[index]);
                }
            }
            catch (Exception ex)
            {
                ThreadDebug.WriteException(ex);
            }

            return null;
        }

        /// <summary>
        /// 文章入力欄コントロールを取得する。
        /// </summary>
        /// <param name="mainWindow">メインウィンドウ。</param>
        /// <returns>コントロール。取得できなかった場合は null 。</returns>
        private static WPFTextBox GetMainTextBox(AppVar mainWindow) =>
            GetByFunc(
                mainWindow,
                win =>
                    new WPFTextBox(
                        win
                            .Content
                            .Children[1]
                            .Children[0]
                            .Children[2]
                            .Children[0]
                            .Content
                            .Children[0]));

        /// <summary>
        /// ボイスプリセット一覧タブコントロールを取得する。
        /// </summary>
        /// <param name="mainWindow">メインウィンドウ。</param>
        /// <returns>コントロール。取得できなかった場合は null 。</returns>
        private static WPFTabControl GetPresetTabControl(AppVar mainWindow) =>
            GetByFunc(
                mainWindow,
                win => new WPFTabControl(win.Content.Children[1].Children[0].Children[0]));

        /// <summary>
        /// ボイスプリセット一覧リストビューを取得する。
        /// </summary>
        /// <param name="presetTabControl">ボイスプリセット一覧タブコントロール。</param>
        /// <param name="index">タブインデックス。</param>
        /// <returns>コントロール。取得できなかった場合は null 。</returns>
        private static WPFListView GetPresetListView(
            WPFTabControl presetTabControl,
            int index)
            =>
            (presetTabControl == null || index < 0 || index >= 2) ?
                null :
                GetByFunc(
                    presetTabControl.AppVar,
                    tc => new WPFListView(tc.Items[index].Content.Content.Children[0]));

        /// <summary>
        /// ボイスプリセット一覧リストビューアイテムからボイスプリセット名を取得する。
        /// </summary>
        /// <param name="presetListViewItem">
        /// ボイスプリセット一覧リストビューアイテム。
        /// </param>
        /// <returns>ボイスプリセット名。取得できなかった場合は null 。</returns>
        /// <remarks>
        /// ビジュアルツリーから走査するため、リストビューが表示状態であること。
        /// </remarks>
        private static string GetPresetName(WPFListViewItem presetListViewItem)
        {
            // Content.PresetName から取得するのが手っ取り早いのだが、
            // VOICEROID2定義の型を操作することになるのでやめておく。

            try
            {
                var blocks = presetListViewItem.VisualTree().ByType<TextBlock>();
                if (blocks.Count >= 2)
                {
                    return blocks[1].Dynamic().Text;
                }
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
            }
            return null;
        }

        /// <summary>
        /// ウィンドウ下部の操作タブコントロールを取得する。
        /// </summary>
        /// <param name="mainWindow">メインウィンドウ。</param>
        /// <returns>コントロール。取得できなかった場合は null 。</returns>
        private static WPFTabControl GetOperationTabControl(AppVar mainWindow) =>
            GetByFunc(
                mainWindow,
                win => new WPFTabControl(win.Content.Children[1].Children[2]));

        /// <summary>
        /// ボイスプリセットタブコントロールに対する処理を行うデリゲート型。
        /// </summary>
        /// <typeparam name="T">処理結果値の型。</typeparam>
        /// <param name="presetTabControl">ボイスプリセット一覧タブコントロール。</param>
        /// <param name="tabItemCount">タブアイテム数。</param>
        /// <param name="selectedIndex">現在選択中のタブアイテムインデックス。</param>
        /// <returns>処理結果。</returns>
        private delegate Result<T> PresetTabControlDelegate<T>(
            WPFTabControl presetTabControl,
            int tabItemCount,
            int selectedIndex);

        /// <summary>
        /// ボイスプリセットタブコントロールに対する処理を行う。
        /// </summary>
        /// <typeparam name="T">処理結果値の型。</typeparam>
        /// <param name="processor">処理デリゲート。</param>
        /// <param name="tabSelectionRollbackDecider">
        /// 処理完了時に選択タブアイテムを最初の状態に戻すか否かを判断するデリゲート。
        /// 第1引数には processor が完了したならばその戻り値、
        /// そうでなければ null が渡される。
        /// 第2引数には processor が例外送出したならばその例外、
        /// そうでなければ null が渡される。
        /// このデリゲートが true を返すならば戻し処理を実施する。
        /// null を指定した場合は常に戻し処理を実施する。
        /// </param>
        /// <returns>処理デリゲートの戻り値。開始処理に失敗した場合は default(T) 。</returns>
        private Result<T> ProcessPresetTabControl<T>(
            PresetTabControlDelegate<T> processor,
            Func<Result<T>?, Exception, bool> tabSelectionRollbackDecider = null)
        {
            ArgumentValidation.IsNotNull(processor, nameof(processor));

            // メインウィンドウを検索
            var mainWin = this.FindMainWindow();
            if (mainWin == null)
            {
                return (default(T), @"本体のウィンドウが見つかりません。");
            }

            // タブコントロールを取得
            var tabControl = GetPresetTabControl(mainWin);
            if (tabControl == null)
            {
                return (default(T), @"本体のボイスプリセットタブページが見つかりません。");
            }

            // 現在選択中のタブアイテムインデックスとタブアイテム数を保存
            int tabIndex = 0, tabItemCount = 0;
            try
            {
                tabIndex = tabControl.SelectedIndex;
                tabItemCount = tabControl.ItemCount;
                if (tabIndex < 0 || tabItemCount <= 0)
                {
                    return (default(T), @"本体のボイスプリセットタブページが見つかりません");
                }
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
                return (
                    default(T),
                    @"本体のボイスプリセットタブページから情報を取得できませんでした。");
            }

            bool? rollback = false;
            try
            {
                var result = processor(tabControl, tabItemCount, tabIndex);
                rollback = tabSelectionRollbackDecider?.Invoke(result, null);

                return result;
            }
            catch (Exception ex)
            {
                rollback = tabSelectionRollbackDecider?.Invoke(null, ex);

                throw ex;
            }
            finally
            {
                // 戻り処理有効ならば元のタブアイテムを選択する
                // 失敗してもよい
                try
                {
                    if (rollback != false && tabIndex != tabControl.SelectedIndex)
                    {
                        tabControl.EmulateChangeSelectedIndex(tabIndex);
                    }
                }
                catch (Exception ex)
                {
                    ThreadDebug.WriteException(ex);
                }
            }
        }

        /// <summary>
        /// パラメータを保持するスライダーに対する処理を行うデリゲート。
        /// </summary>
        /// <typeparam name="T">処理結果値の型。</typeparam>
        /// <param name="id">パラメータID。</param>
        /// <param name="slider">スライダー。</param>
        /// <returns>
        /// 処理結果値。
        /// failMessage は成功ならば null 、失敗ならばエラーメッセージとすること。
        /// </returns>
        private delegate (T result, string failMessage) ParameterDelegate<T>(
            ParameterId id,
            WPFSlider slider);

        /// <summary>
        /// パラメータを保持するスライダー群に対して処理を行う。
        /// </summary>
        /// <typeparam name="T">処理結果値の型。</typeparam>
        /// <param name="processor">
        /// 処理デリゲート。
        /// 戻り値の failMessage は成功ならば null 、失敗ならばエラーメッセージとすること。
        /// </param>
        /// <param name="targetParameterIds">
        /// 取得対象パラメータID列挙。 null ならばすべて対象。
        /// </param>
        /// <returns>処理結果のディクショナリ。失敗した場合は null 。</returns>
        private Result<Dictionary<ParameterId, T>> ProcessParameterSliders<T>(
            ParameterDelegate<T> processor,
            IEnumerable<ParameterId> targetParameterIds = null)
        {
            ArgumentValidation.IsNotNull(processor, nameof(processor));

            // メインウィンドウを検索
            var mainWin = this.FindMainWindow();
            if (mainWin == null)
            {
                return (null, @"本体のウィンドウが見つかりません。");
            }

            // タブコントロールを取得
            var tabControl = GetOperationTabControl(mainWin);
            if (tabControl == null)
            {
                return (null, @"本体のタブページが見つかりません。");
            }

            // タブアイテムコレクションを取得
            dynamic tabItems;
            try
            {
                tabItems = tabControl.Dynamic().Items;
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
                return (null, @"本体のタブページが見つかりません。");
            }

            var dict = new Dictionary<ParameterId, T>();

            var allIds = targetParameterIds ?? ParameterIdExtension.AllIds;
            var allIdGroups = allIds.Select(id => (id: id, gi: id.GetGuiGroup()));

            // マスタータブ
            var masterSounds =
                allIdGroups.Where(
                    v => v.gi.group == ParameterIdExtension.GuiGroup.MasterSound);
            var masterPauses =
                allIdGroups.Where(
                    v => v.gi.group == ParameterIdExtension.GuiGroup.MasterPause);
            if (masterSounds.Any() || masterPauses.Any())
            {
                var sliders = new Dictionary<ParameterId, WPFSlider>();

                try
                {
                    var bases = tabItems[0].Content.Content.Children[0].Content.Children;
                    if (masterSounds.Any())
                    {
                        var children = bases[1].Children;
                        foreach (var v in masterSounds)
                        {
                            sliders.Add(
                                v.id,
                                new WPFSlider(children[v.gi.index].Content.Children[2]));
                        }
                    }
                    if (masterPauses.Any())
                    {
                        var children = bases[3].Children;
                        foreach (var v in masterPauses)
                        {
                            sliders.Add(
                                v.id,
                                new WPFSlider(children[v.gi.index].Content.Children[2]));
                        }
                    }
                }
                catch (Exception ex)
                {
                    ThreadTrace.WriteException(ex);
                    return (null, @"本体のマスタータブのスライダーが見つかりませんでした。");
                }

                foreach (var s in sliders)
                {
                    var (result, failMessage) = processor(s.Key, s.Value);
                    if (failMessage != null)
                    {
                        return (null, failMessage);
                    }

                    dict.Add(s.Key, result);
                }
            }

            // ボイスタブ
            var presetSounds =
                allIdGroups.Where(
                    v => v.gi.group == ParameterIdExtension.GuiGroup.PresetSound);
            var presetEmotions =
                allIdGroups.Where(
                    v => v.gi.group == ParameterIdExtension.GuiGroup.PresetEmotion);
            if (presetSounds.Any() || presetEmotions.Any())
            {
                var sliders = new Dictionary<ParameterId, WPFSlider>();
                int tabIndex = -1;

                try
                {
                    try
                    {
                        var bases = tabItems[1].Content.Content.Children[2].Content.Children;
                        if (presetSounds.Any())
                        {
                            var children = bases[1].Children;
                            foreach (var v in presetSounds)
                            {
                                sliders.Add(
                                    v.id,
                                    new WPFSlider(children[v.gi.index].Content.Children[2]));
                            }
                        }
                        if (presetEmotions.Any())
                        {
                            var baseListBox = new WPFListBox(bases[5]);

                            // 感情は表示されている場合のみ処理可能
                            if (baseListBox.Visibility == Visibility.Visible)
                            {
                                // 直接 Items をいじるのが手っ取り早いのだが、
                                // VOICEROID2定義の型を操作することになるのでやめておく。

                                var ti = tabControl.SelectedIndex;
                                if (ti != 1)
                                {
                                    // 元のタブインデックスを保存してボイスタブ選択
                                    tabIndex = ti;
                                    tabControl.EmulateChangeSelectedIndex(1);
                                }

                                // ビジュアルツリーからスライダーリストを得る
                                var children = baseListBox.VisualTree().ByType<Slider>();
                                foreach (var v in presetEmotions)
                                {
                                    sliders.Add(v.id, new WPFSlider(children[v.gi.index]));
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        ThreadTrace.WriteException(ex);
                        return (
                            null,
                            @"本体のボイスタブのスライダーが見つかりませんでした。");
                    }

                    foreach (var s in sliders)
                    {
                        var (result, failMessage) = processor(s.Key, s.Value);
                        if (failMessage != null)
                        {
                            return (null, failMessage);
                        }

                        dict.Add(s.Key, result);
                    }
                }
                finally
                {
                    // 元のタブを選択する
                    // 失敗してもよい
                    if (tabIndex >= 0)
                    {
                        try
                        {
                            tabControl.EmulateChangeSelectedIndex(tabIndex);
                        }
                        catch (Exception ex)
                        {
                            ThreadDebug.WriteException(ex);
                        }
                    }
                }
            }

            return dict;
        }

        /// <summary>
        /// スプラッシュスクリーンのウィンドウタイトル。
        /// </summary>
        private const string SplashScreenWindowTitle = @"SplashScreen";

        /// <summary>
        /// 音声保存オプションウィンドウのタイトル。
        /// </summary>
        private const string SaveOptionWindowTitle = @"音声保存";

        /// <summary>
        /// 音声ファイル保存ダイアログのウィンドウタイトル。
        /// </summary>
        private const string SaveFileDialogTitle = @"名前を付けて保存";

        /// <summary>
        /// 音声保存進捗ウィンドウのウィンドウタイトル。
        /// </summary>
        private const string SaveProgressWindowTitle = @"音声保存";

        /// <summary>
        /// 音声完了ダイアログのウィンドウタイトル。
        /// </summary>
        private const string SaveCompleteDialogTitle = @"情報";

        #region FriendlyProcessTalkerBase<ParameterId> のオーバライド

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
            if (
                title == SaveOptionWindowTitle ||
                title == SaveFileDialogTitle ||
                title == SaveProgressWindowTitle)
            {
                return WindowTitleKind.FileSaving;
            }
            if (title.StartsWith(this.TalkerName))
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
        protected override Result<TalkerState> CheckState(AppVar mainWindow)
        {
            // 音声保存ボタンを探す
            var saveButton =
                GetMainButton(GetMainButtonsParent(mainWindow), MainButton.Save);
            if (saveButton == null)
            {
                // ウィンドウ構築途中or破棄途中であると判断
                // 即ち起動中or終了中
                return
                    (this.State == TalkerState.None || this.State == TalkerState.Startup) ?
                        TalkerState.Startup : TalkerState.Cleanup;
            }

            // 音声保存ボタンが無効ならば読み上げ中と判断する
            return (saveButton.IsEnabled ? TalkerState.Idle : TalkerState.Speaking);
        }

        #endregion

        #region ProcessTalkerBase<ParameterId> のオーバライド

        /// <summary>
        /// 有効キャラクターの一覧を取得する。
        /// </summary>
        /// <returns>有効キャラクター配列。取得できなかった場合は null 。</returns>
        protected override Result<ReadOnlyCollection<string>> GetAvailableCharactersImpl()
        {
            // ボイスプリセット名一覧取得を行うローカルメソッド
            Result<ReadOnlyCollection<string>> getAvailableCharacters(
                WPFTabControl tabControl,
                int tabItemCount,
                int _)
            {
                var names = new List<string>();

                for (int ti = 0; ti < tabItemCount; ++ti)
                {
                    // タブアイテム選択
                    tabControl.EmulateChangeSelectedIndex(ti);

                    // リストビュー取得
                    var listView = GetPresetListView(tabControl, ti);
                    if (listView == null)
                    {
                        return (null, @"本体のボイスプリセット一覧が見つかりません。");
                    }

                    // リストビューアイテムを走査
                    var listItemCount = listView.ItemCount;
                    for (int li = 0; li < listItemCount; ++li)
                    {
                        var name = GetPresetName(listView.GetItem(li));
                        if (name == null)
                        {
                            return (null, @"本体のボイスプリセット名を取得できません。");
                        }

                        names.Add(name);
                    }
                }

                return names.AsReadOnly();
            }

            try
            {
                return this.ProcessPresetTabControl(getAvailableCharacters);
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
            }
            return (null, @"ボイスプリセット名一覧の取得に失敗しました。");
        }

        /// <summary>
        /// 現在選択されているキャラクターを取得する。
        /// </summary>
        /// <returns>キャラクター。取得できなかった場合は null 。</returns>
        protected override Result<string> GetCharacterImpl()
        {
            // 選択中ボイスプリセット名取得を行うローカルメソッド
            Result<string> getCharacter(
                WPFTabControl tabControl,
                int tabItemCount,
                int selectedTabIndex)
            {
                var tabIndex = selectedTabIndex;

                do
                {
                    // 現在のタブアイテムからリストビュー取得
                    var listView = GetPresetListView(tabControl, tabIndex);
                    if (listView == null)
                    {
                        return (null, @"本体のボイスプリセット一覧が見つかりません。");
                    }

                    // 選択中アイテムがあるか確認
                    var selectedIndex = listView.SelectedIndex;
                    if (selectedIndex >= 0)
                    {
                        // 選択中アイテムから名前を取得して返す
                        var name = GetPresetName(listView.GetItem(selectedIndex));
                        return (
                            name,
                            (name == null) ?
                                @"本体のボイスプリセット名を取得できません。" : null);
                    }

                    // 次のタブアイテムへ
                    tabIndex = (tabIndex + 1) % tabItemCount;
                    tabControl.EmulateChangeSelectedIndex(tabIndex);
                }
                while (tabIndex != selectedTabIndex);

                return (null, @"ボイスプリセットが選択されていません。");
            }

            try
            {
                return this.ProcessPresetTabControl(getCharacter);
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
            }
            return (null, @"選択中ボイスプリセット名の取得に失敗しました。");
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
            // ボイスプリセット選択を行うローカルメソッド
            Result<bool> setCharacter(
                WPFTabControl tabControl,
                int tabItemCount,
                int selectedTabIndex)
            {
                var tabIndex = selectedTabIndex;

                do
                {
                    // 現在のタブアイテムからリストビュー取得
                    var listView = GetPresetListView(tabControl, tabIndex);
                    if (listView == null)
                    {
                        return (false, @"本体のボイスプリセット一覧が見つかりません。");
                    }

                    // リストビューアイテムを走査
                    var listItemCount = listView.ItemCount;
                    for (int li = 0; li < listItemCount; ++li)
                    {
                        var name = GetPresetName(listView.GetItem(li));
                        if (name == character)
                        {
                            // 見つかったので選択
                            listView.EmulateChangeSelectedIndex(li);
                            return true;
                        }
                    }

                    // 次のタブアイテムへ
                    tabIndex = (tabIndex + 1) % tabItemCount;
                    tabControl.EmulateChangeSelectedIndex(tabIndex);
                }
                while (tabIndex != selectedTabIndex);

                return (false, @"対象ボイスプリセットが見つかりません。");
            }

            try
            {
                // 失敗時のみ元のタブに戻すようにする
                return
                    this.ProcessPresetTabControl(
                        setCharacter,
                        (r, ex) => r?.Value != true || ex != null);
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
            }
            return (false, @"ボイスプリセットの選択に失敗しました。");
        }

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
            var textBox = GetMainTextBox(mainWin);
            if (textBox == null)
            {
                return (null, @"本体の文章入力欄が見つかりません。");
            }

            try
            {
                return textBox.Text;
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
            var textBox = GetMainTextBox(mainWin);
            if (textBox == null)
            {
                return (false, @"本体の文章入力欄が見つかりません。");
            }

            // 500文字あたり1ミリ秒をタイムアウト値に追加
            var timeout = StandardTimeoutMilliseconds + (text.Length / 500);

            try
            {
                // 文章入力欄にテキストを設定
                bool done =
                    WaitAsyncAction(
                        async => textBox.EmulateChangeText(text, async),
                        timeout);
                if (!done)
                {
                    return (false, @"文章設定処理がタイムアウトしました。");
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
            // パラメータ値を取得するローカルメソッド
            (decimal result, string failMessage) getParameter(
                ParameterId id,
                WPFSlider slider)
            {
                try
                {
                    return ((decimal)slider.Value, null);
                }
                catch (Exception ex)
                {
                    ThreadDebug.WriteException(ex);
                }
                return (
                    0,
                    id.GetTabItemName() + @"タブの" +
                    id.GetInfo().DisplayName + @"の値が不正です。");
            }

            try
            {
                return this.ProcessParameterSliders(getParameter);
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
            }
            return (null, @"パラメータの取得に失敗しました。");
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
            // setParameter の戻り値を作成するローカルメソッド
            (Result<bool> result, string failMessage) makeSetParameterResult(
                bool value,
                string message = null)
                =>
                (new Result<bool>(value, message), null);

            // パラメータ値を設定するローカルメソッド
            (Result<bool> result, string failMessage) setParameter(
                ParameterId id,
                WPFSlider slider)
            {
                var value = parameters.First(kv => kv.Key == id).Value;
                var info = id.GetInfo();
                var format = @"F" + info.Digits;

                // 範囲チェック
                if (value < info.MinValue)
                {
                    return
                        makeSetParameterResult(
                            false,
                            $@"最小許容値 {info.MinValue.ToString(format)} " +
                            $@"より小さい値 {value.ToString(format)} は設定できません。");
                }
                if (value > info.MaxValue)
                {
                    return
                        makeSetParameterResult(
                            false,
                            $@"最大許容値 {info.MaxValue.ToString(format)} " +
                            $@"より大きい値 {value.ToString(format)} は設定できません。");
                }

                try
                {
                    slider.EmulateChangeValue((double)value);
                    return makeSetParameterResult(true);
                }
                catch (Exception ex)
                {
                    ThreadTrace.WriteException(ex);
                    return
                        makeSetParameterResult(
                            false,
                            ex.Message ?? (ex.GetType().Name + @" 例外が発生しました。"));
                }
            }

            try
            {
                return
                    this.ProcessParameterSliders(
                        setParameter,
                        parameters.Select(kv => kv.Key));
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
            }
            return (null, @"パラメータの設定に失敗しました。");
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
                if (!play.IsEnabled)
                {
                    return (false, @"本体の再生ボタンがクリックできない状態です。");
                }

                // 先頭ボタン取得してクリック
                // 失敗しても先へ進む
                var head = GetMainButton(parent, MainButton.Head);
                try
                {
                    if (head?.IsEnabled == true)
                    {
                        head.EmulateClick();
                    }
                }
                catch (Exception ex)
                {
                    ThreadDebug.WriteException(ex);
                }

                // 再生ボタンクリック
                var playAsync = new Async();
                play.EmulateClick(playAsync);

                // フレーズ編集未保存の場合等はダイアログが出るためそれを待つ
                // ダイアログが出ずに完了した場合は成功
                var modalWin = new WindowControl(mainWin).WaitForNextModal(playAsync);
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
                if (!stop.IsEnabled)
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
            result = this.SaveFileImpl_WaitSaving(saveButtonAsync);
            if (!result.Value)
            {
                return (null, result.Message);
            }

            // ファイル保存確認
            result = this.SaveFileImpl_CheckFileSaved(waveFilePath);
            if (!result.Value)
            {
                // 連番ファイルの場合があるのでそちらも確認
                waveFilePath = FilePath.ToSequential(waveFilePath, 0);
                result = this.SaveFileImpl_CheckFileSaved(waveFilePath);
                if (!result.Value)
                {
                    return (null, result.Message);
                }
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
                if (!button.IsEnabled)
                {
                    return (null, @"本体の音声保存ボタンがクリックできない状態です。");
                }

                // 音声保存ボタンクリック
                button.EmulateClick(saveButtonAsync);

                // ファイルダイアログ or オプションウィンドウ(or 警告ダイアログ)を待つ
                fileDialog = new WindowControl(mainWindow).WaitForNextModal(saveButtonAsync);
                if (fileDialog == null)
                {
                    return (null, @"本体の音声保存ダイアログが見つかりません。");
                }

                var title = fileDialog.GetWindowText();

                // オプションウィンドウか？
                if (title == SaveOptionWindowTitle)
                {
                    // OKボタン取得
                    var optionOkButton =
                        new WPFButtonBase(
                            fileDialog.Dynamic().Content.Children[1].Children[0]);

                    if (!optionOkButton.IsEnabled)
                    {
                        return (null, @"設定画面のOKボタンがクリックできない状態です。");
                    }

                    // OKボタンクリック
                    var okButtonAsync = new Async();
                    optionOkButton.EmulateClick(okButtonAsync);

                    // ファイルダイアログを待つ
                    fileDialog = fileDialog.WaitForNextModal(okButtonAsync);
                    if (fileDialog == null)
                    {
                        return (null, @"本体の音声保存ダイアログが見つかりません。");
                    }

                    title = fileDialog.GetWindowText();
                }

                // 音声ファイル保存ダイアログか？
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
        /// <param name="saveButtonAsync">
        /// 音声保存ボタンクリック処理に用いた非同期オブジェクト。
        /// </param>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        private Result<bool> SaveFileImpl_WaitSaving(Async saveButtonAsync)
        {
            Debug.Assert(saveButtonAsync != null);

            try
            {
                WindowControl completeDialog = null;

                // 保存完了ダイアログを探す
                for (; !saveButtonAsync.IsCompleted; Thread.Yield())
                {
                    // トップレベルウィンドウ群取得
                    var topWins = WindowControl.GetTopLevelWindows(this.TargetApp);

                    // 保存完了ダイアログと思われるウィンドウはあるか？
                    var dialog =
                        topWins.FirstOrDefault(
                            win => win.GetWindowText() == SaveCompleteDialogTitle);
                    if (dialog != null)
                    {
                        // 親ウィンドウの型名で調べる方がより正確だが、
                        // VOICEROID2定義の型名を判定に使うことになるのでやめておく。

                        // 親ウィンドウが進捗ウィンドウまたはオプションウィンドウか？
                        var parentTitle = dialog.ParentWindow?.GetWindowText();
                        if (
                            parentTitle == SaveProgressWindowTitle ||
                            parentTitle == SaveOptionWindowTitle)
                        {
                            completeDialog = dialog;
                            break;
                        }

                        // トップレベルに進捗ウィンドウもあるか？
                        // オプションウィンドウを表示している場合はこちらの場合もある
                        if (
                            topWins.Any(
                                win => win.GetWindowText() == SaveProgressWindowTitle))
                        {
                            completeDialog = dialog;
                            break;
                        }
                    }
                }

                if (completeDialog == null)
                {
                    return (false, @"本体の保存完了ダイアログが見つかりません。");
                }

                // 保存完了ダイアログのOKボタンを押下する
                // 失敗してもよい
                try
                {
                    var button = completeDialog.IdentifyFromWindowClass(@"Button");
                    if (button != null)
                    {
                        new NativeButton(button).EmulateClick();
                    }
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
    }
}
