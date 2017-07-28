using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RucheHome.Automation.Talkers;
using RucheHome.Automation.Talkers.CeVIO;

namespace RucheHome.Tests.Automation.Talkers.CeVIO
{
    /// <summary>
    /// <see cref="Talker"/> クラスのテストクラス。
    /// </summary>
    [TestClass]
    public class TalkerTest : ProcessTalkerTestBase<Talker>
    {
        /// <summary>
        /// テスト用の Talker インスタンスを取得または設定する。
        /// </summary>
        private static Talker TestTalker { get; set; } = null;

        /// <summary>
        /// <see cref="TestTalker"/> の実行ファイルパスキャッシュを取得または設定する。
        /// </summary>
        private static string TestTalkerProcessFilePathCache { get; set; } = null;

        /// <summary>
        /// テストクラスの初期化処理を行う。
        /// </summary>
        /// <param name="context">テストコンテキスト。</param>
        [ClassInitialize]
        public static void Initialize(TestContext context)
        {
            // Talker 作成
            TestTalker = new Talker { CastSpeechInputRow = CastSpeechInputRow.Current };

            // 一度 Update を走らせる
            TestTalker.Update();

            // 起動中でなければ不可
            if (!TestTalker.IsAlive)
            {
                Assert.Inconclusive(@"CeVIO Creative Studio S を起動してください。");
            }

            // 実行ファイルパスを取得する
            var r = TestTalker.GetProcessFilePath();
            if (r.Value == null)
            {
                Assert.Inconclusive(@"実行ファイルパスを取得できません。" + r.Message);
            }
            TestTalkerProcessFilePathCache = r.Value;
        }

        /// <summary>
        /// テストクラスのクリーンアップを行う。
        /// </summary>
        [ClassCleanup]
        public static void Cleanup()
        {
            // Talker を破棄
            TestTalker?.Dispose();
        }

        [TestMethod]
        [TestCategory(nameof(CeVIO) + @"." + nameof(Talker))]
        public override void Test_ITalker_SetText_GetText()
        {
            var talker = this.GetTalker();
            var orgSetting = talker.IsTextSeparatedByLineBreaks;

            try
            {
                var text = "テキスト設定テストです。\nテストテストテスト。";

                // 改行削除設定
                talker.IsTextSeparatedByLineBreaks = false;

                // テキスト設定
                {
                    var r = talker.SetText(text);
                    Assert.IsTrue(r.Value, r.Message);
                }

                // テキスト取得
                {
                    var r = talker.GetText();
                    Assert.IsNotNull(r.Value, r.Message);
                    Assert.AreEqual(r.Value, text.Replace("\n", @""));
                }

                // 改行⇒半角スペース置換設定
                talker.IsTextSeparatedByLineBreaks = true;

                // テキスト設定
                {
                    var r = talker.SetText(text);
                    Assert.IsTrue(r.Value, r.Message);
                }

                // テキスト取得
                {
                    var r = talker.GetText();
                    Assert.IsNotNull(r.Value, r.Message);
                    Assert.AreEqual(r.Value, text.Replace("\n", @" "));
                }
            }
            finally
            {
                // 設定を元に戻しておく
                talker.IsTextSeparatedByLineBreaks = orgSetting;
            }
        }

        #region オーバライド

        /// <summary>
        /// テスト用の Talker インスタンスを取得する。
        /// </summary>
        /// <returns>Talker インスタンス。</returns>
        protected override Talker GetTalkerImpl() => TestTalker;

        /// <summary>
        /// パラメータ情報一覧を取得する。
        /// </summary>
        /// <returns>パラメータ情報一覧の列挙。</returns>
        protected override IEnumerable<IParameterInfo> GetParameterInfosImpl() =>
            ((ParameterId[])Enum.GetValues(typeof(ParameterId))).Select(id => id.GetInfo());

        /// <summary>
        /// テスト用の Talker インスタンスが操作するプロセスの実行ファイルパスキャッシュを
        /// 取得する。
        /// </summary>
        /// <returns>実行ファイルパスキャッシュ。</returns>
        /// <remarks>
        /// テスト処理によって操作対象プロセスが終了しても有効な値を返し続けること。
        /// </remarks>
        protected override string GetTalkerProcessFilePathCacheImpl() =>
            TestTalkerProcessFilePathCache;

        #endregion
    }
}
