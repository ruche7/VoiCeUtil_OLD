using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RucheHome.Talker.Tests
{
    /// <summary>
    /// <see cref="ITalker"/> 実装クラスに共通するテストを提供する抽象クラス。
    /// </summary>
    public abstract class TalkerTestBase<TTalker>
        where TTalker : ITalker
    {
        #region テストメソッド群

        [TestMethod]
        [TestCategory(nameof(ITalker))]
        public void Test_ITalker_TalkerName()
        {
            var talker = this.GetTalker();

            var talkerName = talker.TalkerName;
            Console.WriteLine(talkerName);
            Assert.IsNotNull(talkerName);
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
        public void Test_ITalker_FailStateMessage()
        {
            var talker = this.GetTalker();

            var failStateMessage = talker.FailStateMessage;
            Assert.IsNull(failStateMessage, failStateMessage);
        }

        [TestMethod]
        [TestCategory(nameof(ITalker))]
        public void Test_ITalker_GetAvailableCharacters_SetCharacters_GetCharacters()
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
                this.CheckAvailableCharacters(r.Value);

                characters = r.Value;
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
        }

        [TestMethod]
        [TestCategory(nameof(ITalker))]
        public void Test_ITalker_GetAvailableCharacters_SetText_GetText()
        {
            var talker = this.GetTalker();

            var text = "テキスト設定テストです。\nテストテストテスト。";

            // テキスト設定
            {
                var r = talker.SetText(text);
                Assert.IsTrue(r.Value, r.Message);
            }

            // テキスト取得
            {
                var r = talker.GetText();
                Assert.IsNotNull(r.Value, r.Message);
                Assert.AreEqual(r.Value, text);
            }
        }

        [TestMethod]
        [TestCategory(nameof(ITalker))]
        public void Test_ITalker_GetParameters_SetParameters()
        {
            // パラメータ群を出力するローカルメソッド
            void writeParameters(IEnumerable<KeyValuePair<object, decimal>> parameters)
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

            var talker = this.GetTalker();
            var infos = this.GetParameterInfos();

            Dictionary<object, decimal> orgValues = null;

            // パラメータ群取得
            {
                var r = talker.GetParameters();
                writeParameters(r.Value);
                Assert.IsNotNull(r.Value, r.Message);
                CollectionAssert.AreEqual(r.Value.Keys, infos.Keys);

                orgValues = r.Value;
            }

            // 最小許容値設定
            {
                var r =
                    talker.SetParameters(
                        infos.ToDictionary(kv => kv.Key, kv => kv.Value.MinValue));
                Assert.IsNotNull(r.Value, r.Message);
                CollectionAssert.AreEqual(r.Value.Keys, infos.Keys);
                foreach (var kv in r.Value)
                {
                    Assert.IsTrue(kv.Value.Value, $@"{kv.Key} : {kv.Value.Message}");
                }
            }

            // パラメータ群取得(最小許容値)
            {
                var r = talker.GetParameters();
                writeParameters(r.Value);
                Assert.IsNotNull(r.Value, r.Message);
                CollectionAssert.AreEqual(r.Value.Keys, infos.Keys);
                CollectionAssert.AreEqual(
                    r.Value.Values,
                    infos.Select(kv => kv.Value.MinValue).ToArray());
            }

            // 最大許容値設定
            {
                var r =
                    talker.SetParameters(
                        infos.ToDictionary(kv => kv.Key, kv => kv.Value.MaxValue));
                Assert.IsNotNull(r.Value, r.Message);
                CollectionAssert.AreEqual(r.Value.Keys, infos.Keys);
                foreach (var kv in r.Value)
                {
                    Assert.IsTrue(kv.Value.Value, $@"{kv.Key} : {kv.Value.Message}");
                }
            }

            // パラメータ群取得(最大許容値)
            {
                var r = talker.GetParameters();
                writeParameters(r.Value);
                Assert.IsNotNull(r.Value, r.Message);
                CollectionAssert.AreEqual(r.Value.Keys, infos.Keys);
                CollectionAssert.AreEqual(
                    r.Value.Values,
                    infos.Select(kv => kv.Value.MaxValue).ToArray());
            }

            // 初期値設定
            {
                var r = talker.SetParameters(orgValues);
                Assert.IsNotNull(r.Value, r.Message);
                CollectionAssert.AreEqual(r.Value.Keys, infos.Keys);
                foreach (var kv in r.Value)
                {
                    Assert.IsTrue(kv.Value.Value, $@"{kv.Key} : {kv.Value.Message}");
                }
            }
        }

        [TestMethod]
        [TestCategory(nameof(ITalker))]
        public void Test_ITalker_GetAvailableCharacters_SetText_Speak_Stop()
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

        #endregion

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
        /// GetAvailableCharacters メソッドの戻り値に対する追加のチェック処理を行う。
        /// </summary>
        /// <param name="characters">有効キャラクター配列。</param>
        /// <remarks>
        /// 既定では何も行わない。
        /// </remarks>
        protected virtual void CheckAvailableCharacters(ReadOnlyCollection<string> characters)
        {
            // 何もしない
        }

        #endregion
    }
}
