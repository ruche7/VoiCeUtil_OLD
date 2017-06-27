using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace RucheHome.Talker
{
    /// <summary>
    /// ITalker インタフェースに状態更新処理を追加提供するインタフェース。
    /// </summary>
    public interface IUpdatableTalker : ITalker
    {
        /// <summary>
        /// 操作対象プロセスの実行ファイル名(拡張子なし)を取得する。
        /// </summary>
        /// <remarks>
        /// <see cref="Process.GetProcessesByName(string)">Process.GetProcessesByName</see>
        /// メソッドの引数として利用できる。
        /// </remarks>
        string ProcessFileName { get; }

        /// <summary>
        /// 状態を更新する。
        /// </summary>
        /// <param name="processes">
        /// 対象プロセス検索先列挙。メソッド内でプロセスリストを取得させるならば null 。
        /// </param>
        void Update(IEnumerable<Process> processes = null);
    }
}
