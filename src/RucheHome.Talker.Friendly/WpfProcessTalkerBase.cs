using System;
using System.Collections.Generic;
using System.Windows;
using Codeer.Friendly;
using Codeer.Friendly.Dynamic;
using Codeer.Friendly.Windows;

namespace RucheHome.Talker.Friendly
{
    /// <summary>
    /// WPFプロセス対象の、 Codeer.Friendly ライブラリを用いた
    /// <see cref="IProcessTalker"/> インタフェースの抽象実装クラス。
    /// </summary>
    /// <typeparam name="TParameterId">パラメータID型。</typeparam>
    /// <remarks>
    /// <see cref="IProcessTalker"/> インタフェースの各メソッドは互いに排他制御されている。
    /// そのため、派生クラスで抽象メソッドを実装する際、それらのメソッドを呼び出さないこと。
    /// </remarks>
    public abstract class WpfProcessTalkerBase<TParameterId>
        : ProcessTalkerBase<TParameterId>
    {
        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="processClrVersion">操作対象プロセスのCLRバージョン種別。</param>
        /// <param name="processFileName">
        /// 操作対象プロセスの実行ファイル名(拡張子なし)。
        /// </param>
        /// <param name="processProduct">操作対象プロセスの製品名情報。</param>
        /// <param name="talkerName">名前。 null ならば processProduct を使う。</param>
        /// <param name="canSaveBlankText">空白文を音声ファイル保存可能ならば true 。</param>
        /// <param name="hasCharacters">キャラクター設定を保持しているならば true 。</param>
        protected WpfProcessTalkerBase(
            ClrVersion processClrVersion,
            string processFileName,
            string processProduct,
            string talkerName = null,
            bool canSaveBlankText = false,
            bool hasCharacters = false)
            :
            base(
                processClrVersion,
                processFileName,
                processProduct,
                talkerName,
                canSaveBlankText,
                hasCharacters)
        {
        }

        #region ProcessTalkerBase<TParameterId> のオーバライド

        /// <summary>
        /// 現在表示されているウィンドウを列挙する。
        /// </summary>
        /// <param name="app">
        /// 操作対象アプリ。
        /// <see cref="ProcessTalkerBase{TParameterId}"/> 実装から
        /// null や操作対象外アプリが渡されることはない。
        /// </param>
        /// <returns>ウィンドウを表す <see cref="AppVar"/> 列挙。</returns>
        protected override sealed IEnumerable<AppVar> EnumerateWindows(WindowsAppFriend app)
        {
            foreach (var window in app.Type<Application>().Current.Windows)
            {
                if ((Visibility)window.Visibility == Visibility.Visible)
                {
                    yield return window;
                }
            }
        }

        /// <summary>
        /// ウィンドウタイトルを取得する。
        /// </summary>
        /// <param name="window">
        /// ウィンドウを表す <see cref="AppVar"/> 。
        /// <see cref="ProcessTalkerBase{TParameterId}"/> 実装から
        /// <see cref="EnumerateWindows"/> の列挙値以外が渡されることはない。
        /// </param>
        /// <returns>ウィンドウタイトル。取得できなければ null 。</returns>
        protected override sealed string GetWindowTitle(AppVar window) =>
            (string)window.Dynamic().Title;

        #endregion
    }
}
