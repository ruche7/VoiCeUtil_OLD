using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RucheHome.Talker;

namespace RucheHome.Tests.Talker
{
    /// <summary>
    /// <see cref="IProcessTalker"/> 実装クラスに共通するテストを提供する抽象クラス。
    /// </summary>
    public abstract class ProcessTalkerTestBase<TTalker> : TalkerTestBase<TTalker>
        where TTalker : IProcessTalker
    {
        #region テストメソッド群

        [TestMethod]
        [TestCategory(nameof(IProcessTalker))]
        public void Test_IProcessTalker_ProcessFileName()
        {
            var talker = this.GetTalker();

            var processFileName = talker.ProcessFileName;
            Console.WriteLine(processFileName);
            Assert.IsNotNull(processFileName);
            Assert.AreNotEqual(processFileName, @"");
        }

        [TestMethod]
        [TestCategory(nameof(IProcessTalker))]
        public void Test_IProcessTalker_Update()
        {
            var talker = this.GetTalker();

            talker.Update();
            Assert.IsTrue(talker.IsAlive);
        }

        [TestMethod]
        [TestCategory(nameof(IProcessTalker))]
        public void Test_IProcessTalker_GetProcessFilePath()
        {
            var talker = this.GetTalker();
            var processFilePath = this.GetTalkerProcessFilePathCache();

            var r = talker.GetProcessFilePath();
            Console.WriteLine(r.Value);
            Assert.IsNotNull(r.Value, r.Message);
            Assert.IsTrue(File.Exists(r.Value));
            Assert.AreEqual(r.Value, processFilePath);
        }

        [TestMethod]
        [TestCategory(nameof(IProcessTalker))]
        public void Test_IProcessTalker_ExitProcess_RunProcess()
        {
            var talker = this.GetTalker();
            var processFilePath = this.GetTalkerProcessFilePathCache();

            // 終了
            {
                var r = talker.ExitProcess();
                Assert.IsTrue(r.Value != false, r.Message);

                if (r.Value == null)
                {
                    Console.WriteLine(r.Message);

                    // 先頭ボタンをクリックして終わる
                    ClickAllModalsFirstButtonIfProcessTalker(talker);
                    return;
                }
            }

            // 起動
            {
                var r = talker.RunProcess(processFilePath);
                Assert.IsTrue(r.Value, r.Message);

                // スタートアップ完了まで待つ
                while (talker.State == TalkerState.Startup)
                {
                    talker.Update();
                }
                Assert.IsTrue(talker.IsAlive);
            }
        }

        #endregion

        /// <summary>
        /// テスト用の Talker インスタンスが操作するプロセスの実行ファイルパスキャッシュを
        /// 取得する。
        /// </summary>
        /// <returns>実行ファイルパスキャッシュ。</returns>
        protected string GetTalkerProcessFilePathCache()
        {
            var filePath = this.GetTalkerProcessFilePathCacheImpl();

            if (filePath == null)
            {
                Assert.Inconclusive(@"操作対象プロセスの実行ファイルパスを取得できません。");
            }
            if (!File.Exists(filePath))
            {
                Assert.Inconclusive(@"操作対象プロセスの実行ファイルが存在しません。");
            }

            return filePath;
        }

        #region 要オーバライド

        /// <summary>
        /// テスト用の Talker インスタンスが操作するプロセスの実行ファイルパスキャッシュを
        /// 取得する。
        /// </summary>
        /// <returns>実行ファイルパスキャッシュ。</returns>
        /// <remarks>
        /// テスト処理によって操作対象プロセスが終了しても有効な値を返し続けること。
        /// </remarks>
        protected abstract string GetTalkerProcessFilePathCacheImpl();

        #endregion
    }
}
