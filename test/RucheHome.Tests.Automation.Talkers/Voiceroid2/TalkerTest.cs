﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RucheHome.Automation.Talkers;
using RucheHome.Automation.Talkers.Voiceroid2;

namespace RucheHome.Tests.Automation.Talkers.Voiceroid2
{
    /// <summary>
    /// <see cref="Talker"/> クラスのテストクラス。
    /// </summary>
    [TestClass]
    public class TalkerTest : ProcessTalkerTestBase<Talker>
    {
        /// <summary>
        /// コンストラクタ。
        /// </summary>
        public TalkerTest() : base(TalkerKind.Voiceroid2)
        {
        }

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
            TestTalker = new Talker();

            // 一度 Update を走らせる
            TestTalker.Update();

            // 操作可能状態でなければ不可
            if (!TestTalker.CanOperate)
            {
                Assert.Inconclusive(
                    TestTalker.IsAlive ?
                        @"VOICEROID2をアイドル状態にしてください。" :
                        @"VOICEROID2を起動してください。");
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
