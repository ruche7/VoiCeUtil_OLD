﻿using System;
using System.Windows.Forms;
using Codeer.Friendly.Dynamic;
using RucheHome.Automation.Friendly;
using RucheHome.Diagnostics;

namespace RucheHome.Automation.Talkers.Friendly
{
    /// <summary>
    /// WinFormsプロセス対象の、 Codeer.Friendly ライブラリを用いた
    /// <see cref="IProcessTalker{TParameterId}"/> インタフェースの抽象実装クラス。
    /// </summary>
    /// <typeparam name="TParameterId">パラメータID型。</typeparam>
    /// <remarks>
    /// <see cref="IProcessTalker"/> インタフェースの各メソッドは互いに排他制御されている。
    /// そのため、派生クラスで抽象メソッドを実装する際、それらのメソッドを呼び出さないこと。
    /// </remarks>
    public abstract class FormsTalkerBase<TParameterId>
        : TalkerBase<TParameterId>
    {
        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="processClrVersion">操作対象プロセスのCLRバージョン種別。</param>
        protected FormsTalkerBase(ClrVersion processClrVersion) : base(processClrVersion)
        {
        }

        /// <summary>
        /// Controls プロパティツリーを辿ることによって子孫コントロールを取得する。
        /// </summary>
        /// <param name="root">ツリーのルートとなるコントロール。</param>
        /// <param name="treeIndices">Controls プロパティツリーインデックス配列。</param>
        /// <returns>コントロール。取得できなかった場合は null 。</returns>
        protected static dynamic GetControlFromControlsTree(
            dynamic root,
            params int[] treeIndices)
        {
            if (root != null && treeIndices != null)
            {
                try
                {
                    var control = root;

                    foreach (var index in treeIndices)
                    {
                        control = control.Controls[index];
                    }

                    return control;
                }
                catch (Exception ex)
                {
                    ThreadTrace.WriteException(ex);
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
        protected static dynamic FindChildControlByText(dynamic parent, string text)
        {
            if (parent != null && text != null)
            {
                try
                {
                    foreach (var c in parent.Controls)
                    {
                        if ((string)c.Text == text)
                        {
                            return c;
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
        /// タブコントロールから指定した名前のタブページを検索する。
        /// </summary>
        /// <param name="tabControl">タブコントロール。</param>
        /// <param name="name">検索するタブページ名。</param>
        /// <returns>タブページ。見つからなかった場合は null 。</returns>
        /// <remarks>
        /// 検索対象はWinFormsの TabPage のみ。
        /// </remarks>
        protected static dynamic FindTabPage(dynamic tabControl, string name)
        {
            if (tabControl != null && name != null)
            {
                try
                {
                    foreach (var page in tabControl.TabPages)
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
        protected dynamic FindMainWindow()
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
