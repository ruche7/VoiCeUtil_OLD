using System;
using Codeer.Friendly;
using RucheHome.Diagnostics;

namespace RucheHome.Automation.Friendly.Wpf
{
    /// <summary>
    /// クリック可能なWPFのコントロールに対してクリック操作を提供する静的クラス。
    /// </summary>
    public static class WpfClicker
    {
        /// <summary>
        /// クリック操作を行う。
        /// </summary>
        /// <param name="control">クリック可能なコントロール。</param>
        /// <param name="async">非同期オブジェクト。 null ならば同期処理。</param>
        public static void Click(dynamic control, Async async = null)
        {
            ArgumentValidation.IsNotNull(control, nameof(control));

            control.Focus();

            if (async == null)
            {
                control.OnClick();
            }
            else
            {
                control.OnClick(async);
            }
        }
    }
}
