using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using RucheHome.Diagnostics;

namespace RucheHome.Automation
{
    /// <summary>
    /// <see cref="IProcessOperation"/>
    /// オブジェクト群の非同期な状態更新処理を提供するクラス。
    /// </summary>
    public class ProcessOperationUpdater : IDisposable
    {
        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <remarks>
        /// 並走させるタスクの最大数は
        /// <see cref="ThreadPool.GetMinThreads">ThreadPool.GetMinThreads</see>
        /// メソッドから取得する。
        /// </remarks>
        public ProcessOperationUpdater()
        {
            // スレッドプールの設定値からタスク数を決定
            ThreadPool.GetMinThreads(out var count, out _);
            this.TaskCountLimit = count;
        }

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="taskCountLimit">
        /// 並走させるタスクの最大数。 1 以上 1024 以下。
        /// </param>
        public ProcessOperationUpdater(int taskCountLimit)
        {
            ArgumentValidation.IsWithinRange(taskCountLimit, 1, 1024, nameof(taskCountLimit));

            this.TaskCountLimit = taskCountLimit;
        }

        /// <summary>
        /// デストラクタ。
        /// </summary>
        ~ProcessOperationUpdater() => this.Dispose(false);

        /// <summary>
        /// 並走させるタスクの最大数を取得する。
        /// </summary>
        public int TaskCountLimit { get; }

        /// <summary>
        /// 状態更新処理間隔ミリ秒数を取得または設定する。
        /// </summary>
        public int OperationUpdateIntervalMilliseconds { get; set; } = 10;

        /// <summary>
        /// プロセスリスト更新間隔ミリ秒数を取得または設定する。
        /// </summary>
        public int ProcessListUpdateIntervalMilliseconds { get; set; } = 100;

        /// <summary>
        /// <see cref="IProcessOperation"/> オブジェクトを登録する。
        /// </summary>
        /// <param name="processOperation">
        /// <see cref="IProcessOperation"/> オブジェクト。
        /// </param>
        /// <returns>登録できたならば true 。既に登録済みならば false 。</returns>
        public bool Add(IProcessOperation processOperation)
        {
            ArgumentValidation.IsNotNull(processOperation, nameof(processOperation));

            lock (this.OperationTaskLock)
            {
                if (this.Operations.Contains(processOperation))
                {
                    return false;
                }

                // 追加
                this.Operations.Add(processOperation);

                // タスク数が少ないなら追加
                while (
                    this.Tasks.Count < this.TaskCountLimit &&
                    this.Tasks.Count < this.Operations.Count)
                {
                    if (!this.PushTask())
                    {
                        break;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// <see cref="IProcessOperation"/> オブジェクトの登録を破棄する。
        /// </summary>
        /// <param name="processOperation">
        /// <see cref="IProcessOperation"/> オブジェクト。
        /// </param>
        /// <returns>破棄できたならば true 。登録されていないならば false 。</returns>
        public bool Remove(IProcessOperation processOperation)
        {
            ArgumentValidation.IsNotNull(processOperation, nameof(processOperation));

            List<Task> removedTasks = null;

            lock (this.OperationTaskLock)
            {
                // 位置検索
                var index = this.Operations.IndexOf(processOperation);
                if (index < 0)
                {
                    return false;
                }

                // 削除
                this.Operations.RemoveAt(index);

                // 次回処理対象インデックスより手前を削除したなら補正
                if (this.OperationIndex > index)
                {
                    --this.OperationIndex;
                }

                // タスク数が多すぎるなら終わらせる
                if (this.Tasks.Count > this.Operations.Count)
                {
                    removedTasks = new List<Task>();
                    while (this.Tasks.Count > this.Operations.Count)
                    {
                        var task = this.PopTask();
                        if (task == null)
                        {
                            break;
                        }
                        removedTasks.Add(task);
                    }
                }
            }
            lock (((ICollection)this.LastExceptionMap).SyncRoot)
            {
                this.LastExceptionMap.Remove(processOperation);
            }

            // lock 内で Wait するとデッドロックするので注意
            if (removedTasks != null && removedTasks.Count > 0)
            {
                Task.WaitAll(removedTasks.ToArray());
            }

            return true;
        }

        /// <summary>
        /// <see cref="IProcessOperation.Update"/>
        /// メソッド呼び出しで送出された直近の例外を取得する。
        /// </summary>
        /// <param name="processOperation">
        /// <see cref="IProcessOperation"/> オブジェクト。
        /// </param>
        /// <returns>例外。登録されていないか例外が送出されていないならば null 。</returns>
        public Exception GetLastException(IProcessOperation processOperation)
        {
            ArgumentValidation.IsNotNull(processOperation, nameof(processOperation));

            lock (((ICollection)this.LastExceptionMap).SyncRoot)
            {
                return
                    this.LastExceptionMap.TryGetValue(processOperation, out var ex) ?
                        ex : null;
            }
        }

        /// <summary>
        /// 登録済み <see cref="IProcessOperation"/> オブジェクトリストを取得する。
        /// </summary>
        private List<IProcessOperation> Operations { get; } = new List<IProcessOperation>();

        /// <summary>
        /// 次回処理対象の <see cref="Operations"/> インデックスを取得または設定する。
        /// </summary>
        private int OperationIndex { get; set; } = 0;

        /// <summary>
        /// タスクスタックを取得する。
        /// </summary>
        private Stack<(Task task, CancellationTokenSource cancelTokenSource)>
        Tasks { get; } =
            new Stack<(Task task, CancellationTokenSource cancelTokenSource)>();

        /// <summary>
        /// <see cref="Operations"/>, <see cref="OperationIndex"/>, <see cref="Tasks"/>
        /// の排他制御用オブジェクト。
        /// </summary>
        private object OperationTaskLock = new object();

        /// <summary>
        /// 実行ファイル名ごとのプロセス配列ディクショナリを取得する。
        /// </summary>
        private ConcurrentDictionary<string, Process[]> ProcessesMap { get; } =
            new ConcurrentDictionary<string, Process[]>();

        /// <summary>
        /// プロセス配列ディクショナリ更新判定用ストップウォッチを取得する。
        /// </summary>
        private Stopwatch ProcessesStopwatch { get; } = Stopwatch.StartNew();

        /// <summary>
        /// プロセス配列ディクショナリの最終更新時間値を取得または設定する。
        /// </summary>
        /// <remarks>
        /// 負数ならば次のタイミングで必ず更新する。
        /// </remarks>
        private long ProcessesMapUpdateTime { get; set; } = -1;

        /// <summary>
        /// 最後に発生した例外のディクショナリを取得する。
        /// </summary>
        private Dictionary<IProcessOperation, Exception> LastExceptionMap { get; } =
            new Dictionary<IProcessOperation, Exception>();

        /// <summary>
        /// タスクを1つ増やす。
        /// </summary>
        /// <returns>
        /// 追加できたならば true 。個数制限で追加できなかったならば false 。
        /// </returns>
        /// <remarks>
        /// 呼び出し元で排他制御を行うこと。
        /// </remarks>
        private bool PushTask()
        {
            if (this.Tasks.Count >= this.TaskCountLimit)
            {
                return false;
            }

            var cancelTokenSrc = new CancellationTokenSource();

            // タスク開始
            // 終了時に cancelTokenSrc.Dispose() を呼ぶ
            var task =
                Task.Run(
                    () => this.ExecuteTask(cancelTokenSrc.Token),
                    cancelTokenSrc.Token)
                    .ContinueWith(t => cancelTokenSrc.Dispose());

            this.Tasks.Push((task, cancelTokenSrc));

            return true;
        }

        /// <summary>
        /// タスクを1つ減らす。
        /// </summary>
        /// <returns>
        /// 削除できたならば終了指示済みのタスク。タスクが存在しないならば null 。
        /// </returns>
        /// <remarks>
        /// 呼び出し元で排他制御を行うこと。
        /// </remarks>
        private Task PopTask()
        {
            if (this.Tasks.Count <= 0)
            {
                return null;
            }

            var t = this.Tasks.Pop();

            // 終了指示
            // タスク終了時に t.cancelTokenSource は Dispose される
            t.cancelTokenSource.Cancel();

            return t.task;
        }

        /// <summary>
        /// タスク処理を行う。
        /// </summary>
        /// <param name="cancelToken">キャンセルトークン。</param>
        private void ExecuteTask(CancellationToken cancelToken)
        {
            // 終了指示があるまでループ
            for (
                ;
                !cancelToken.IsCancellationRequested;
                Thread.Sleep(this.OperationUpdateIntervalMilliseconds))
            {
                // 対象 IProcessOperation オブジェクト決定
                IProcessOperation processOperation = null;
                lock (this.OperationTaskLock)
                {
                    // 別の場所で lock して Cancel が呼ばれるため、
                    // この位置でもキャンセル済みか否か確認しておく
                    if (cancelToken.IsCancellationRequested)
                    {
                        break;
                    }

                    if (this.OperationIndex >= this.Operations.Count)
                    {
                        this.OperationIndex = 0;
                    }
                    if (this.OperationIndex < this.Operations.Count)
                    {
                        // 処理対象取得
                        processOperation = this.Operations[this.OperationIndex];
                        ++this.OperationIndex;
                    }
                }
                if (processOperation == null)
                {
                    continue;
                }

                // プロセス配列取得
                Process[] processes = null;
                lock (((ICollection)this.ProcessesMap).SyncRoot)
                {
                    // Dispose 呼び出しされている可能性があるため確認
                    if (cancelToken.IsCancellationRequested)
                    {
                        break;
                    }

                    // プロセス配列ディクショナリ更新判定
                    var timeNow = this.ProcessesStopwatch.ElapsedMilliseconds;
                    var timeInterval = timeNow - this.ProcessesMapUpdateTime;
                    if (
                        this.ProcessesMapUpdateTime < 0 ||
                        timeInterval >= this.ProcessListUpdateIntervalMilliseconds)
                    {
                        // 新規作成させるためにクリア
                        this.ProcessesMap.Clear();
                        this.ProcessesMapUpdateTime = timeNow;
                    }

                    // プロセス配列取得or作成
                    processes =
                        this.ProcessesMap.GetOrAdd(
                            processOperation.ProcessFileName,
                            name => Process.GetProcessesByName(name));
                }

                // 更新処理
                try
                {
                    processOperation.Update(processes);
                }
                catch (Exception ex)
                {
                    lock (((ICollection)this.LastExceptionMap).SyncRoot)
                    {
                        // Dispose 呼び出しされている可能性があるため確認
                        if (cancelToken.IsCancellationRequested)
                        {
                            break;
                        }

                        this.LastExceptionMap[processOperation] = ex;
                    }
                }
            }
        }

        #region IDisposable の実装

        /// <summary>
        /// リソースを破棄する。
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// リソース破棄の実処理を行う。
        /// </summary>
        /// <param name="disposing">
        /// Dispose メソッドから呼び出された場合は true 。
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
            List<Task> removedTasks = null;

            lock (this.OperationTaskLock)
            {
                this.Operations.Clear();
                this.OperationIndex = 0;

                if (this.Tasks.Count > 0)
                {
                    removedTasks = new List<Task>(this.Tasks.Count);
                    while (this.Tasks.Count > 0)
                    {
                        var task = this.PopTask();
                        if (task == null)
                        {
                            break;
                        }
                        removedTasks.Add(task);
                    }

                    // 念のためクリア
                    this.Tasks.Clear();
                }
            }
            lock (((ICollection)this.ProcessesMap).SyncRoot)
            {
                this.ProcessesMap.Clear();
            }
            lock (((ICollection)this.LastExceptionMap).SyncRoot)
            {
                this.LastExceptionMap.Clear();
            }

            // lock 内で Wait するとデッドロックするので注意
            if (removedTasks != null && removedTasks.Count > 0)
            {
                Task.WaitAll(removedTasks.ToArray());
            }
        }

        #endregion
    }
}
