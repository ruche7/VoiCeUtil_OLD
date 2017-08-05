using System;
using System.Linq;
using Codeer.Friendly;
using Codeer.Friendly.Windows.Grasp;
using Codeer.Friendly.Windows.NativeStandardControls;
using RucheHome.Diagnostics;

namespace RucheHome.Automation.Friendly.Native
{
    /// <summary>
    /// Win32のファイルダイアログをラップするクラス。
    /// </summary>
    public class NativeFileDialog
    {
        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="fileDialog">ラップ対象のファイルダイアログオブジェクト。</param>
        public NativeFileDialog(WindowControl fileDialog)
        {
            this.Base = fileDialog ?? throw new ArgumentNullException(nameof(fileDialog));

            this.FileNameComboBox = GetFileNameComboBox(this.Base);
            this.DecideButton = GetDecideButton(this.Base);
        }

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="fileDialog">ラップ対象のファイルダイアログオブジェクト。</param>
        public NativeFileDialog(AppVar fileDialog)
            : this((fileDialog == null) ? null : new WindowControl(fileDialog))
        {
        }

        /// <summary>
        /// 操作対象アプリを取得する。
        /// </summary>
        public AppFriend App => this.Base.App;

        /// <summary>
        /// ラップ対象のファイルダイアログオブジェクトを取得する。
        /// </summary>
        public WindowControl Base { get; }

        /// <summary>
        /// ファイル名コンボボックスのテキストを取得または設定する。
        /// </summary>
        public string FileName
        {
            get => this.FileNameComboBox.Text;
            set => this.SetFileName(value ?? throw new ArgumentNullException(nameof(value)));
        }

        /// <summary>
        /// ファイル名コンボボックスのテキストを設定する。
        /// </summary>
        /// <param name="fileName">設定するテキスト。</param>
        /// <param name="async">非同期オブジェクト。 null ならば同期処理。</param>
        public void SetFileName(string fileName, Async async = null)
        {
            ArgumentValidation.IsNotNull(fileName, nameof(fileName));

            if (async == null)
            {
                this.FileNameComboBox.EmulateChangeEditText(fileName);
            }
            else
            {
                this.FileNameComboBox.EmulateChangeEditText(fileName, async);
            }
        }

        /// <summary>
        /// 決定ボタンをクリックする。
        /// </summary>
        /// <param name="async">非同期オブジェクト。 null ならば同期処理。</param>
        public void ClickDecideButton(Async async = null)
        {
            if (async == null)
            {
                this.DecideButton.EmulateClick();
            }
            else
            {
                this.DecideButton.EmulateClick(async);
            }
        }

        /// <summary>
        /// ファイル名コンボボックスを取得する。
        /// </summary>
        private NativeComboBox FileNameComboBox { get; }

        /// <summary>
        /// 決定ボタンを取得する。
        /// </summary>
        private NativeButton DecideButton { get; }

        /// <summary>
        /// ファイルダイアログからファイル名コンボボックスを取得する。
        /// </summary>
        /// <param name="dialog">ファイルダイアログ。</param>
        /// <returns>ファイル名コンボボックス。</returns>
        /// <remarks>
        /// 直下に Edit を持つ ComboBox を探す。
        /// </remarks>
        private static NativeComboBox GetFileNameComboBox(WindowControl dialog) =>
            new NativeComboBox(
                dialog
                    .IdentifyFromZIndex(11, 0)
                    .GetFromWindowClass(@"ComboBox")
                    .Where(c => c.GetFromWindowClass(@"Edit").Length == 1)
                    .FirstOrDefault());

        /// <summary>
        /// ファイルダイアログから決定ボタンを取得する。
        /// </summary>
        /// <param name="dialog">ファイルダイアログ。</param>
        /// <returns>決定ボタン。</returns>
        /// <remarks>
        /// ダイアログアイテムIDが 1 のコントロールを探す。
        /// </remarks>
        private static NativeButton GetDecideButton(WindowControl dialog) =>
            new NativeButton(dialog.IdentifyFromDialogId(1));
    }
}
