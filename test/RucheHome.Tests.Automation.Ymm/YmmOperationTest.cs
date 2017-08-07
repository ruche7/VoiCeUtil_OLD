using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RucheHome.Automation.Ymm;

namespace RucheHome.Tests.Automation.Ymm
{
    /// <summary>
    /// <see cref="YmmOperation"/> クラスのテストクラス。
    /// </summary>
    [TestClass]
    public class YmmOperationTest
    {
        /// <summary>
        /// コンストラクタ。
        /// </summary>
        public YmmOperationTest() { }

        #region テストメソッド群

        // TODO: 実装

        #endregion

        /// <summary>
        /// テスト用の YmmOperation インスタンスを取得または設定する。
        /// </summary>
        private static YmmOperation YmmOperation { get; set; } = null;

        /// <summary>
        /// YMMの実行ファイルパスを取得または設定する。
        /// </summary>
        private static string YmmProcessFilePath { get; set; } = null;

        /// <summary>
        /// テスト用WAVEファイルパス。
        /// </summary>
        private static readonly string TestWaveFilePath = @"..\..\..\data\test.wav";

        /// <summary>
        /// テストクラスの初期化処理を行う。
        /// </summary>
        /// <param name="context">テストコンテキスト。</param>
        [ClassInitialize]
        public static void Initialize(TestContext context)
        {
            // YmmOperation 作成
            YmmOperation = new YmmOperation();

            // 一度 Update を走らせる
            YmmOperation.Update();

            // 操作可能状態でなければ不可
            if (!YmmOperation.CanOperate)
            {
                Assert.Inconclusive(
                    YmmOperation.IsAlive ?
                        @"ゆっくりMovieMakerをアイドル状態にしてください。" :
                        @"ゆっくりMovieMakerを起動してください。");
            }

            // タイムラインウィンドウ非表示なら表示試行
            if (!YmmOperation.IsTimelineVisible)
            {
                var r = YmmOperation.SetTimelineVisible(true);
                if (!r.Value)
                {
                    Assert.Inconclusive(r.Message);
                }
            }

            // 実行ファイルパスを取得する
            var (filePath, filePathMessage) = YmmOperation.GetProcessFilePath();
            if (filePath == null)
            {
                Assert.Inconclusive(@"実行ファイルパスを取得できません。" + filePathMessage);
            }
            YmmProcessFilePath = filePath;

            // 念のためテスト用WAVEファイルの存在チェック
            if (!File.Exists(TestWaveFilePath))
            {
                Assert.Inconclusive(
                    @"テスト用WAVEファイル " +
                    Path.GetFullPath(TestWaveFilePath) +
                    @" が配置されていません。");
            }
        }

        /// <summary>
        /// テストクラスのクリーンアップを行う。
        /// </summary>
        [ClassCleanup]
        public static void Cleanup()
        {
            // YmmOperation を破棄
            YmmOperation?.Dispose();
        }
    }
}
