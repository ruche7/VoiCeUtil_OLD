using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using Codeer.Friendly.Windows;
using Codeer.Friendly.Windows.Grasp;
using Codeer.Friendly.Windows.NativeStandardControls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RucheHome.Automation.Talkers;

namespace RucheHome.Tests.Automation.Talkers
{
    /// <summary>
    /// <see cref="ITalker"/> 実装クラスに共通するテストを提供する抽象クラス。
    /// </summary>
    public abstract class TalkerTestBase<TTalker>
        where TTalker : ITalker
    {
        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="kind">Talker インスタンス種別。</param>
        public TalkerTestBase(TalkerKind kind)
        {
            this.TalkerKind = kind;
        }

        #region テストメソッド群

        [TestMethod]
        [TestCategory(nameof(ITalker))]
        public void Test_ITalker_TalkerName()
        {
            var talker = this.GetTalker();

            var talkerName = talker.TalkerName;
            Assert.IsNotNull(talkerName);
            Console.WriteLine(talkerName);
        }

        [TestMethod]
        [TestCategory(nameof(ITalker))]
        public void Test_ITalker_State()
        {
            var talker = this.GetTalker();

            var state = talker.State;
            Console.WriteLine(state);
            Assert.AreEqual(state, TalkerState.Idle);
        }

        [TestMethod]
        [TestCategory(nameof(ITalker))]
        public void Test_ITalker_IsAlive()
        {
            var talker = this.GetTalker();

            Assert.IsTrue(talker.IsAlive);
        }

        [TestMethod]
        [TestCategory(nameof(ITalker))]
        public void Test_ITalker_CanOperate()
        {
            var talker = this.GetTalker();

            Assert.IsTrue(talker.CanOperate);
        }

        [TestMethod]
        [TestCategory(nameof(ITalker))]
        public void Test_ITalker_StateMessage()
        {
            var talker = this.GetTalker();

            Console.WriteLine(talker.StateMessage);
        }

        [TestMethod]
        [TestCategory(nameof(ITalker))]
        public void Test_ITalker_GetAvailableCharacters_GetCharacters_SetCharacters()
        {
            var talker = this.GetTalker();

            // キャラクター設定を保持している場合のみテストする
            if (!talker.HasCharacters)
            {
                Console.WriteLine($@"[Skip] {nameof(talker.HasCharacters)} == false");
                return;
            }

            // 有効キャラクター配列取得
            ReadOnlyCollection<string> characters = null;
            {
                var r = talker.GetAvailableCharacters();
                Assert.IsNotNull(r.Value, r.Message);
                Assert.IsTrue(r.Value.Count > 0);
                CollectionAssert.AllItemsAreUnique(r.Value);
                this.ValidateAvailableCharacters(r.Value);

                characters = r.Value;
            }

            // 現在のキャラクターを取得
            string orgCharacter = null;
            {
                var r = talker.GetCharacter();
                Assert.IsNotNull(r.Value, r.Message);

                orgCharacter = r.Value;
                Console.WriteLine($@"{nameof(orgCharacter)} == {orgCharacter}");
            }

            foreach (var character in characters)
            {
                Console.WriteLine($@"{nameof(character)} == {character}");

                // キャラクター設定
                {
                    var r = talker.SetCharacter(character);
                    Assert.IsTrue(r.Value, r.Message);
                }

                // キャラクター取得
                {
                    var r = talker.GetCharacter();
                    Assert.IsNotNull(r.Value, r.Message);
                    Assert.AreEqual(r.Value, character);
                }
            }

            // 元のキャラクターに戻す
            {
                var r = talker.SetCharacter(orgCharacter);
                Assert.IsTrue(r.Value, r.Message);
            }
        }

        [TestMethod]
        [TestCategory(nameof(ITalker))]
        public virtual void Test_ITalker_SetText_GetText()
        {
            var talker = this.GetTalker();

            var text = "テキスト設定テストです。\nテスト\tテスト\tテスト。";

            // テキスト設定
            {
                var r = talker.SetText(text);
                Assert.IsTrue(r.Value, r.Message);
            }

            // テキスト取得
            {
                var r = talker.GetText();
                Assert.IsNotNull(r.Value, r.Message);

                // CeVIO用 Talker は改行やタブ文字を置換するので等価にはならない
                if (this.TalkerKind != TalkerKind.CeVIO)
                {
                    Assert.AreEqual(r.Value, text);
                }
            }
        }

        [TestMethod]
        [TestCategory(nameof(ITalker))]
        public void Test_ITalker_GetParameters_SetParameters()
        {
            var talker = this.GetTalker();
            var infos = this.GetParameterInfos();

            Dictionary<object, decimal> orgValues = null;

            // パラメータ群取得
            {
                var r = talker.GetParameters();
                PrintParameters(r.Value);
                Assert.IsNotNull(r.Value, r.Message);
                CollectionAssert.IsSubsetOf(r.Value.Keys, infos.Keys);

                orgValues = r.Value;
            }

            // 最小許容値設定
            {
                var r =
                    talker.SetParameters(
                        infos.ToDictionary(kv => kv.Key, kv => kv.Value.MinValue));
                Assert.IsNotNull(r.Value, r.Message);
                CollectionAssert.IsSubsetOf(r.Value.Keys, infos.Keys);
                foreach (var kv in r.Value)
                {
                    Assert.IsTrue(kv.Value.Value, $@"{kv.Key} : {kv.Value.Message}");
                }
            }

            // パラメータ群取得(最小許容値)
            {
                var r = talker.GetParameters();
                PrintParameters(r.Value);
                Assert.IsNotNull(r.Value, r.Message);
                CollectionAssert.IsSubsetOf(r.Value.Keys, infos.Keys);

                // CeVIOは感情値をすべて最小値にはできないため等価にならない
                if (this.TalkerKind != TalkerKind.CeVIO)
                {
                    CollectionAssert.AreEqual(
                        r.Value.Values,
                        r.Value.Keys
                            .Select(
                                id => infos.First(kv => id.Equals(kv.Key)).Value.MinValue)
                            .ToArray());
                }
            }

            // 最大許容値設定
            {
                var r =
                    talker.SetParameters(
                        infos.ToDictionary(kv => kv.Key, kv => kv.Value.MaxValue));
                Assert.IsNotNull(r.Value, r.Message);
                CollectionAssert.IsSubsetOf(r.Value.Keys, infos.Keys);
                foreach (var kv in r.Value)
                {
                    Assert.IsTrue(kv.Value.Value, $@"{kv.Key} : {kv.Value.Message}");
                }
            }

            // パラメータ群取得(最大許容値)
            {
                var r = talker.GetParameters();
                PrintParameters(r.Value);
                Assert.IsNotNull(r.Value, r.Message);
                CollectionAssert.IsSubsetOf(r.Value.Keys, infos.Keys);
                CollectionAssert.AreEqual(
                    r.Value.Values,
                    r.Value.Keys
                        .Select(id => infos.First(kv => id.Equals(kv.Key)).Value.MaxValue)
                        .ToArray());
            }

            // 初期値設定
            {
                var r = talker.SetParameters(orgValues);
                Assert.IsNotNull(r.Value, r.Message);
                CollectionAssert.IsSubsetOf(r.Value.Keys, infos.Keys);
                foreach (var kv in r.Value)
                {
                    Assert.IsTrue(kv.Value.Value, $@"{kv.Key} : {kv.Value.Message}");
                }
            }
        }

        [TestMethod]
        [TestCategory(nameof(ITalker))]
        public void Test_ITalker_SetText_Speak_Stop()
        {
            var talker = this.GetTalker();

            // 読み切らないよう長めのテキストにする
            var text = "テキスト読み上げテストです。\nテストテストテスト。\n";
            text += text;
            text += text;
            text += text;
            text += text;

            // テキスト設定
            {
                var r = talker.SetText(text);
                Assert.IsTrue(r.Value, r.Message);
            }

            // 読み上げ
            {
                var r = talker.Speak();
                Assert.IsTrue(r.Value, r.Message);
                Assert.AreEqual(talker.State, TalkerState.Speaking);
            }

            // 3秒待つ
            Thread.Sleep(3000);

            // 停止
            {
                var r = talker.Stop();
                Assert.IsTrue(r.Value, r.Message);
                Assert.AreEqual(talker.State, TalkerState.Idle);
            }
        }

        [TestMethod]
        [TestCategory(nameof(ITalker))]
        public void Test_ITalker_SetText_SaveFile_Normal()
        {
            var talker = this.GetTalker();
            var filePath = this.MakeSaveFilePath();

            // 進捗ウィンドウを出すためにランダムな長文にする
            var text = "音声ファイル保存テストです。\nテストテストテスト。\n";
            for (int i = 0; i < 50; ++i)
            {
                text += Path.GetRandomFileName();
            }

            // テキスト設定
            {
                var r = talker.SetText(text);
                Assert.IsTrue(r.Value, r.Message);
            }

            // 音声ファイル保存
            {
                var r = talker.SaveFile(filePath);
                Assert.IsNotNull(r.Value, r.Message);
                Console.WriteLine(r.Value);
                Assert.IsTrue(File.Exists(r.Value));

                // 削除しておく
                DeleteSavedFiles(r.Value);
            }
        }

        [TestMethod]
        [TestCategory(nameof(ITalker))]
        public void Test_ITalker_SetText_SaveFile_WhiteSpace()
        {
            var talker = this.GetTalker();
            var filePath = this.MakeSaveFilePath();

            // 空白文
            var text = "\r\n\t 　";
            for (int i = 0; i < 6; ++i)
            {
                text += text;
            }

            // テキスト設定
            {
                var r = talker.SetText(text);

                // CeVIO用 Talker はそもそも空白文の設定自体できない
                if (this.TalkerKind == TalkerKind.CeVIO)
                {
                    Assert.IsFalse(r.Value, r.Message);

                    // ダイアログが出ている可能性があるので閉じる
                    CloseAllModalsIfProcessTalker(talker);
                    return;
                }

                Assert.IsTrue(r.Value, r.Message);
            }

            // 音声ファイル保存
            {
                var r = talker.SaveFile(filePath);

                // 成否は CanSaveBlankText の値次第
                if (talker.CanSaveBlankText)
                {
                    Assert.IsNotNull(r.Value, r.Message);
                    Console.WriteLine(r.Value);
                    Assert.IsTrue(File.Exists(r.Value));

                    // 削除しておく
                    DeleteSavedFiles(r.Value);
                }
                else
                {
                    Assert.IsNull(r.Value);
                    Console.WriteLine(r.Message);

                    // ダイアログが出ている可能性があるので閉じる
                    CloseAllModalsIfProcessTalker(talker);
                }
            }
        }

        #endregion

        /// <summary>
        /// WM_CLOSE メッセージID値。
        /// </summary>
        private const int WM_CLOSE = 0x0010;

        /// <summary>
        /// パラメータ群を出力する。
        /// </summary>
        /// <param name="parameters">パラメータ群。</param>
        protected static void PrintParameters<TParameterId>(
            IEnumerable<KeyValuePair<TParameterId, decimal>> parameters)
        {
            if (parameters == null)
            {
                Console.WriteLine(@"{0}", null);
            }
            else
            {
                foreach (var kv in parameters)
                {
                    Console.WriteLine(@"{0} : {1}", kv.Key, kv.Value);
                }
            }
        }

        /// <summary>
        /// もし <see cref="IProcessTalker"/> 実装クラスならば、
        /// すべてのモーダルウィンドウに WM_CLOSE メッセージを送信する。
        /// </summary>
        protected static void CloseAllModalsIfProcessTalker(ITalker talker)
        {
            try
            {
                var processTalker = talker as IProcessTalker;
                if (processTalker == null)
                {
                    return;
                }
                var mainWinHandle = processTalker.MainWindowHandle;
                if (mainWinHandle == IntPtr.Zero)
                {
                    return;
                }

                using (var app = new WindowsAppFriend(mainWinHandle))
                {
                    while (true)
                    {
                        var topWin = WindowControl.FromZTop(app);

                        // メインウィンドウなら処理終了
                        if (topWin == null || topWin.Handle == mainWinHandle)
                        {
                            break;
                        }

                        topWin.SendMessage(WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// もし <see cref="IProcessTalker"/> 実装クラスならば、
        /// すべてのネイティブモーダルウィンドウの先頭ボタンをクリックする。
        /// </summary>
        protected static void ClickAllModalsFirstButtonIfProcessTalker(ITalker talker)
        {
            try
            {
                var processTalker = talker as IProcessTalker;
                if (processTalker == null)
                {
                    return;
                }
                var mainWinHandle = processTalker.MainWindowHandle;
                if (mainWinHandle == IntPtr.Zero)
                {
                    return;
                }

                using (var app = new WindowsAppFriend(mainWinHandle))
                {
                    while (true)
                    {
                        var topWin = WindowControl.FromZTop(app);

                        // メインウィンドウなら処理終了
                        if (topWin == null || topWin.Handle == mainWinHandle)
                        {
                            break;
                        }

                        // ネイティブボタンがあればクリック
                        var natives = topWin.GetFromWindowClass(@"Button");
                        if (natives.Length > 0)
                        {
                            new NativeButton(natives[0]).EmulateClick();
                        }
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// SaveFile メソッドで保存されたファイルを削除する。
        /// </summary>
        /// <param name="savedFilePath">SaveFile メソッドの戻り値。</param>
        protected static void DeleteSavedFiles(string savedFilePath)
        {
            if (File.Exists(savedFilePath))
            {
                File.Delete(savedFilePath);
            }

            var txtPath = Path.ChangeExtension(savedFilePath, @".txt");
            if (File.Exists(txtPath))
            {
                File.Delete(txtPath);
            }
        }

        /// <summary>
        /// Talker インスタンス種別を取得する。
        /// </summary>
        protected TalkerKind TalkerKind { get; }

        /// <summary>
        /// テスト用の Talker インスタンスを取得する。
        /// </summary>
        /// <returns>Talker インスタンス。</returns>
        protected TTalker GetTalker()
        {
            var talker = this.GetTalkerImpl();

            if (talker == null)
            {
                Assert.Inconclusive(@"Talker インスタンスを取得できません。");
            }

            if (talker is IProcessTalker pt)
            {
                pt.Update();
            }
            if (!talker.IsAlive)
            {
                Assert.Inconclusive(@"Talker インスタンスが動作していません。");
            }

            return talker;
        }

        /// <summary>
        /// パラメータ情報ディクショナリを取得する。
        /// </summary>
        /// <returns>パラメータ情報ディクショナリ。</returns>
        protected Dictionary<object, IParameterInfo> GetParameterInfos()
        {
            var infos = GetParameterInfosImpl();

            if (infos == null)
            {
                Assert.Inconclusive(@"パラメータ情報一覧を取得できません。");
            }

            return infos.ToDictionary(i => i.Id);
        }

        #region 要オーバライド

        /// <summary>
        /// テスト用の Talker インスタンスを取得する。
        /// </summary>
        /// <returns>Talker インスタンス。</returns>
        protected abstract TTalker GetTalkerImpl();

        /// <summary>
        /// パラメータ情報一覧を取得する。
        /// </summary>
        /// <returns>パラメータ情報一覧の列挙。</returns>
        protected abstract IEnumerable<IParameterInfo> GetParameterInfosImpl();

        /// <summary>
        /// GetAvailableCharacters メソッドの戻り値に対する追加の検証を行う。
        /// </summary>
        /// <param name="characters">有効キャラクター配列。</param>
        /// <remarks>
        /// 既定では何も行わない。
        /// </remarks>
        protected virtual void ValidateAvailableCharacters(
            ReadOnlyCollection<string> characters)
        {
            // 何もしない
        }

        /// <summary>
        /// 音声ファイル保存処理テスト時に作成希望する音声ファイルパスを作成する。
        /// </summary>
        /// <returns>音声ファイルパス。</returns>
        /// <remarks>
        /// 既定では、ユーザテンポラリディレクトリ内のランダムな名前のファイルを返す。
        /// </remarks>
        protected virtual string MakeSaveFilePath()
        {
            string wavPath = null;

            var basePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            for (int i = 0; ; ++i)
            {
                wavPath = basePath + i + @".wav";
                var txtPath = basePath + i + @".txt";

                if (!File.Exists(wavPath) && !File.Exists(txtPath))
                {
                    break;
                }
            }

            return wavPath;
        }

        #endregion
    }
}
