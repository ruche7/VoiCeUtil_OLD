using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using static RucheHome.Util.ArgumentValidater;

namespace RucheHome.Talker
{
    /// <summary>
    /// <see cref="IUpdatableTalker"/> インスタンス群の非同期な状態更新処理を提供するクラス。
    /// </summary>
    public class TalkerUpdater : IDisposable
    {
        /// <summary>
        /// コンストラクタ。
        /// </summary>
        public TalkerUpdater()
        {
            // スレッドプールの設定値からタスク数を決定
            ThreadPool.GetMinThreads(out var count, out _);
            this.TaskCountLimit = count;
        }

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="taskCountLimit">並走させるタスクの最大数。 1 以上 1024 以下。</param>
        public TalkerUpdater(int taskCountLimit)
        {
            ValidateArgumentOutOfRange(taskCountLimit, 1, 1024, nameof(taskCountLimit));

            this.TaskCountLimit = taskCountLimit;
        }

        /// <summary>
        /// デストラクタ。
        /// </summary>
        ~TalkerUpdater()
        {
            this.Dispose(false);
        }

        /// <summary>
        /// 並走させるタスクの最大数を取得する。
        /// </summary>
        public int TaskCountLimit { get; }

        /// <summary>
        /// 状態更新処理間隔ミリ秒数を取得または設定する。
        /// </summary>
        public int TalkerUpdateIntervalMilliseconds { get; set; } = 20;

        /// <summary>
        /// プロセスリスト更新間隔ミリ秒数を取得または設定する。
        /// </summary>
        public int ProcessListUpdateIntervalMilliseconds { get; set; } = 100;

        /// <summary>
        /// <see cref="IUpdatableTalker"/> インスタンスを登録する。
        /// </summary>
        /// <param name="talker"><see cref="IUpdatableTalker"/> インスタンス。</param>
        /// <returns>登録できたならば true 。既に登録済みならば false 。</returns>
        public bool Register(IUpdatableTalker talker)
        {
            ValidateArgumentNull(talker, nameof(talker));

            lock (this.lockObject)
            {
                if (this.Talkers.Contains(talker))
                {
                    return false;
                }

                // 追加
                this.Talkers.Add(talker);

                // タスク数が少ないなら追加
                while (
                    this.Tasks.Count < this.TaskCountLimit &&
                    this.Tasks.Count < this.Talkers.Count)
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
        /// <see cref="IUpdatableTalker"/> インスタンスの登録を破棄する。
        /// </summary>
        /// <param name="talker"><see cref="IUpdatableTalker"/> インスタンス。</param>
        /// <returns>破棄できたならば true 。登録されていないならば false 。</returns>
        public bool Remove(IUpdatableTalker talker)
        {
            ValidateArgumentNull(talker, nameof(talker));

            List<Task> removedTasks = null;

            lock (this.lockObject)
            {
                // 位置検索
                var index = this.Talkers.IndexOf(talker);
                if (index < 0)
                {
                    return false;
                }

                // 削除
                this.Talkers.RemoveAt(index);

                // 次回処理対象インデックスより手前を削除したなら補正
                if (this.TargetTalkerIndex > index)
                {
                    --this.TargetTalkerIndex;
                }

                // タスク数が多すぎるなら終わらせる
                if (this.Tasks.Count > this.Talkers.Count)
                {
                    removedTasks = new List<Task>();
                    while (this.Tasks.Count > this.Talkers.Count)
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

            // lock 内で Wait するとデッドロックするので注意
            if (removedTasks != null && removedTasks.Count > 0)
            {
                Task.WaitAll(removedTasks.ToArray());
            }

            return true;
        }

        /// <summary>
        /// 登録済み <see cref="IUpdatableTalker"/> インスタンスリストを取得する。
        /// </summary>
        private List<IUpdatableTalker> Talkers { get; } = new List<IUpdatableTalker>();

        /// <summary>
        /// 次回処理対象の Talkers インデックスを取得または設定する。
        /// </summary>
        private int TargetTalkerIndex { get; set; } = 0;

        /// <summary>
        /// タスクスタックを取得する。
        /// </summary>
        private Stack<(Task task, CancellationTokenSource cancelTokenSource)> Tasks { get; } =
            new Stack<(Task task, CancellationTokenSource cancelTokenSource)>();

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
        /// 排他制御用オブジェクト。
        /// </summary>
        private object lockObject = new object();

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
                Thread.Sleep(this.TalkerUpdateIntervalMilliseconds))
            {
                IUpdatableTalker talker = null;
                Process[] processes = null;

                lock (this.lockObject)
                {
                    // 別の場所で lock して Cancel が呼ばれるため、
                    // この位置でもキャンセル済みか否か確認しておく
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
                        // 更新のためにキャッシュをクリア
                        this.ProcessesMap.Clear();
                        this.ProcessesMapUpdateTime = timeNow;
                    }

                    if (this.TargetTalkerIndex >= this.Talkers.Count)
                    {
                        this.TargetTalkerIndex = 0;
                    }
                    if (this.TargetTalkerIndex < this.Talkers.Count)
                    {
                        // 処理対象取得
                        talker = this.Talkers[this.TargetTalkerIndex];
                        ++this.TargetTalkerIndex;

                        // プロセス配列取得
                        processes =
                            this.ProcessesMap.GetOrAdd(
                                talker.ProcessFileName,
                                name => Process.GetProcessesByName(name));
                    }
                }

                // 更新処理
                talker?.Update(processes);
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

            lock (this.lockObject)
            {
                this.Talkers.Clear();
                this.TargetTalkerIndex = 0;

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
                }

                this.ProcessesMap.Clear();
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
