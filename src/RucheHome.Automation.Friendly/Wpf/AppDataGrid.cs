using System;
using Codeer.Friendly;
using Codeer.Friendly.Dynamic;
using RucheHome.Diagnostics;

namespace RucheHome.Automation.Friendly.Wpf
{
    /// <summary>
    /// 操作対象WPFアプリの DataGrid コントロールをラップするクラス。
    /// </summary>
    public class AppDataGrid
    {
        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="dataGrid">ラップ対象の DataGrid オブジェクト。</param>
        /// <param name="appVisualTree">ビジュアルツリー走査用オブジェクト。</param>
        public AppDataGrid(dynamic dataGrid, AppVisualTree appVisualTree)
        {
            ArgumentValidation.IsNotNull(dataGrid, nameof(dataGrid));
            ArgumentValidation.IsNotNull(appVisualTree, nameof(appVisualTree));

            this.Control = dataGrid;
            this.AppVisualTree = appVisualTree;
        }

        /// <summary>
        /// 操作対象アプリを取得する。
        /// </summary>
        public AppFriend App => this.AppVisualTree.App;

        /// <summary>
        /// ラップ対象の DataGrid オブジェクトを取得する。
        /// </summary>
        public dynamic Control { get; }

        /// <summary>
        /// 選択行のインデックスを取得または設定する。
        /// </summary>
        /// <remarks>
        /// 複数行選択中の場合は先頭行のインデックスを返す。
        /// </remarks>
        public int SelectedRow
        {
            get => (int)this.Control.SelectedIndex;
            set => this.Control.SelectedIndex = value;
        }

        /// <summary>
        /// 行数を取得する。
        /// </summary>
        public int RowCount => (int)this.Control.Items.Count;

        /// <summary>
        /// 列数を取得する。非表示の列も含む。
        /// </summary>
        public int ColumnCount => (int)this.Control.Columns.Count;

        /// <summary>
        /// DataGrid オブジェクトにフォーカスを設定する。
        /// </summary>
        public void Focus() => this.Control.Focus();

        /// <summary>
        /// DataGridRow オブジェクトを取得する。
        /// </summary>
        /// <param name="row">行インデックス。</param>
        /// <returns>DataGridRow オブジェクト。</returns>
        public dynamic GetRow(int row) => this.GetRowsParent().Children[row];

        /// <summary>
        /// DataGridColumn オブジェクトを取得する。
        /// </summary>
        /// <param name="column">列インデックス。非表示の列も含む。</param>
        /// <returns>DataGridColumn オブジェクト。</returns>
        public dynamic GetColumn(int column) => this.Control.Columns[column];

        /// <summary>
        /// DataGridCell オブジェクトを取得する。
        /// </summary>
        /// <param name="row">行インデックス。</param>
        /// <param name="column">列インデックス。非表示の列は含まない。</param>
        /// <returns>DataGridCell オブジェクト。</returns>
        public dynamic GetCell(int row, int column) =>
            this.GetCellFrom(this.GetRow(row), column);

        /// <summary>
        /// DataGridRow オブジェクトから DataGridCell オブジェクトを取得する。
        /// </summary>
        /// <param name="dataGridRow">DataGridRow オブジェクト。</param>
        /// <param name="column">列インデックス。非表示の列は含まない。</param>
        /// <returns>DataGridCell オブジェクト。</returns>
        public dynamic GetCellFrom(dynamic dataGridRow, int column) =>
            this.GetCellsParent(dataGridRow).Children[column];

        /// <summary>
        /// セルのコンテンツを取得する。
        /// </summary>
        /// <param name="row">行インデックス。</param>
        /// <param name="column">列インデックス。非表示の列は含まない。</param>
        /// <returns>セルのコンテンツ。</returns>
        public dynamic GetCellContent(int row, int column) =>
            this.GetCellContentFrom(this.GetCell(row, column));

        /// <summary>
        /// DataGridCell オブジェクトからセルのコンテンツを取得する。
        /// </summary>
        /// <param name="dataGridCell">DataGridCell オブジェクト。</param>
        /// <returns>セルのコンテンツ。</returns>
        public dynamic GetCellContentFrom(dynamic dataGridCell) =>
            (dataGridCell == null) ?
                throw new ArgumentNullException(nameof(dataGridCell)) : dataGridCell.Content;

        /// <summary>
        /// セルのテキストを取得する。
        /// </summary>
        /// <param name="row">行インデックス。</param>
        /// <param name="column">列インデックス。非表示の列は含まない。</param>
        /// <returns>セルのテキスト。</returns>
        public string GetCellText(int row, int column) =>
            this.GetCellTextFrom(this.GetCell(row, column));

        /// <summary>
        /// DataGridCell オブジェクトからセルのテキストを取得する。
        /// </summary>
        /// <param name="dataGridCell">DataGridCell オブジェクト。</param>
        /// <returns>セルのテキスト。</returns>
        public string GetCellTextFrom(dynamic dataGridCell) =>
            (string)this.GetCellContentFrom(dataGridCell).Text;

        /// <summary>
        /// セルを選択する。
        /// </summary>
        /// <param name="row">行インデックス。</param>
        /// <param name="column">列インデックス。非表示の列は含まない。</param>
        public void SelectCell(int row, int column) =>
            this.SelectCellFrom(this.GetCell(row, column));

        /// <summary>
        /// DataGridCellInfo 型のフルネーム。
        /// </summary>
        private const string DataGridCellInfoTypeFullName =
            @"System.Windows.Controls.DataGridCellInfo";

        /// <summary>
        /// DataGridCellInfo コンストラクタの OperationTypeInfo 。
        /// </summary>
        private static readonly OperationTypeInfo DataGridCellInfoConstructorInfo =
            new OperationTypeInfo(
                DataGridCellInfoTypeFullName,
                @"System.Windows.Controls.DataGridCell");

        /// <summary>
        /// DataGridCell オブジェクトのセルを選択する。
        /// </summary>
        /// <param name="dataGridCell">DataGridCell オブジェクト。</param>
        public void SelectCellFrom(dynamic dataGridCell)
        {
            ArgumentValidation.IsNotNull(dataGridCell, nameof(dataGridCell));

            this.Control.CurrentCell =
                this.App.Type(DataGridCellInfoTypeFullName)(
                    DataGridCellInfoConstructorInfo,
                    dataGridCell);
        }

        /// <summary>
        /// セルを選択し、そのテキストを編集する。
        /// </summary>
        /// <param name="row">行インデックス。</param>
        /// <param name="column">列インデックス。非表示の列は含まない。</param>
        /// <param name="text">テキスト。</param>
        /// <param name="asyncCommit">
        /// 編集確定の非同期オブジェクト。 null ならば同期処理。
        /// </param>
        /// <returns>
        /// 成功したならば DataGrid.CommitEdit の戻り値。そうでなければ false 。
        /// </returns>
        /// <remarks>
        /// 非同期処理の場合、処理が完了するまで戻り値には有効な値が設定されない。
        /// </remarks>
        public dynamic EditCellText(
            int row,
            int column,
            string text,
            Async asyncCommit = null)
            =>
            this.EditCellTextFrom(this.GetCell(row, column), text, asyncCommit);

        /// <summary>
        /// DataGridCell オブジェクトのセルを選択し、そのテキストを編集する。
        /// </summary>
        /// <param name="dataGridCell">DataGridCell オブジェクト。</param>
        /// <param name="text">テキスト。</param>
        /// <param name="asyncCommit">
        /// 編集確定の非同期オブジェクト。 null ならば同期処理。
        /// </param>
        /// <returns>
        /// 成功したならば DataGrid.CommitEdit の戻り値。そうでなければ false 。
        /// </returns>
        /// <remarks>
        /// 非同期処理の場合、処理が完了するまで戻り値には有効な値が設定されない。
        /// </remarks>
        public dynamic EditCellTextFrom(
            dynamic dataGridCell,
            string text,
            Async asyncCommit = null)
        {
            ArgumentValidation.IsNotNull(dataGridCell, nameof(dataGridCell));
            ArgumentValidation.IsNotNull(text, nameof(text));

            var dataGrid = this.Control;
            dataGrid.Focus();

            this.SelectCellFrom(dataGridCell);

            if (!(bool)dataGrid.BeginEdit())
            {
                return false;
            }

            this.GetCellContentFrom(dataGridCell).Text = text;

            return
                (asyncCommit == null) ?
                    dataGrid.CommitEdit() : dataGrid.CommitEdit(asyncCommit);
        }

        /// <summary>
        /// ビジュアルツリー走査用オブジェクトを取得する。
        /// </summary>
        private AppVisualTree AppVisualTree { get; }

        /// <summary>
        /// DataGridRow 群の親コントロールを取得する。
        /// </summary>
        /// <returns>DataGridRow 群の親コントロール。</returns>
        private dynamic GetRowsParent()
        {
            var vtree = this.AppVisualTree;

            var border = vtree.GetDescendant(this.Control, 0);
            var presenter = border.Child.Content;
            var parent = vtree.GetDescendant(presenter, 0);

            return parent;
        }

        /// <summary>
        /// DataGridCell 群の親コントロールを取得する。
        /// </summary>
        /// <param name="dataGridRow">DataGridRow オブジェクト。</param>
        /// <returns>DataGridCell 群の親コントロール。</returns>
        private dynamic GetCellsParent(dynamic dataGridRow)
        {
            ArgumentValidation.IsNotNull(dataGridRow, nameof(dataGridRow));

            var vtree = this.AppVisualTree;

            var grid = vtree.GetDescendant(dataGridRow, 0);
            var cellsPresenter = grid.Children[0].Child.Children[0];
            var parent = vtree.GetDescendant(cellsPresenter, 0, 0);

            return parent;
        }
    }
}
