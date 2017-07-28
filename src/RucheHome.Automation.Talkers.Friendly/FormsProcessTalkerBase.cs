using System;
using System.Windows.Forms;
using Codeer.Friendly;
using Codeer.Friendly.Dynamic;
using RucheHome.Diagnostics;

namespace RucheHome.Automation.Talkers.Friendly
{
    /// <summary>
    /// WinFormsプロセス対象の、 Codeer.Friendly ライブラリを用いた
    /// <see cref="IProcessTalker"/> インタフェースの抽象実装クラス。
    /// </summary>
    /// <typeparam name="TParameterId">パラメータID型。</typeparam>
    /// <remarks>
    /// <see cref="IProcessTalker"/> インタフェースの各メソッドは互いに排他制御されている。
    /// そのため、派生クラスで抽象メソッドを実装する際、それらのメソッドを呼び出さないこと。
    /// </remarks>
    public abstract class FormsProcessTalkerBase<TParameterId>
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
        protected FormsProcessTalkerBase(
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

        /// <summary>
        /// Controls プロパティツリーを辿ることによって子孫コントロールを取得する。
        /// </summary>
        /// <param name="root">ツリーのルートとなるコントロール。</param>
        /// <param name="treeIndices">Controls プロパティツリーインデックス配列。</param>
        /// <returns>コントロール。取得できなかった場合は null 。</returns>
        protected static AppVar GetControlFromControlsTree(
            AppVar root,
            params int[] treeIndices)
        {
            if (root != null && treeIndices != null)
            {
                try
                {
                    var control = root.Dynamic();

                    foreach (var index in treeIndices)
                    {
                        control = control.Controls[index];
                    }

                    return control;
                }
                catch (Exception ex)
                {
                    ThreadDebug.WriteException(ex);
                }
            }

            return null;
        }

        /// <summary>
        /// Controls プロパティツリーを辿ることによって子孫コントロールを取得する。
        /// </summary>
        /// <typeparam name="TControl">
        /// 子孫コントロール型。
        /// <see cref="AppVar"/> インスタンスを代入可能ではない場合、
        /// <see cref="AppVar"/> インスタンスを引数に取るコンストラクタが必要。
        /// </typeparam>
        /// <param name="root">ツリーのルートとなるコントロール。</param>
        /// <param name="treeIndices">Controls プロパティツリーインデックス配列。</param>
        /// <returns>コントロール。取得できなかった場合は null 。</returns>
        protected static TControl GetControlFromControlsTree<TControl>(
            AppVar root,
            params int[] treeIndices)
            where TControl : class
        {
            var v = GetControlFromControlsTree(root, treeIndices);
            if (v != null)
            {
                try
                {
                    return CastOrCreate<TControl>(v);
                }
                catch (Exception ex)
                {
                    ThreadDebug.WriteException(ex);
                }
            }

            return null;
        }

        /// <summary>
        /// Text プロパティ値によって子コントロールを検索する。
        /// </summary>
        /// <param name="parent">親コントロール。</param>
        /// <param name="text">検索する Text プロパティ値。</param>
        /// <returns>コントロール。見つからなければ null 。</returns>
        protected static AppVar FindChildControlByText(AppVar parent, string text)
        {
            if (parent != null && text != null)
            {
                try
                {
                    foreach (var c in parent.Dynamic().Controls)
                    {
                        if ((string)c.Text == text)
                        {
                            return c;
                        }
                    }
                }
                catch (Exception ex)
                {
                    ThreadDebug.WriteException(ex);
                }
            }

            return null;
        }

        /// <summary>
        /// Text プロパティ値によって子コントロールを検索する。
        /// </summary>
        /// <typeparam name="TControl">
        /// 子孫コントロール型。
        /// <see cref="AppVar"/> インスタンスを代入可能ではない場合、
        /// <see cref="AppVar"/> インスタンスを引数に取るコンストラクタが必要。
        /// </typeparam>
        /// <param name="parent">親コントロール。</param>
        /// <param name="text">検索する Text プロパティ値。</param>
        /// <returns>コントロール。見つからなければ null 。</returns>
        protected static TControl FindChildControlByText<TControl>(
            AppVar parent,
            string text)
            where TControl : class
        {
            var v = FindChildControlByText(parent, text);
            if (v != null)
            {
                try
                {
                    return CastOrCreate<TControl>(v);
                }
                catch (Exception ex)
                {
                    ThreadDebug.WriteException(ex);
                }
            }

            return null;
        }

        /// <summary>
        /// タブコントロールから指定した名前のタブページを検索する。
        /// </summary>
        /// <param name="tabControl">タブコントロール。</param>
        /// <param name="name">検索するタブページ名。</param>
        /// <returns>タブページ。見つからなかった場合は null 。</returns>
        /// <remarks>
        /// 検索対象はWinFormsの TabPage のみ。
        /// </remarks>
        protected static AppVar FindTabPage(AppVar tabControl, string name)
        {
            if (tabControl != null && name != null)
            {
                try
                {
                    foreach (var page in tabControl.Dynamic().TabPages)
                    {
                        if ((string)page.Text == name)
                        {
                            return page;
                        }
                    }
                }
                catch (Exception ex)
                {
                    ThreadTrace.WriteException(ex);
                }
            }

            return null;
        }

        /// <summary>
        /// メインウィンドウを検索する。
        /// </summary>
        /// <returns>メインウィンドウ。見つからなかった場合は null 。</returns>
        /// <remarks>
        /// 戻り値が有効である場合、本体側の
        /// <see cref="Form"/> オブジェクトを参照している。
        /// </remarks>
        protected AppVar FindMainWindow()
        {
            var app = this.TargetApp;
            if (app == null)
            {
                return null;
            }

            try
            {
                foreach (var form in app.Type<Application>().OpenForms)
                {
                    if (this.CheckWindowTitleKind((string)form.Text) == WindowTitleKind.Main)
                    {
                        return form;
                    }
                }
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
            }
            return null;
        }
    }
}
