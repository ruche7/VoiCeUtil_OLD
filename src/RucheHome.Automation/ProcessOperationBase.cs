using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using RucheHome.Diagnostics;
using RucheHome.ObjectModel;

namespace RucheHome.Automation
{
    /// <summary>
    /// <see cref="IProcessOperation"/> インタフェースの抽象実装クラス。
    /// </summary>
    /// <remarks>
    /// 既定では、 <see cref="Update"/>, <see cref="GetProcessFilePath"/>,
    /// <see cref="RunProcess"/>, <see cref="ExitProcess"/> の各メソッドは互いに
    /// <see cref="LockObject"/> を用いて排他制御され、
    /// 末尾に "Core" を付けた名前のメソッドに実処理を委譲している。
    /// </remarks>
    public abstract class ProcessOperationBase : BindableBase, IProcessOperation
    {
        /// <summary>
        /// コンストラクタ。
        /// </summary>
        public ProcessOperationBase() { }

        /// <summary>
        /// 排他制御用オブジェクト。
        /// </summary>
        protected readonly object LockObject = new object();

        /// <summary>
        /// 操作対象プロセスを取得する。
        /// </summary>
        /// <remarks>
        /// <para>起動していないならば null とすること。</para>
        /// <para>
        /// 既定では、派生クラスでこのプロパティの値を更新するには
        /// <see cref="UpdateByTargetProcess"/> を用いること。
        /// </para>
        /// </remarks>
        protected virtual Process TargetProcess => this.targetProcess;
        private Process targetProcess = null;

        /// <summary>
        /// 待機処理の標準タイムアウトミリ秒数値。
        /// </summary>
        protected const int StandardTimeoutMilliseconds = 1500;

        /// <summary>
        /// デリゲートの戻り値が条件を満たさない間待機する。
        /// </summary>
        /// <typeparam name="T">戻り値の型。</typeparam>
        /// <param name="getter">戻り値を取得するデリゲート。</param>
        /// <param name="predicator">戻り値の条件判定を行うデリゲート。</param>
        /// <param name="timeoutMilliseconds">
        /// タイムアウトミリ秒数。既定値は <see cref="StandardTimeoutMilliseconds"/> 。
        /// 負数ならば無制限。
        /// </param>
        /// <returns>
        /// predicator が true を返したならばその値。
        /// タイムアウトしたならばタイムアウト直前の値。
        /// </returns>
        protected static T WaitUntil<T>(
            Func<T> getter,
            Func<T, bool> predicator,
            int timeoutMilliseconds = StandardTimeoutMilliseconds)
        {
            ArgumentValidation.IsNotNull(getter, nameof(getter));
            ArgumentValidation.IsNotNull(predicator, nameof(predicator));

            var value = getter();

            for (
                var sw = Stopwatch.StartNew();
                !predicator(value) &&
                (timeoutMilliseconds < 0 || sw.ElapsedMilliseconds < timeoutMilliseconds);)
            {
                Thread.Yield();
                value = getter();
            }

            return value;
        }

        /// <summary>
        /// デリゲートの戻り値が false の間待機する。
        /// </summary>
        /// <param name="getter">戻り値を取得するデリゲート。</param>
        /// <param name="timeoutMilliseconds">
        /// タイムアウトミリ秒数。既定値は <see cref="StandardTimeoutMilliseconds"/> 。
        /// 負数ならば無制限。
        /// </param>
        /// <returns>true を返したならば true 。タイムアウトしたならば false 。</returns>
        protected static bool WaitUntil(
            Func<bool> getter,
            int timeoutMilliseconds = StandardTimeoutMilliseconds)
            =>
            WaitUntil(getter, f => f, timeoutMilliseconds);

        /// <summary>
        /// <see cref="TargetProcess"/> によって状態を更新する。
        /// </summary>
        /// <param name="refresh">
        /// プロセスの内部状態更新を行うならば true 。既定では true 。
        /// </param>
        /// <returns>プロパティ値変更通知を行うデリゲート。通知不要ならば null 。</returns>
        protected Action UpdateByCurrentTargetProcess(bool refresh = true)
        {
            var process = this.TargetProcess;

            if (refresh)
            {
                process?.Refresh();
            }

            return this.UpdateByTargetProcess(process);
        }

        #region 要オーバライド

        /// <summary>
        /// 操作対象プロセスの製品名情報を取得する。
        /// </summary>
        /// <remarks>
        /// <para>操作対象プロセスか否かの判別に利用される。</para>
        /// <para>インスタンス生成後に値が変化することはない。</para>
        /// </remarks>
        public abstract string ProcessProduct { get; }

        /// <summary>
        /// 操作対象プロセスの実行ファイル名(拡張子なし)を取得する。
        /// </summary>
        /// <remarks>
        /// <para>
        /// <see cref="Process.GetProcessesByName(string)">Process.GetProcessesByName</see>
        /// メソッドの引数として利用できる。
        /// </para>
        /// <para>インスタンス生成後に値が変化することはない。</para>
        /// </remarks>
        public abstract string ProcessFileName { get; }

        #endregion

        #region Update メソッドの既定の実処理

        /// <summary>
        /// <see cref="Update"/> メソッドの実処理を行う。
        /// </summary>
        /// <param name="processes">
        /// 対象プロセス検索先列挙。メソッド内でプロセスリストを取得させるならば null 。
        /// </param>
        /// <returns>プロパティ値変更通知を行うデリゲート。通知不要ならば null 。</returns>
        /// <remarks>
        /// 既定では <see cref="FindTargetProcess"/> によってプロセスを検索し、
        /// 見つかったか否かに関わらず <see cref="UpdateByTargetProcess"/> を呼び出す。
        /// </remarks>
        protected virtual Action UpdateCore(
            IEnumerable<Process> processes = null)
            =>
            this.UpdateByTargetProcess(this.FindTargetProcess(processes));

        /// <summary>
        /// プロセス検索オブジェクト。
        /// </summary>
        private readonly ProcessDetector ProcessDetector = new ProcessDetector();

        /// <summary>
        /// 操作対象プロセスを検索する。
        /// </summary>
        /// <param name="processes">
        /// 対象プロセス検索先列挙。メソッド内でプロセスリストを取得させるならば null 。
        /// </param>
        /// <returns>操作対象プロセス。見つからなければ null 。</returns>
        protected Process FindTargetProcess(IEnumerable<Process> processes = null)
        {
            this.ProcessDetector.ProductName = this.ProcessProduct;

            // processes が非 null ならばファイル名は検索条件としない
            this.ProcessDetector.FileName =
                (processes == null) ? this.ProcessFileName : null;

            return this.ProcessDetector.Detect(processes).FirstOrDefault(p => !p.HasExited);
        }

        /// <summary>
        /// 操作対象プロセスによって状態を更新する。
        /// </summary>
        /// <param name="targetProcess">
        /// 操作対象プロセス。見つからなかった場合は null 。
        /// </param>
        /// <returns>プロパティ値変更通知を行うデリゲート。通知不要ならば null 。</returns>
        /// <remarks>
        /// 既定では、まず <see cref="MakeUpdatePropertiesAction"/> を呼び出し、
        /// その戻り値を引数値として <see cref="UpdatePropertiesByAction"/> を呼び出す。
        /// そしてその戻り値を引数値として <see cref="MakePropertiesChangedAction"/>
        /// を呼び出し、その戻り値のデリゲートを返す。
        /// </remarks>
        protected virtual Action UpdateByTargetProcess(Process targetProcess) =>
            this.MakePropertiesChangedAction(
                this.UpdatePropertiesByAction(
                    this.MakeUpdatePropertiesAction(targetProcess)));

        /// <summary>
        /// 操作対象プロセスを基に、プロパティ値を変更するデリゲートを作成する。
        /// </summary>
        /// <param name="targetProcess">
        /// 操作対象プロセス。見つからなかった場合は null 。
        /// </param>
        /// <returns>プロパティ値を変更するデリゲート。</returns>
        /// <remarks>
        /// 既定では、 <see cref="TargetProcess"/> のみを変更するデリゲートを作成する。
        /// </remarks>
        protected virtual Action MakeUpdatePropertiesAction(Process targetProcess)
        {
            var process = (targetProcess?.HasExited == false) ? targetProcess : null;
            return () => this.targetProcess = process;
        }

        /// <summary>
        /// プロパティ値を変更するデリゲートを呼び出し、
        /// 呼び出しの前後で値の変化したプロパティ名のコレクションを返す。
        /// </summary>
        /// <param name="updateProperties">プロパティ値を変更するデリゲート。</param>
        /// <returns>値の変化したプロパティ名のコレクション。</returns>
        /// <remarks>
        /// 既定では、 <see cref="TargetProcess"/>, <see cref="MainWindowHandle"/>,
        /// <see cref="IsAlive"/>, <see cref="CanOperate"/>
        /// の 4 つのプロパティ値について変更を監視する。
        /// </remarks>
        protected virtual IReadOnlyCollection<string> UpdatePropertiesByAction(
            Action updateProperties)
        {
            var oldTargetProcess = this.TargetProcess;
            var oldMainWindowHandle = this.MainWindowHandle;
            var oldAlive = this.IsAlive;
            var oldCanOperate = this.CanOperate;

            updateProperties?.Invoke();

            var propNames = new List<string>();

            if (this.TargetProcess != oldTargetProcess)
            {
                propNames.Add(nameof(TargetProcess));
            }
            if (this.MainWindowHandle != oldMainWindowHandle)
            {
                propNames.Add(nameof(MainWindowHandle));
            }
            if (this.IsAlive != oldAlive)
            {
                propNames.Add(nameof(IsAlive));
            }
            if (this.CanOperate != oldCanOperate)
            {
                propNames.Add(nameof(CanOperate));
            }

            return propNames;
        }

        /// <summary>
        /// 値の変化したプロパティ名について
        /// <see cref="INotifyPropertyChanged.PropertyChanged"/>
        /// イベントを呼び出すデリゲートを作成する。
        /// </summary>
        /// <param name="changedPropertyNames">値の変化したプロパティ名の列挙。</param>
        /// <returns>デリゲート。呼び出し不要ならば null 。</returns>
        protected Action MakePropertiesChangedAction(
            IEnumerable<string> changedPropertyNames)
            =>
            (changedPropertyNames?.Any() != true) ?
                (Action)null :
                (
                    () =>
                    {
                        foreach (var name in changedPropertyNames)
                        {
                            this.RaisePropertyChanged(name);
                        }
                    }
                );

        #endregion

        #region GetProcessFilePath メソッドの既定の実処理

        /// <summary>
        /// <see cref="GetProcessFilePath"/> の実処理を行う。
        /// </summary>
        /// <returns>実行ファイルパス。取得できなかった場合は null 。</returns>
        /// <remarks>
        /// 既定では、 <see cref="TargetProcess"/> が null の場合は取得できない。
        /// </remarks>
        protected virtual Result<string> GetProcessFilePathCore()
        {
            var process = this.TargetProcess;
            if (process == null)
            {
                return (null, @"実行中のみ取得可能です。");
            }

            try
            {
                var filePath = process.MainModule.FileName;
                return (
                    filePath,
                    (filePath == null) ? @"情報を取得できませんでした。" : null);
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
            }
            return (null, @"情報を取得できませんでした。");
        }

        #endregion

        #region RunProcess メソッドの既定の実処理

        /// <summary>
        /// <see cref="RunProcess"/> メソッドの実処理を行う。
        /// </summary>
        /// <param name="processFilePath">実行ファイルパス。</param>
        /// <param name="raisePropertyChanged">
        /// プロパティ値変更通知を行うデリゲートの設定先。通知不要ならば null が設定される。
        /// </param>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        /// <remarks>
        /// 既定では、 <see cref="StartProcess"/> によるプロセスの起動処理を行い、
        /// 処理に成功したならば <see cref="RunProcessImpl"/> を呼び出す。
        /// </remarks>
        protected virtual Result<bool> RunProcessCore(
            string processFilePath,
            out Action raisePropertyChanged)
        {
            raisePropertyChanged = null;

            if (this.IsAlive && this.TargetProcess?.HasExited == false)
            {
                // 既に起動しているので何もしない
                return (true, @"既に起動しています。");
            }

            try
            {
                // 起動処理
                var (process, failMessage) = this.StartProcess(processFilePath);
                if (process == null)
                {
                    return (false, failMessage);
                }

                // 反映
                var result = this.RunProcessImpl(process, out raisePropertyChanged);

                if (result.Value && this.TargetProcess == null)
                {
                    return (false, @"起動を確認できません。");
                }

                return result;
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
            }
            return (false, @"起動処理に失敗しました。");
        }

        /// <summary>
        /// プロセスの起動と、操作対象プロセスであるか否かの確認を行う。
        /// </summary>
        /// <param name="processFilePath">実行ファイルパス。</param>
        /// <returns>起動したプロセス。失敗したならば null 。</returns>
        protected Result<Process> StartProcess(string processFilePath)
        {
            if (string.IsNullOrWhiteSpace(processFilePath))
            {
                return (null, @"実行ファイルパスが不正です。");
            }
            if (!File.Exists(processFilePath))
            {
                return (null, @"実行ファイルが存在しません。");
            }

            Process process = null;

            // 起動
            try
            {
                process = Process.Start(processFilePath);
                if (process == null)
                {
                    return (null, @"起動処理に失敗しました。");
                }
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
                return (null, @"起動処理に失敗しました。");
            }

            bool succeeded = false;
            try
            {
                // 入力待機
                try
                {
                    if (!process.WaitForInputIdle())
                    {
                        return (null, @"起動待機に失敗しました。");
                    }
                }
                catch (Exception ex)
                {
                    // 管理者権限で起動する設定になっていてUACが有効だとここに来る
                    ThreadTrace.WriteException(ex);
                    return (null, @"管理者権限で起動した可能性があります。");
                }

                try
                {
                    // 操作対象プロセスか確認
                    if (
                        process.ProcessName != this.ProcessFileName ||
                        process.MainModule.FileVersionInfo.ProductName != this.ProcessProduct)
                    {
                        // 終了させる
                        // 失敗してもよい
                        try
                        {
                            if (!process.CloseMainWindow())
                            {
                                process.Kill();
                            }
                        }
                        catch (Exception ex)
                        {
                            ThreadDebug.WriteException(ex);
                        }

                        return (null, @"操作対象ではありませんでした。");
                    }

                    // 終了済みでないか確認
                    if (process.HasExited)
                    {
                        return (null, @"起動しましたが即終了しました。");
                    }
                }
                catch (Exception ex)
                {
                    ThreadTrace.WriteException(ex);
                    return (null, @"起動アプリの情報取得に失敗しました。");
                }

                succeeded = true;
            }
            finally
            {
                if (!succeeded)
                {
                    process.Dispose();
                }
            }

            return process;
        }

        /// <summary>
        /// <see cref="RunProcessCore"/>
        /// メソッドの既定の実装によってプロセスを起動させた後の処理を行う。
        /// </summary>
        /// <param name="process">起動済みプロセス。製品情報の一致も確認済み。</param>
        /// <param name="raisePropertyChanged">
        /// プロパティ値変更通知を行うデリゲートの設定先。通知不要ならば null が設定される。
        /// </param>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        /// <remarks>
        /// <para>
        /// 既定ではまず引数 process を破棄する。
        /// 次に <see cref="UpdateCore"/> を引数値 null で呼び出し、戻り値を引数
        /// raisePropertyChanged に設定する。
        /// 最後に <see cref="IsAlive"/> の値を返す。
        /// </para>
        /// <para>
        /// このメソッドで true を返しても、 <see cref="TargetProcess"/> が
        /// null のままの場合は呼び出し元で失敗扱いとなる。
        /// </para>
        /// </remarks>
        protected virtual Result<bool> RunProcessImpl(
            Process process,
            out Action raisePropertyChanged)
        {
            // Process インスタンスのリソースを破棄
            // プロセス自体は起動し続ける
            process.Dispose();

            // 状態更新
            raisePropertyChanged = this.UpdateCore(null);

            var alive = this.IsAlive;
            return (alive, alive ? null : @"起動状態にできませんでした。");
        }

        #endregion

        #region ExitProcess メソッドの既定の実処理

        /// <summary>
        /// <see cref="ExitProcess"/> メソッドの実処理を行う。
        /// </summary>
        /// <param name="raisePropertyChanged">
        /// プロパティ値変更通知を行うデリゲートの設定先。通知不要ならば null が設定される。
        /// </param>
        /// <returns>
        /// 成功したならば true 。
        /// 終了通知には成功したがプロセス側で終了が抑止されたならば null 。
        /// 失敗したならば false 。
        /// </returns>
        /// <remarks>
        /// 既定では、プロセスに対して <see cref="RequestProcessExit"/> と
        /// <see cref="WaitForProcessExitedOrBlocking"/> の呼び出しを行い、
        /// それらの処理に成功したら <see cref="ExitProcessImpl"/> を呼び出す。
        /// </remarks>
        protected virtual Result<bool?> ExitProcessCore(out Action raisePropertyChanged)
        {
            raisePropertyChanged = null;

            var process = this.TargetProcess;
            if (process == null)
            {
                // 既に終了しているので何もしない
                return (true, @"終了済みです。");
            }

            try
            {
                // 終了通知
                var r = this.RequestProcessExit(process);
                if (!r.Value)
                {
                    return (false, r.Message);
                }

                // 終了待機
                r = this.WaitForProcessExitedOrBlocking(process);
                if (!r.Value)
                {
                    return (false, r.Message);
                }

                // 反映
                var result = this.ExitProcessImpl(out raisePropertyChanged);

                if (result.Value == true && this.TargetProcess != null)
                {
                    return (false, @"終了を確認できません。");
                }

                return result;
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
            }
            return (false, @"終了処理に失敗しました。");
        }

        /// <summary>
        /// 処理対象プロセスに対して終了通知を行う。
        /// </summary>
        /// <param name="targetProcess">処理対象プロセス。</param>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        /// <remarks>
        /// 既定では <see cref="Process.CloseMainWindow"/> を呼び出す。
        /// </remarks>
        protected virtual Result<bool> RequestProcessExit(Process targetProcess)
        {
            bool ok = false;

            try
            {
                ok = targetProcess.HasExited ? true : targetProcess.CloseMainWindow();
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
                ok = false;
            }

            return (ok, ok ? null : @"終了通知に失敗しました。");
        }

        /// <summary>
        /// 処理対象プロセスが終了するか終了不可能な状態になるまで待機する。
        /// </summary>
        /// <param name="targetProcess">終了通知成功済みの処理対象プロセス。</param>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        /// <remarks>
        /// 既定では <see cref="Process.WaitForExit()"/> を呼び出す。
        /// </remarks>
        protected virtual Result<bool> WaitForProcessExitedOrBlocking(Process targetProcess)
        {
            try
            {
                targetProcess.WaitForExit();
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
                return (false, @"終了待機に失敗しました。");
            }

            return true;
        }

        /// <summary>
        /// <see cref="ExitProcessCore"/>
        /// メソッドの既定の実装によって終了または終了不可状態まで待機した後の処理を行う。
        /// </summary>
        /// <param name="raisePropertyChanged">
        /// プロパティ値変更通知を行うデリゲートの設定先。通知不要ならば null が設定される。
        /// </param>
        /// <returns>
        /// 終了が確認できたならば true 。
        /// 終了不可状態になったことが確認できたならば null 。
        /// 失敗したならば false 。
        /// </returns>
        /// <remarks>
        /// <para>
        /// 既定では <see cref="UpdateCore"/> を引数値 null で呼び出し、戻り値を引数
        /// raisePropertyChanged に設定する。
        /// そして <see cref="IsAlive"/> の値に応じて true または null を返す。
        /// </para>
        /// <para>
        /// このメソッドで true を返しても、 <see cref="TargetProcess"/>
        /// が非 null のままの場合は呼び出し元で失敗扱いとなる。
        /// </para>
        /// </remarks>
        protected virtual Result<bool?> ExitProcessImpl(out Action raisePropertyChanged)
        {
            raisePropertyChanged = this.UpdateCore(null);

            if (this.IsAlive)
            {
                return (null, @"終了が保留されました。");
            }

            return true;
        }

        #endregion

        #region IProcessOperation の実装

        /// <summary>
        /// メインウィンドウハンドルを取得する。
        /// </summary>
        /// <remarks>
        /// 既定では、 <see cref="TargetProcess"/> が有効かつ
        /// <see cref="IsAlive"/> が true ならば
        /// <see cref="TargetProcess"/>.<see cref="Process.MainWindowHandle"/> を返す。
        /// そうでなければ <see cref="IntPtr.Zero">IntPtr.Zero</see> を返す。
        /// </remarks>
        public virtual IntPtr MainWindowHandle
        {
            get
            {
                var process = this.TargetProcess;
                return
                    (process?.HasExited == false && this.IsAlive) ?
                        process.MainWindowHandle : IntPtr.Zero;
            }
        }

        /// <summary>
        /// 状態を更新する。
        /// </summary>
        /// <param name="processes">
        /// 対象プロセス検索先列挙。メソッド内でプロセスリストを取得させるならば null 。
        /// </param>
        /// <remarks>
        /// <para>
        /// 既定では <see cref="LockObject"/> による排他ロックを行った後に
        /// <see cref="UpdateCore"/> を呼び出す。
        /// その後、戻り値のプロパティ値変更通知デリゲートを排他ロック外で呼び出す。
        /// </para>
        /// <para>
        /// 排他ロック内で <see cref="INotifyPropertyChanged.PropertyChanged"/>
        /// イベントを呼び出してしまうと、アタッチされたデリゲートから他のメソッドが
        /// 呼び出されることによるデッドロックの恐れがあるため、このような処理となっている。
        /// </para>
        /// </remarks>
        public virtual void Update(IEnumerable<Process> processes = null)
        {
            Action raisePropChanged = null;

            try
            {
                lock (this.LockObject)
                {
                    raisePropChanged = this.UpdateCore(processes);
                }
            }
            finally
            {
                raisePropChanged?.Invoke();
            }
        }

        /// <summary>
        /// 実行ファイルパスを取得する。
        /// </summary>
        /// <returns>実行ファイルパス。取得できなかった場合は null 。</returns>
        /// <remarks>
        /// 既定では <see cref="LockObject"/> による排他ロックを行った後に
        /// <see cref="GetProcessFilePathCore"/> を呼び出す。
        /// </remarks>
        public virtual Result<string> GetProcessFilePath()
        {
            lock (this.LockObject)
            {
                return this.GetProcessFilePathCore();
            }
        }

        /// <summary>
        /// プロセスを起動させる。
        /// </summary>
        /// <param name="processFilePath">実行ファイルパス。</param>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        /// <remarks>
        /// <para>
        /// 既定では <see cref="LockObject"/> による排他ロックを行った後に
        /// <see cref="RunProcessCore"/> を呼び出す。
        /// その後、受け取ったプロパティ値変更通知デリゲートを排他ロック外で呼び出す。
        /// プロパティ値変更通知デリゲートが例外を送出した場合は失敗扱いとなる。
        /// </para>
        /// <para>
        /// 排他ロック内で <see cref="INotifyPropertyChanged.PropertyChanged"/>
        /// イベントを呼び出してしまうと、アタッチされたデリゲートから他のメソッドが
        /// 呼び出されることによるデッドロックの恐れがあるため、このような処理となっている。
        /// </para>
        /// </remarks>
        public virtual Result<bool> RunProcess(string processFilePath)
        {
            Result<bool> result;
            Action raisePropChanged = null;

            try
            {
                lock (this.LockObject)
                {
                    result = this.RunProcessCore(processFilePath, out raisePropChanged);
                }
            }
            finally
            {
                try
                {
                    raisePropChanged?.Invoke();
                }
                catch (Exception ex)
                {
                    ThreadTrace.WriteException(ex);
                    result = (
                        false,
                        ex.Message ?? (ex.GetType().Name + @" 例外が発生しました。"));
                }
            }

            return result;
        }

        /// <summary>
        /// プロセスを終了させる。
        /// </summary>
        /// <returns>
        /// 成功したならば true 。
        /// 終了通知には成功したがプロセス側で終了が抑止されたならば null 。
        /// 失敗したならば false 。
        /// </returns>
        /// <remarks>
        /// <para>
        /// 既定では <see cref="LockObject"/> による排他ロックを行った後に
        /// <see cref="ExitProcessCore"/> を呼び出す。
        /// その後、受け取ったプロパティ値変更通知デリゲートを排他ロック外で呼び出す。
        /// プロパティ値変更通知デリゲートが例外を送出した場合は失敗扱いとなる。
        /// </para>
        /// <para>
        /// 排他ロック内で <see cref="INotifyPropertyChanged.PropertyChanged"/>
        /// イベントを呼び出してしまうと、アタッチされたデリゲートから他のメソッドが
        /// 呼び出されることによるデッドロックの恐れがあるため、このような処理となっている。
        /// </para>
        /// </remarks>
        public virtual Result<bool?> ExitProcess()
        {
            Result<bool?> result;
            Action raisePropChanged = null;

            try
            {
                lock (this.LockObject)
                {
                    result = this.ExitProcessCore(out raisePropChanged);
                }
            }
            finally
            {
                try
                {
                    raisePropChanged?.Invoke();
                }
                catch (Exception ex)
                {
                    ThreadTrace.WriteException(ex);
                    result = (
                        false,
                        ex.Message ?? (ex.GetType().Name + @" 例外が発生しました。"));
                }
            }

            return result;
        }

        #endregion

        #region IOperationState の実装

        /// <summary>
        /// 操作対象が生存状態であるか否かを取得する。
        /// </summary>
        /// <remarks>
        /// 既定では <see cref="TargetProcess"/> が有効ならば true を返す。
        /// </remarks>
        public virtual bool IsAlive => (this.TargetProcess?.HasExited == false);

        /// <summary>
        /// 操作対象が操作可能な状態であるか否かを取得する。
        /// </summary>
        /// <remarks>
        /// 既定では <see cref="IsAlive"/> と同じ値を返す。
        /// </remarks>
        public virtual bool CanOperate => this.IsAlive;

        #endregion
    }
}
