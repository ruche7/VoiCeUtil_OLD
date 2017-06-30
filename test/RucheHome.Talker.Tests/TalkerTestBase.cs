using System;
using System.Collections.ObjectModel;
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

            Assert.IsNotNull(talker.TalkerName);
        }

        [TestMethod]
        [TestCategory(nameof(ITalker))]
        public void Test_ITalker_State()
        {
            var talker = this.GetTalker();

            Assert.AreEqual(talker.State, TalkerState.Idle);
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

            Assert.IsNull(talker.FailStateMessage, talker.FailStateMessage);
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
        public void Test_ITalker_GetAvailableCharacters_SetText_Speak_Stop()
        {
            var talker = this.GetTalker();

            // 読み切らないよう長めのテキストにする
            var text = "テキスト読み上げテストです。\nテストテストテスト。\n";
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

        #region 要オーバライド

        /// <summary>
        /// テスト用の Talker インスタンスを取得する。
        /// </summary>
        /// <returns>Talker インスタンス。</returns>
        protected abstract TTalker GetTalkerImpl();

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
