using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RucheHome.Talker.AITalkEx;
using AI = RucheHome.Talker.AITalkEx;

namespace RucheHome.Talker.Tests.AITalkEx
{
    /// <summary>
    /// <see cref="AI.Talker"/> クラスのテストクラス。
    /// </summary>
    [TestClass]
    public class TalkerTest : ProcessTalkerTestBase<AI.Talker>
    {
        /// <summary>
        /// テスト用の Talker インスタンスを取得または設定する。
        /// </summary>
        private static AI.Talker TestTalker { get; set; } = null;

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
            // 全 Talker 取得
            var talkers =
                ((Product[])Enum.GetValues(typeof(Product))).Select(p => AI.Talker.Get(p));
            Assert.IsTrue(talkers.Any());
            Assert.IsTrue(talkers.All(t => t != null));

            // 一度 Update を走らせる
            foreach (var talker in talkers)
            {
                talker.Update();
            }

            // 起動中の Talker をテスト対象にする
            TestTalker = talkers.FirstOrDefault(t => t.IsAlive);
            if (TestTalker == null)
            {
                Assert.Inconclusive(@"操作対象 AITalkEx アプリを1つ以上起動してください。");
            }

            // 実行ファイルパスを取得する
            var r = TestTalker.GetProcessFilePath();
            if (r.Value == null)
            {
                Assert.Inconclusive(@"実行ファイルパスを取得できません。" + r.Message);
            }
            TestTalkerProcessFilePathCache = r.Value;
        }

        #region オーバライド

        /// <summary>
        /// テスト用の Talker インスタンスを取得する。
        /// </summary>
        /// <returns>Talker インスタンス。</returns>
        protected override AI.Talker GetTalkerImpl() => TestTalker;

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
