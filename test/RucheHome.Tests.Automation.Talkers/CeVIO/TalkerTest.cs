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
        /// コンストラクタ。
        /// </summary>
        public TalkerTest() : base(TalkerKind.CeVIO)
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
            TestTalker = new Talker { CastSpeechInputRow = CastSpeechInputRow.Current };

            // 一度 Update を走らせる
            TestTalker.Update();

            // 操作可能状態でなければ不可
            if (!TestTalker.CanOperate)
            {
                Assert.Inconclusive(
                    TestTalker.IsAlive ?
                        @"CeVIO Creative Studio S をアイドル状態にしてください。" :
                        @"CeVIO Creative Studio S を起動してください。");
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
            var orgSetting = talker.IsTextSeparatingByLineBreaks;

            try
            {
                var text = "テキスト設定テストです。\nテスト\tテスト\tテスト。";

                // 改行削除設定
                talker.IsTextSeparatingByLineBreaks = false;

                // テキスト設定
                {
                    var r = talker.SetText(text);
                    Assert.IsTrue(r.Value, r.Message);
                }

                // テキスト取得
                {
                    var r = talker.GetText();
                    Assert.IsNotNull(r.Value, r.Message);
                    Assert.AreEqual(r.Value, TextFormatter.Format(text, false));
                }

                // 改行⇒半角スペース置換設定
                talker.IsTextSeparatingByLineBreaks = true;

                // テキスト設定
                {
                    var r = talker.SetText(text);
                    Assert.IsTrue(r.Value, r.Message);
                }

                // テキスト取得
                {
                    var r = talker.GetText();
                    Assert.IsNotNull(r.Value, r.Message);
                    Assert.AreEqual(r.Value, TextFormatter.Format(text, true));
                }
            }
            finally
            {
                // 設定を元に戻しておく
                talker.IsTextSeparatingByLineBreaks = orgSetting;
            }
        }

        [TestMethod]
        [TestCategory(nameof(CeVIO) + @"." + nameof(Talker))]
        public void Test_CeVIO_Talker_SetEmotionParameters()
        {
            var talker = this.GetTalker();
            var infos = this.GetParameterInfos();

            Dictionary<ParameterId, decimal> orgValues = null;

            // パラメータ群取得
            {
                var r = talker.GetParameters();
                PrintParameters(r.Value);
                Assert.IsNotNull(r.Value, r.Message);
                CollectionAssert.IsSubsetOf(r.Value.Keys, infos.Keys);

                // 感情関連のみ抜き出す
                orgValues =
                    r.Value
                        .Where(kv => kv.Key.IsEmotion())
                        .ToDictionary(kv => kv.Key, kv => kv.Value);
            }

            // パラメータIDと最小許容値との Dictionary を作成
            var emotionIdMins =
                orgValues.Keys.ToDictionary(id => id, id => id.GetInfo().MinValue);
            Assert.IsTrue(emotionIdMins.Count > 0);

            for (int pi = 0; pi < emotionIdMins.Count; ++pi)
            {
                // どれか1要素だけ 最小値+1 にした KeyValuePair リスト作成
                var parameters = emotionIdMins.ToList();
                var param = parameters[pi];
                parameters[pi] =
                    new KeyValuePair<ParameterId, decimal>(param.Key, param.Value + 1);

                // パラメータ群設定
                {
                    var r = talker.SetParameters(parameters);
                    Assert.IsNotNull(r.Value, r.Message);
                    CollectionAssert.IsSubsetOf(r.Value.Keys, infos.Keys);
                    foreach (var kv in r.Value)
                    {
                        Assert.IsTrue(kv.Value.Value, $@"{kv.Key} : {kv.Value.Message}");
                    }
                }

                // パラメータ群取得
                {
                    var r = talker.GetParameters();
                    PrintParameters(r.Value);
                    Assert.IsNotNull(r.Value, r.Message);
                    CollectionAssert.IsSubsetOf(r.Value.Keys, infos.Keys);

                    // 感情関連だけ取り出す
                    var dests = r.Value.Where(kv => kv.Key.IsEmotion());

                    // 最小値+1 にした項目以外が最小値になっていればOK
                    foreach (var dest in dests)
                    {
                        var id = dest.Key;
                        var minValue = id.GetInfo().MinValue;

                        var srcValue = parameters.First(kv => kv.Key == id).Value;
                        if (srcValue == minValue)
                        {
                            // 最小値にした項目
                            Assert.AreEqual(dest.Value, minValue);
                        }
                        else
                        {
                            // 最小値+1 にした項目
                            Assert.AreNotEqual(dest.Value, minValue);
                        }
                    }
                }
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
