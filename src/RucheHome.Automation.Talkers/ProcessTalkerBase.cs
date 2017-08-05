using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using RucheHome.Diagnostics;
using RucheHome.Text.Extensions;

namespace RucheHome.Automation.Talkers
{
    /// <summary>
    /// <see cref="IProcessTalker{TParameterId}"/> インタフェースの抽象実装クラス。
    /// </summary>
    /// <typeparam name="TParameterId">パラメータID型。</typeparam>
    /// <remarks>
    /// <see cref="IProcessTalker"/> インタフェースの各メソッドは互いに排他制御されている。
    /// そのため、派生クラスで抽象メソッドを実装する際、それらのメソッドを呼び出さないこと。
    /// </remarks>
    public abstract class ProcessTalkerBase<TParameterId>
        : ProcessOperationBase, IProcessTalker<TParameterId>
    {
        /// <summary>
        /// コンストラクタ。
        /// </summary>
        public ProcessTalkerBase() : base() { }

        /// <summary>
        /// 現在の <see cref="State"/> では処理を行えないことを示すメッセージを作成する。
        /// </summary>
        /// <returns>エラーメッセージ。アイドル状態である場合は null 。</returns>
        protected string MakeStateErrorMessage()
        {
            var state = this.State;
            if (state == TalkerState.Idle)
            {
                return null;
            }

            var stateMessage = this.StateMessage;
            if (stateMessage != null)
            {
                return stateMessage;
            }

            switch (state)
            {
            case TalkerState.None:
                return @"起動していません。";

            case TalkerState.Fail:
                return @"不正な状態です。";

            case TalkerState.Startup:
                return @"起動完了していません。";

            case TalkerState.Cleanup:
                return @"終了処理中です。";

            case TalkerState.Speaking:
                return @"トーク中は処理できません。";

            case TalkerState.Blocking:
                return @"処理できない状態です。";

            case TalkerState.FileSaving:
                return @"音声保存中は処理できません。";

            case TalkerState.Idle:
                return null;

            default:
                break;
            }

            ThreadTrace.WriteLine($@"Invalid talker state. ({(int)this.State})");
            return @"不正な状態です。";
        }

        /// <summary>
        /// 状態エラーメッセージを付随メッセージとする <see cref="Result{T}"/> 値を作成する。
        /// </summary>
        /// <typeparam name="T">戻り値の型。</typeparam>
        /// <param name="value">メソッドの戻り値。既定では default(T) 。</param>
        /// <returns><see cref="Result{T}"/> 値。</returns>
        protected Result<T> MakeStateErrorResult<T>(T value = default(T)) =>
            (value, this.MakeStateErrorMessage());

        /// <summary>
        /// 音声保存先ディレクトリを作成し、書き込み権限を確認する。
        /// </summary>
        /// <param name="dirPath">ディレクトリパス。</param>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        private static Result<bool> CreateSaveDirectory(string dirPath)
        {
            if (string.IsNullOrWhiteSpace(dirPath))
            {
                return (false, @"保存先フォルダーパスが不正です。");
            }

            // ディレクトリ作成
            if (!Directory.Exists(dirPath))
            {
                try
                {
                    Directory.CreateDirectory(dirPath);
                }
                catch (Exception ex)
                {
                    ThreadTrace.WriteException(ex);
                    return (false, @"保存先フォルダーを作成できませんでした。");
                }
            }

            // テンポラリファイルパス作成
            string tempFilePath = null;
            try
            {
                for (uint i = 0; tempFilePath == null || File.Exists(tempFilePath); ++i)
                {
                    tempFilePath = Path.Combine(dirPath, i.ToString());
                }
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
                return (false, @"保存先フォルダーの書き込み権限を確認できませんでした。");
            }

            // 書き込み確認
            try
            {
                File.WriteAllBytes(tempFilePath, new byte[] { 0 });
            }
            catch (Exception ex)
            {
                ThreadTrace.WriteException(ex);
                return (false, @"保存先フォルダーの書き込み権限がありません。");
            }
            finally
            {
                try
                {
                    File.Delete(tempFilePath);
                }
                catch { }
            }

            return true;
        }

        /// <summary>
        /// <see cref="SaveFile"/> メソッドの lock 内で
        /// PropertyChanged イベントを処理中であるか否かを取得する。
        /// </summary>
        /// <remarks>
        /// <para>
        /// <see cref="SaveFile"/> メソッドは処理の関係上
        /// lock 内で PropertyChanged イベントを発生させる必要があるため、
        /// イベント内で他のメソッドが呼び出されてデッドロックになることを防ぐ必要がある。
        /// </para>
        /// <para>
        /// このプロパティ値を lock より手前で確認することでデッドロックを防ぐ。
        /// このプロパティ値が true の場合、プロパティ値変更は発生せず、
        /// <see cref="State"/> は <see cref="TalkerState.FileSaving"/> である前提でよい。
        /// </para>
        /// </remarks>
        private bool IsPropertyChangedOnSaveFile { get; set; } = false;

        #region 要オーバライド

        /// <summary>
        /// 名前を取得する。
        /// </summary>
        /// <remarks>
        /// インスタンス生成後に値が変化することはない。
        /// </remarks>
        public abstract string TalkerName { get; }

        /// <summary>
        /// 文章の最大許容文字数を取得する。
        /// </summary>
        /// <remarks>
        /// <para>既定では <see cref="int.MaxValue"/> を返す。</para>
        /// <para>インスタンス生成後に値が変化することはない。</para>
        /// </remarks>
        public virtual int TextLengthLimit { get; } = int.MaxValue;

        /// <summary>
        /// 空白文を設定することが可能か否かを取得する。
        /// </summary>
        /// <remarks>
        /// インスタンス生成後に値が変化することはない。
        /// </remarks>
        public abstract bool CanSetBlankText { get; }

        /// <summary>
        /// 空白文を音声ファイル保存させることが可能か否かを取得する。
        /// </summary>
        /// <remarks>
        /// インスタンス生成後に値が変化することはない。
        /// </remarks>
        public abstract bool CanSaveBlankText { get; }

        /// <summary>
        /// キャラクター設定を保持しているか否かを取得する。
        /// </summary>
        /// <remarks>
        /// インスタンス生成後に値が変化することはない。
        /// </remarks>
        public abstract bool HasCharacters { get; }

        /// <summary>
        /// 操作対象プロセスの状態を調べる。
        /// </summary>
        /// <param name="process">
        /// 操作対象プロセス。
        /// <see cref="ProcessTalkerBase{TParameterId}"/> 実装から
        /// null や操作対象外プロセスが渡されることはない。
        /// </param>
        /// <returns>状態値。</returns>
        /// <remarks>
        /// このメソッドの戻り値によって <see cref="State"/> 等が更新される。
        /// 付随メッセージも <see cref="StateMessage"/> に利用される。
        /// </remarks>
        protected abstract Result<TalkerState> CheckState(Process process);

        /// <summary>
        /// <see cref="UpdatePropertiesByAction"/> メソッドによってプロパティが
        /// 1 つ以上更新された時に呼び出される。
        /// </summary>
        /// <param name="changedPropertyNames">
        /// 変更されたプロパティ名のコレクション。必ず要素数 1 以上となる。
        /// </param>
        /// <returns>追加で更新通知するプロパティ名の列挙。不要ならば null 。</returns>
        /// <remarks>
        /// 既定では何も行わず null を返す。
        /// </remarks>
        protected virtual IEnumerable<string> OnPropertiesChanged(
            IReadOnlyCollection<string> changedPropertyNames)
            =>
            null;

        /// <summary>
        /// 有効キャラクターの一覧を取得する。
        /// </summary>
        /// <returns>有効キャラクター配列。取得できなかった場合は null 。</returns>
        /// <remarks>
        /// <para>
        /// <see cref="ProcessTalkerBase{TParameterId}"/> 実装からは、
        /// <see cref="HasCharacters"/> が true かつ
        /// <see cref="CanOperate"/> が true の時のみ呼び出される。
        /// </para>
        /// <para>
        /// 既定では、 <see cref="HasCharacters"/> が true ならば
        /// <see cref="NotImplementedException"/> 例外を、そうでなければ
        /// <see cref="NotSupportedException"/> 例外を送出する。
        /// </para>
        /// <para>
        /// <see cref="HasCharacters"/> が true の場合は必ずオーバライドすること。
        /// </para>
        /// </remarks>
        protected virtual Result<ReadOnlyCollection<string>> GetAvailableCharactersImpl() =>
            throw (this.HasCharacters ?
                (Exception)new NotImplementedException() : new NotSupportedException());

        /// <summary>
        /// 現在選択されているキャラクターを取得する。
        /// </summary>
        /// <returns>キャラクター。取得できなかった場合は null 。</returns>
        /// <remarks>
        /// <para>
        /// <see cref="ProcessTalkerBase{TParameterId}"/> 実装からは、
        /// <see cref="HasCharacters"/> が true かつ
        /// <see cref="CanOperate"/> が true の時のみ呼び出される。
        /// </para>
        /// <para>
        /// 既定では、 <see cref="HasCharacters"/> が true ならば
        /// <see cref="NotImplementedException"/> 例外を、そうでなければ
        /// <see cref="NotSupportedException"/> 例外を送出する。
        /// </para>
        /// <para>
        /// <see cref="HasCharacters"/> が true の場合は必ずオーバライドすること。
        /// </para>
        /// </remarks>
        protected virtual Result<string> GetCharacterImpl() =>
            throw (this.HasCharacters ?
                (Exception)new NotImplementedException() : new NotSupportedException());

        /// <summary>
        /// キャラクターを選択させる。
        /// </summary>
        /// <param name="character">
        /// キャラクター。
        /// <see cref="ProcessTalkerBase{TParameterId}"/> 実装から null が渡されることはない。
        /// 有効でないキャラクターは渡されうる。
        /// </param>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        /// <remarks>
        /// <para>
        /// <see cref="ProcessTalkerBase{TParameterId}"/> 実装からは、
        /// <see cref="HasCharacters"/> が true かつ
        /// <see cref="CanOperate"/> が true の時のみ呼び出される。
        /// </para>
        /// <para>
        /// 既定では、 <see cref="HasCharacters"/> が true ならば
        /// <see cref="NotImplementedException"/> 例外を、そうでなければ
        /// <see cref="NotSupportedException"/> 例外を送出する。
        /// </para>
        /// <para>
        /// <see cref="HasCharacters"/> が true の場合は必ずオーバライドすること。
        /// </para>
        /// </remarks>
        protected virtual Result<bool> SetCharacterImpl(string character) =>
            throw (this.HasCharacters ?
                (Exception)new NotImplementedException() : new NotSupportedException());

        /// <summary>
        /// 現在設定されている文章を取得する。
        /// </summary>
        /// <returns>文章。取得できなかった場合は null 。</returns>
        /// <remarks>
        /// <see cref="ProcessTalkerBase{TParameterId}"/> 実装からは、
        /// <see cref="CanOperate"/> が true の時のみ呼び出される。
        /// </remarks>
        protected abstract Result<string> GetTextImpl();

        /// <summary>
        /// 文章を設定する。
        /// </summary>
        /// <param name="text">
        /// 文章。
        /// <see cref="ProcessTalkerBase{TParameterId}"/> 実装から
        /// <see cref="TextLengthLimit"/> を超える文字数の値や null が渡されることはない。
        /// </param>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        /// <remarks>
        /// <para>
        /// <see cref="ProcessTalkerBase{TParameterId}"/> 実装からは、
        /// <see cref="CanOperate"/> が true の時のみ呼び出される。
        /// </para>
        /// <para>
        /// <see cref="CanSetBlankText"/> が
        /// false の場合、空白文チェックを呼び出し元で実施済みだが、
        /// <see cref="string.IsNullOrWhiteSpace"/> による簡易的なチェックであるため、
        /// 例えば記号のみの文章の場合等に設定失敗する可能性がある。
        /// </para>
        /// </remarks>
        protected abstract Result<bool> SetTextImpl(string text);

        /// <summary>
        /// 現在のパラメータ一覧を取得する。
        /// </summary>
        /// <param name="targetParameterIds">
        /// 取得対象のパラメータID列挙。 null ならば存在する全パラメータを対象とする。
        /// </param>
        /// <returns>
        /// パラメータIDとその値のディクショナリ。取得できなかった場合は null 。
        /// </returns>
        /// <remarks>
        /// <see cref="ProcessTalkerBase{TParameterId}"/> 実装からは、
        /// <see cref="CanOperate"/> が true の時のみ呼び出される。
        /// </remarks>
        protected abstract Result<Dictionary<TParameterId, decimal>> GetParametersImpl(
            IEnumerable<TParameterId> targetParameterIds);

        /// <summary>
        /// パラメータ群を設定する。
        /// </summary>
        /// <param name="parameters">
        /// 設定するパラメータIDとその値の列挙。
        /// <see cref="ProcessTalkerBase{TParameterId}"/> 実装から null が渡されることはない。
        /// </param>
        /// <returns>
        /// 個々のパラメータIDとその設定成否を保持するディクショナリ。
        /// 処理を行えない状態ならば null 。
        /// </returns>
        /// <remarks>
        /// <para>
        /// <see cref="ProcessTalkerBase{TParameterId}"/> 実装からは、
        /// <see cref="CanOperate"/> が true の時のみ呼び出される。
        /// </para>
        /// <para>設定処理自体行わなかったパラメータIDは戻り値のキーに含めないこと。</para>
        /// </remarks>
        protected abstract Result<Dictionary<TParameterId, Result<bool>>> SetParametersImpl(
            IEnumerable<KeyValuePair<TParameterId, decimal>> parameters);

        /// <summary>
        /// 現在の文章の読み上げを開始させる。
        /// </summary>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        /// <remarks>
        /// <para>
        /// <see cref="ProcessTalkerBase{TParameterId}"/> 実装からは、
        /// <see cref="State"/> が <see cref="TalkerState.Idle"/> または
        /// <see cref="TalkerState.Speaking"/> の時のみ呼び出される。
        /// </para>
        /// <para>
        /// <see cref="State"/> が <see cref="TalkerState.Speaking"/>
        /// の場合、事前に呼び出し元で <see cref="StopImpl"/>
        /// を呼び出し、その成功を確認済みの状態で呼び出される。
        /// </para>
        /// <para>
        /// 読み上げ開始の成否を確認するまでブロッキングする。読み上げ完了は待たない。
        /// </para>
        /// </remarks>
        protected abstract Result<bool> SpeakImpl();

        /// <summary>
        /// 読み上げを停止させる。
        /// </summary>
        /// <returns>成功したか既に停止中ならば true 。そうでなければ false 。</returns>
        /// <remarks>
        /// <para>
        /// <see cref="ProcessTalkerBase{TParameterId}"/> 実装からは、
        /// <see cref="State"/> が <see cref="TalkerState.Speaking"/> の時のみ呼び出される。
        /// </para>
        /// <para>読み上げ停止の成否を確認するまでブロッキングする。</para>
        /// </remarks>
        protected abstract Result<bool> StopImpl();

        /// <summary>
        /// 現在の文章の音声ファイル保存を行わせる。
        /// </summary>
        /// <param name="filePath">
        /// 音声ファイルの保存先希望パス。
        /// <see cref="ProcessTalkerBase{TParameterId}"/>
        /// 実装からは <see cref="Path.GetFullPath"/> に成功したフルパスが渡される。
        /// また、親ディレクトリは必ず作成済みとなる。
        /// </param>
        /// <returns>
        /// 実際に保存された音声ファイルのパス。分割されている場合はそのうちの1ファイル。
        /// 保存に失敗した場合は null 。
        /// </returns>
        /// <remarks>
        /// <para>
        /// <see cref="ProcessTalkerBase{TParameterId}"/> 実装からは、
        /// <see cref="CanOperate"/> が true の時のみ呼び出される。
        /// </para>
        /// <para>
        /// <see cref="CanSaveBlankText"/> が
        /// false の場合、空白文チェックを呼び出し元で実施済みだが、
        /// <see cref="string.IsNullOrWhiteSpace"/> による簡易的なチェックであるため、
        /// 例えば記号のみの文章の場合等に保存失敗する可能性がある。
        /// </para>
        /// <para>音声ファイル保存の成否を確認するまでブロッキングする。</para>
        /// </remarks>
        protected abstract Result<string> SaveFileImpl(string filePath);

        /// <summary>
        /// <see cref="Process.CloseMainWindow"/> 呼び出し直前に呼び出される。
        /// </summary>
        /// <param name="process">
        /// 操作対象プロセス。
        /// <see cref="ProcessTalkerBase{TParameterId}"/> 実装から
        /// null や操作対象外プロセスが渡されることはない。
        /// </param>
        /// <remarks>
        /// 既定では何も行わない。
        /// </remarks>
        protected virtual void OnProcessExiting(Process process)
        {
            // 何もしない
        }

        /// <summary>
        /// <see cref="Process.CloseMainWindow"/>
        /// 呼び出し後の操作対象プロセスが終了済みか否かを調べる。
        /// </summary>
        /// <param name="process">
        /// <see cref="Process.CloseMainWindow"/> 呼び出し後の操作対象プロセス。
        /// 呼び出し元で <see cref="Process.Refresh"/> 呼び出し済み。
        /// <see cref="ProcessTalkerBase{TParameterId}"/> 実装から
        /// null や操作対象外プロセスが渡されることはない。
        /// </param>
        /// <returns>
        /// 終了を確認できたならば true 。
        /// ブロッキング処理等により終了できないことを確認できたならば null 。
        /// いずれも確認できなければ false 。
        /// </returns>
        /// <remarks>
        /// 既定では <see cref="Process.WaitForExit(int)"/> と
        /// <see cref="CheckState(Process)"/> を用いて終了もしくはブロッキング判定を行う。
        /// </remarks>
        protected virtual bool? CheckProcessExited(Process process)
        {
            if (process.WaitForExit(0))
            {
                return true;
            }

            switch (this.CheckState(process).Value)
            {
            case TalkerState.None:
                return true;

            case TalkerState.Blocking:
            case TalkerState.FileSaving:
                return null;
            }

            return false;
        }

        #endregion

        #region ProcessOperationBase のオーバライド

        /// <summary>
        /// 操作対象プロセスを取得する。
        /// </summary>
        /// <remarks>
        /// 実装の sealed 化のためのオーバライド。
        /// </remarks>
        protected override sealed Process TargetProcess => base.TargetProcess;

        /// <summary>
        /// メインウィンドウハンドルを取得する。
        /// </summary>
        /// <remarks>
        /// 実装の sealed 化のためのオーバライド。
        /// </remarks>
        public override sealed IntPtr MainWindowHandle => base.MainWindowHandle;

        /// <summary>
        /// 操作対象が生存状態であるか否かを取得する。
        /// </summary>
        /// <remarks>
        /// <para><see cref="Update"/> メソッド呼び出しによって更新される。</para>
        /// <para>
        /// <see cref="State"/> が
        /// <see cref="TalkerState.None"/>, <see cref="TalkerState.Fail"/>,
        /// <see cref="TalkerState.Startup"/>, <see cref="TalkerState.Cleanup"/>
        /// のいずれでもなければ true を返す。
        /// </para>
        /// </remarks>
        public override sealed bool IsAlive
        {
            get
            {
                // State の評価を1回にするために一旦変数に入れる
                var state = this.State;
                return (
                    state != TalkerState.None &&
                    state != TalkerState.Fail &&
                    state != TalkerState.Startup &&
                    state != TalkerState.Cleanup);
            }
        }

        /// <summary>
        /// 操作対象が操作可能な状態であるか否かを取得する。
        /// </summary>
        /// <remarks>
        /// <para><see cref="Update"/> メソッド呼び出しによって更新される。</para>
        /// <para>
        /// <see cref="State"/> が
        /// <see cref="TalkerState.Idle"/> または
        /// <see cref="TalkerState.Speaking"/> ならば true を返す。
        /// </para>
        /// </remarks>
        public override sealed bool CanOperate
        {
            get
            {
                // State の評価を1回にするために一旦変数に入れる
                var state = this.State;
                return (state == TalkerState.Idle || state == TalkerState.Speaking);
            }
        }

        /// <summary>
        /// 状態を更新する。
        /// </summary>
        /// <param name="processes">
        /// 対象プロセス検索先列挙。メソッド内でプロセスリストを取得させるならば null 。
        /// </param>
        public override sealed void Update(IEnumerable<Process> processes = null)
        {
            // SaveFile 処理中のデッドロック回避
            if (!this.IsPropertyChangedOnSaveFile)
            {
                base.Update(processes);
            }
        }

        /// <summary>
        /// <see cref="Update"/> メソッドの実処理を行う。
        /// </summary>
        /// <param name="processes">
        /// 対象プロセス検索先列挙。メソッド内でプロセスリストを取得させるならば null 。
        /// </param>
        /// <returns>プロパティ値変更通知を行うデリゲート。通知不要ならば null 。</returns>
        /// <remarks>
        /// 実装の sealed 化のためのオーバライド。
        /// </remarks>
        protected override sealed Action UpdateCore(IEnumerable<Process> processes) =>
            base.UpdateCore(processes);

        /// <summary>
        /// 操作対象プロセスによって状態を更新する。
        /// </summary>
        /// <param name="targetProcess">
        /// 操作対象プロセス。見つからなかった場合は null 。
        /// </param>
        /// <returns>プロパティ値変更通知を行うデリゲート。通知不要ならば null 。</returns>
        /// <remarks>
        /// 実装の sealed 化のためのオーバライド。
        /// </remarks>
        protected override sealed Action UpdateByTargetProcess(Process targetProcess) =>
            base.UpdateByTargetProcess(targetProcess);

        /// <summary>
        /// 操作対象プロセスを基に、プロパティ値を変更するデリゲートを作成する。
        /// </summary>
        /// <param name="targetProcess">
        /// 操作対象プロセス。見つからなかった場合は null 。
        /// </param>
        /// <returns>プロパティ値を変更するデリゲート。</returns>
        /// <remarks>
        /// <see cref="CheckState(Process)"/> を呼び出し、その戻り値によって
        /// <see cref="TargetProcess"/>, <see cref="State"/>, <see cref="StateMessage"/>
        /// を変更するデリゲートを作成する。
        /// </remarks>
        protected override sealed Action MakeUpdatePropertiesAction(Process targetProcess) =>
            () =>
            {
                // ベースクラス処理によって TargetProcess を更新
                base.MakeUpdatePropertiesAction(targetProcess)?.Invoke();
                var process = this.TargetProcess;

                // 状態確認
                var (state, stateMessage) =
                    (process != null) ? this.CheckState(process) : TalkerState.None;

                // state が None なら TargetProcess を null に更新
                if (state == TalkerState.None && process != null)
                {
                    base.MakeUpdatePropertiesAction(null)?.Invoke();
                }

                this.State = state;
                this.StateMessage = stateMessage;
            };

        /// <summary>
        /// プロパティ値を変更するデリゲートを呼び出し、
        /// 呼び出しの前後で値の変化したプロパティ名のコレクションを返す。
        /// </summary>
        /// <param name="updateProperties">プロパティ値を変更するデリゲート。</param>
        /// <returns>値の変化したプロパティ名のコレクション。</returns>
        /// <remarks>
        /// ベースクラスの監視対象に加えて
        /// <see cref="State"/>, <see cref="StateMessage"/> の変更を監視する。
        /// また、 <see cref="OnPropertiesChanged"/> を呼び出す。
        /// </remarks>
        protected override sealed IReadOnlyCollection<string> UpdatePropertiesByAction(
            Action updateProperties)
        {
            var oldState = this.State;
            var oldStateMessage = this.StateMessage;

            // ベースクラス処理
            var propNames =
                base.UpdatePropertiesByAction(updateProperties)?.ToList() ??
                new List<string>();

            if (this.State != oldState)
            {
                propNames.Add(nameof(State));
            }
            if (this.StateMessage != oldStateMessage)
            {
                propNames.Add(nameof(StateMessage));
            }

            // 派生クラス処理
            if (propNames.Count > 0)
            {
                var extraNames = this.OnPropertiesChanged(propNames);
                if (extraNames != null)
                {
                    propNames.AddRange(extraNames);
                }
            }

            return propNames;
        }

        /// <summary>
        /// 実行ファイルパスを取得する。
        /// </summary>
        /// <returns>実行ファイルパス。取得できなかった場合は null 。</returns>
        /// <remarks>
        /// <see cref="State"/> が <see cref="TalkerState.None"/> または
        /// <see cref="TalkerState.Fail"/> の場合は取得できない。
        /// </remarks>
        public override sealed Result<string> GetProcessFilePath()
        {
            // SaveFile 処理中のデッドロック回避
            // ダミーのロックオブジェクトを使わせる
            bool propChangedOnSaveFile = this.IsPropertyChangedOnSaveFile;
            object lockObj = propChangedOnSaveFile ? (new object()) : this.LockObject;

            lock (lockObj)
            {
                if (
                    !propChangedOnSaveFile &&
                    (this.State == TalkerState.None || this.State == TalkerState.Fail))
                {
                    return MakeStateErrorResult<string>();
                }

                return this.GetProcessFilePathCore();
            }
        }

        /// <summary>
        /// <see cref="ProcessOperationBase.GetProcessFilePathCore">
        /// ProcessOperationBase.GetProcessFilePathCore
        /// </see> メソッドを呼び出す。
        /// </summary>
        /// <returns>実行ファイルパス。取得できなかった場合は null 。</returns>
        /// <remarks>
        /// 実装の sealed 化のためのオーバライド。
        /// </remarks>
        protected override sealed Result<string> GetProcessFilePathCore() =>
            base.GetProcessFilePathCore();

        /// <summary>
        /// プロセスを起動させる。
        /// </summary>
        /// <param name="processFilePath">実行ファイルパス。</param>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        /// <remarks>
        /// 起動開始の成否を確認するまでブロッキングする。起動完了は待たない。
        /// 既に起動している場合は何もせず true を返す。
        /// </remarks>
        public override sealed Result<bool> RunProcess(string processFilePath)
        {
            // SaveFile 処理中のデッドロック回避
            if (this.IsPropertyChangedOnSaveFile)
            {
                // 既に起動しているはずなので何もしない
                return (true, @"既に起動しています。");
            }

            return base.RunProcess(processFilePath);
        }

        /// <summary>
        /// <see cref="ProcessOperationBase.RunProcess"/> メソッドの実処理を行う。
        /// </summary>
        /// <param name="processFilePath">実行ファイルパス。</param>
        /// <param name="raisePropertyChanged">
        /// プロパティ値変更通知を行うデリゲートの設定先。通知不要ならば null が設定される。
        /// </param>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        protected override sealed Result<bool> RunProcessCore(
            string processFilePath,
            out Action raisePropertyChanged)
        {
            raisePropertyChanged = null;

            switch (this.State)
            {
            case TalkerState.None:
                break;

            case TalkerState.Fail:
                return MakeStateErrorResult(false);

            default:
                // 既に起動しているので何もしない
                return (true, @"既に起動しています。");
            }

            return base.RunProcessCore(processFilePath, out raisePropertyChanged);
        }

        /// <summary>
        /// <see cref="ProcessOperationBase.RunProcessCore"/>
        /// メソッドの既定の実装によってプロセスを起動させた後の処理を行う。
        /// </summary>
        /// <param name="process">起動済みプロセス。製品情報の一致も確認済み。</param>
        /// <param name="raisePropertyChanged">
        /// プロパティ値変更通知を行うデリゲートの設定先。通知不要ならば null が設定される。
        /// </param>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        protected override sealed Result<bool> RunProcessImpl(
            Process process,
            out Action raisePropertyChanged)
        {
            // result.Value には IsAlive の値が入る
            var result = base.RunProcessImpl(process, out raisePropertyChanged);

            if (!result.Value)
            {
                switch (this.State)
                {
                case TalkerState.Startup:
                    // 起動途中でも成功扱い
                    return true;

                case TalkerState.Cleanup:
                    // 多重起動に引っ掛かるとここに来る場合がある
                    // 通常は事前に弾くはず ⇒ 起動状態を確認できていない可能性が高い
                    return (false, @"管理者権限で起動済みの可能性があります。");

                case TalkerState.Fail:
                    return MakeStateErrorResult(false);

                default:
                    break;
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
        /// 終了の成否を確認するまでブロッキングする。
        /// 既に終了している場合は何もせず true を返す。
        /// </remarks>
        public override sealed Result<bool?> ExitProcess()
        {
            // SaveFile 処理中のデッドロック回避
            if (this.IsPropertyChangedOnSaveFile)
            {
                return MakeStateErrorResult<bool?>(false);
            }

            return base.ExitProcess();
        }

        /// <summary>
        /// <see cref="ProcessOperationBase.ExitProcess"/> メソッドの実処理を行う。
        /// </summary>
        /// <param name="raisePropertyChanged">
        /// プロパティ値変更通知を行うデリゲートの設定先。通知不要ならば null が設定される。
        /// </param>
        /// <returns>
        /// 成功したならば true 。
        /// 終了通知には成功したがプロセス側で終了が抑止されたならば null 。
        /// 失敗したならば false 。
        /// </returns>
        protected override sealed Result<bool?> ExitProcessCore(
            out Action raisePropertyChanged)
        {
            raisePropertyChanged = null;

            switch (this.State)
            {
            case TalkerState.None:
                // 既に終了しているので何もしない
                return (true, @"終了済みです。");

            case TalkerState.Startup:
            case TalkerState.Cleanup:
            case TalkerState.Idle:
            case TalkerState.Speaking:
                break;

            default:
                return MakeStateErrorResult<bool?>(false);
            }

            return base.ExitProcessCore(out raisePropertyChanged);
        }

        /// <summary>
        /// 処理対象プロセスに対して終了通知を行う。
        /// </summary>
        /// <param name="targetProcess">処理対象プロセス。</param>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        protected override sealed Result<bool> RequestProcessExit(Process targetProcess)
        {
            // 終了前処理
            this.OnProcessExiting(targetProcess);

            // Cleanup 状態以外ならば終了通知
            return
                (this.State == TalkerState.Cleanup) ?
                    true : base.RequestProcessExit(targetProcess);
        }

        /// <summary>
        /// 処理対象プロセスが終了するか終了不可能な状態になるまで待機する。
        /// </summary>
        /// <param name="targetProcess">終了通知成功済みの処理対象プロセス。</param>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        protected override Result<bool> WaitForProcessExitedOrBlocking(Process targetProcess)
        {
            // 終了orブロッキング状態まで待つ
            if (!WaitUntil(() => this.CheckProcessExited(targetProcess) != false))
            {
                return (false, @"終了状態へ遷移しませんでした。");
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
        protected override sealed Result<bool?> ExitProcessImpl(
            out Action raisePropertyChanged)
        {
            base.ExitProcessImpl(out raisePropertyChanged);

            // ベースクラス処理の戻り値は無視して State で判定
            switch (this.State)
            {
            case TalkerState.Fail:
                return MakeStateErrorResult<bool?>(false);

            case TalkerState.Blocking:
            case TalkerState.FileSaving:
                return (null, @"本体側で終了が保留されました。");

            default:
                // Startup, Idle, Speaking は終了後即再起動したものと判断
                break;
            }

            return true;
        }

        #endregion

        #region ITalker<TParameterId> の実装

        /// <summary>
        /// 現在のパラメータ一覧を取得する。
        /// </summary>
        /// <param name="targetParameterIds">
        /// 取得対象のパラメータID列挙。 null ならば存在する全パラメータを対象とする。
        /// </param>
        /// <returns>
        /// パラメータIDとその値のディクショナリ。取得できなかった場合は null 。
        /// </returns>
        public Result<Dictionary<TParameterId, decimal>> GetParameters(
            IEnumerable<TParameterId> targetParameterIds = null)
        {
            // SaveFile 処理中のデッドロック回避
            if (this.IsPropertyChangedOnSaveFile)
            {
                return MakeStateErrorResult<Dictionary<TParameterId, decimal>>();
            }

            lock (this.LockObject)
            {
                if (!this.CanOperate)
                {
                    return MakeStateErrorResult<Dictionary<TParameterId, decimal>>();
                }

                return this.GetParametersImpl(targetParameterIds);
            }
        }

        /// <summary>
        /// パラメータ群を設定する。
        /// </summary>
        /// <param name="parameters">設定するパラメータIDとその値の列挙。</param>
        /// <returns>
        /// 個々のパラメータIDとその設定成否を保持するディクショナリ。
        /// 処理を行えない状態ならば null 。
        /// </returns>
        /// <remarks>
        /// 設定処理自体行われなかったパラメータIDは戻り値のキーに含まれない。
        /// </remarks>
        public Result<Dictionary<TParameterId, Result<bool>>> SetParameters(
            IEnumerable<KeyValuePair<TParameterId, decimal>> parameters)
        {
            // SaveFile 処理中のデッドロック回避
            if (this.IsPropertyChangedOnSaveFile)
            {
                return MakeStateErrorResult<Dictionary<TParameterId, Result<bool>>>();
            }

            lock (this.LockObject)
            {
                if (!this.CanOperate)
                {
                    return MakeStateErrorResult<Dictionary<TParameterId, Result<bool>>>();
                }

                // 引数が null なら空の列挙を渡す
                // 処理を行えない状態の可能性があるため
                return
                    this.SetParametersImpl(
                        parameters ??
                        Enumerable.Empty<KeyValuePair<TParameterId, decimal>>());
            }
        }

        #endregion

        #region ITalker の実装

        /// <summary>
        /// 状態を取得する。
        /// </summary>
        /// <remarks>
        /// <see cref="Update"/> メソッド呼び出しによって更新される。
        /// </remarks>
        public TalkerState State { get; private set; } = TalkerState.None;

        /// <summary>
        /// 現在の状態に関する付随メッセージを取得する。
        /// </summary>
        /// <remarks>
        /// <para>
        /// <see cref="Update"/> メソッド呼び出しによって更新される。 null にもなりうる。
        /// </para>
        /// <para>
        /// <see cref="State"/> が <see cref="TalkerState.Idle"/> の場合は無視される。
        /// それ以外の場合、 <see cref="MakeStateErrorMessage"/> の戻り値に利用される。
        /// </para>
        /// </remarks>
        public string StateMessage { get; private set; } = null;

        /// <summary>
        /// 有効キャラクターの一覧を取得する。
        /// </summary>
        /// <returns>有効キャラクター配列。取得できなかった場合は null 。</returns>
        /// <remarks>
        /// <see cref="HasCharacters"/> が false の場合は取得できない。
        /// </remarks>
        public Result<ReadOnlyCollection<string>> GetAvailableCharacters()
        {
            if (!this.HasCharacters)
            {
                return (null, @"サポートしていません。");
            }

            // SaveFile 処理中のデッドロック回避
            if (this.IsPropertyChangedOnSaveFile)
            {
                return MakeStateErrorResult<ReadOnlyCollection<string>>();
            }

            lock (this.LockObject)
            {
                if (!this.CanOperate)
                {
                    return MakeStateErrorResult<ReadOnlyCollection<string>>();
                }

                return this.GetAvailableCharactersImpl();
            }
        }

        /// <summary>
        /// 現在選択されているキャラクターを取得する。
        /// </summary>
        /// <returns>キャラクター。取得できなかった場合は null 。</returns>
        /// <remarks>
        /// <see cref="HasCharacters"/> が false の場合は取得できない。
        /// </remarks>
        public Result<string> GetCharacter()
        {
            if (!this.HasCharacters)
            {
                return (null, @"サポートしていません。");
            }

            // SaveFile 処理中のデッドロック回避
            if (this.IsPropertyChangedOnSaveFile)
            {
                return MakeStateErrorResult<string>();
            }

            lock (this.LockObject)
            {
                if (!this.CanOperate)
                {
                    return MakeStateErrorResult<string>();
                }

                return this.GetCharacterImpl();
            }
        }

        /// <summary>
        /// キャラクターを選択させる。
        /// </summary>
        /// <param name="character">キャラクター。</param>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        /// <remarks>
        /// <see cref="HasCharacters"/> が false の場合は設定できない。
        /// </remarks>
        public Result<bool> SetCharacter(string character)
        {
            if (!this.HasCharacters)
            {
                return (false, @"サポートしていません。");
            }

            // SaveFile 処理中のデッドロック回避
            if (this.IsPropertyChangedOnSaveFile)
            {
                return MakeStateErrorResult(false);
            }

            lock (this.LockObject)
            {
                if (!this.CanOperate)
                {
                    return MakeStateErrorResult(false);
                }

                return this.SetCharacterImpl(character ?? @"");
            }
        }

        /// <summary>
        /// 現在設定されている文章を取得する。
        /// </summary>
        /// <returns>文章。取得できなかった場合は null 。</returns>
        public Result<string> GetText()
        {
            // SaveFile 処理中のデッドロック回避
            if (this.IsPropertyChangedOnSaveFile)
            {
                return MakeStateErrorResult<string>();
            }

            lock (this.LockObject)
            {
                if (!this.CanOperate)
                {
                    return MakeStateErrorResult<string>();
                }

                return this.GetTextImpl();
            }
        }

        /// <summary>
        /// 文章を設定する。
        /// </summary>
        /// <param name="text">文章。</param>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        public Result<bool> SetText(string text)
        {
            // SaveFile 処理中のデッドロック回避
            if (this.IsPropertyChangedOnSaveFile)
            {
                return MakeStateErrorResult(false);
            }

            lock (this.LockObject)
            {
                if (!this.CanOperate)
                {
                    return MakeStateErrorResult(false);
                }

                var value =
                    (text == null) ?
                        @"" :
                        (text.Length > this.TextLengthLimit) ?
                            text.SubstringSurrogateSafe(0, this.TextLengthLimit) : text;

                // 空白文チェック
                if (!this.CanSetBlankText && string.IsNullOrWhiteSpace(value))
                {
                    return (false, @"空白文を設定することはできません。");
                }

                return this.SetTextImpl(value);
            }
        }

        /// <summary>
        /// 現在の文章の読み上げを開始させる。
        /// </summary>
        /// <returns>成功したならば true 。そうでなければ false 。</returns>
        /// <remarks>
        /// 読み上げ開始の成否を確認するまでブロッキングする。読み上げ完了は待たない。
        /// 既に読み上げ中の場合は一旦停止して再度開始させる。
        /// </remarks>
        public Result<bool> Speak()
        {
            // SaveFile 処理中のデッドロック回避
            if (this.IsPropertyChangedOnSaveFile)
            {
                return MakeStateErrorResult(false);
            }

            Result<bool> result;
            Action raisePropChanged = null;

            try
            {
                lock (this.LockObject)
                {
                    if (!this.CanOperate)
                    {
                        return MakeStateErrorResult(false);
                    }

                    // 読み上げ中ならまず停止させる
                    if (this.State == TalkerState.Speaking)
                    {
                        var r = this.StopImpl();
                        if (!r.Value)
                        {
                            return r;
                        }
                    }

                    result = this.SpeakImpl();

                    // 状態更新
                    raisePropChanged = this.UpdateByCurrentTargetProcess();
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
        /// 読み上げを停止させる。
        /// </summary>
        /// <returns>成功したか既に停止中ならば true 。そうでなければ false 。</returns>
        /// <remarks>
        /// 読み上げ停止の成否を確認するまでブロッキングする。
        /// 既にアイドル状態の場合は何もせず true を返す。
        /// </remarks>
        public Result<bool> Stop()
        {
            // SaveFile 処理中のデッドロック回避
            if (this.IsPropertyChangedOnSaveFile)
            {
                return MakeStateErrorResult(false);
            }

            Result<bool> result;
            Action raisePropChanged = null;

            try
            {
                lock (this.LockObject)
                {
                    if (!this.CanOperate)
                    {
                        return MakeStateErrorResult(false);
                    }
                    if (this.State == TalkerState.Idle)
                    {
                        // 既に停止しているので何もしない
                        return (true, @"停止済みです。");
                    }

                    result = this.StopImpl();

                    // 状態更新
                    raisePropChanged = this.UpdateByCurrentTargetProcess();
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
        /// 現在の文章の音声ファイル保存を行わせる。
        /// </summary>
        /// <param name="filePath">音声ファイルの保存先希望パス。</param>
        /// <returns>
        /// 実際に保存された音声ファイルのパス。分割されている場合はそのうちの1ファイル。
        /// 保存に失敗した場合は null 。
        /// </returns>
        /// <remarks>
        /// 音声ファイル保存の成否を確認するまでブロッキングする。
        /// </remarks>
        public Result<string> SaveFile(string filePath)
        {
            // SaveFile 処理中のデッドロック回避
            if (this.IsPropertyChangedOnSaveFile)
            {
                return MakeStateErrorResult<string>();
            }

            Result<string> result;
            Action raisePropChanged = null;

            try
            {
                lock (this.LockObject)
                {
                    if (!this.CanOperate)
                    {
                        return MakeStateErrorResult<string>();
                    }

                    // フルパス取得
                    string fileFullPath = null;
                    try
                    {
                        fileFullPath = Path.GetFullPath(filePath);
                    }
                    catch (PathTooLongException ex)
                    {
                        ThreadTrace.WriteException(ex);
                        return (null, @"保存先ファイルパスが長すぎます。");
                    }
                    catch (Exception ex)
                    {
                        ThreadTrace.WriteException(ex);
                        return (null, @"保存先ファイルパスが不正です。");
                    }

                    // 読み上げ中ならまず停止させる
                    if (this.State == TalkerState.Speaking)
                    {
                        var r = this.StopImpl();
                        if (!r.Value)
                        {
                            return (null, r.Message);
                        }
                    }

                    // 空白文チェック
                    if (!this.CanSaveBlankText)
                    {
                        var r = this.GetTextImpl();
                        if (string.IsNullOrWhiteSpace(r.Value))
                        {
                            return (
                                null,
                                (r.Value == null) ?
                                    r.Message : @"空白文を音声保存することはできません。");
                        }
                    }

                    // 音声ファイル保存中状態に変更
                    raisePropChanged =
                        this.MakePropertiesChangedAction(
                            this.UpdatePropertiesByAction(
                                () =>
                                {
                                    this.State = TalkerState.FileSaving;
                                    this.StateMessage = null;
                                }));
                    if (raisePropChanged != null)
                    {
                        try
                        {
                            // PropertyChanged イベント処理中フラグを立てる
                            this.IsPropertyChangedOnSaveFile = true;

                            // プロパティ値変更通知
                            // lock 内だが IsSaveFileRunning によりデッドロック回避する
                            raisePropChanged.Invoke();
                        }
                        catch (Exception ex)
                        {
                            ThreadTrace.WriteException(ex);
                            return (
                                null,
                                ex.Message ??
                                (ex.GetType().Name + @" 例外が発生しました。"));
                        }
                        finally
                        {
                            raisePropChanged = null;

                            // PropertyChanged イベント処理中フラグを下ろす
                            this.IsPropertyChangedOnSaveFile = false;
                        }
                    }

                    // 保存先ディレクトリ作成
                    {
                        var dirPath = Path.GetDirectoryName(fileFullPath);
                        var r = CreateSaveDirectory(dirPath);
                        if (!r.Value)
                        {
                            return (null, r.Message);
                        }
                    }

                    result = this.SaveFileImpl(fileFullPath);

                    // 状態更新
                    raisePropChanged = this.UpdateByCurrentTargetProcess();
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
                        null,
                        ex.Message ?? (ex.GetType().Name + @" 例外が発生しました。"));
                }
            }

            return result;
        }

        #endregion

        #region ITalker の明示的実装

        /// <summary>
        /// 現在のパラメータ一覧を取得する。
        /// </summary>
        /// <param name="targetParameterIds">
        /// 取得対象のパラメータID列挙。 null ならば存在する全パラメータを対象とする。
        /// </param>
        /// <returns>
        /// パラメータIDとその値のディクショナリ。取得できなかった場合は null 。
        /// </returns>
        Result<Dictionary<object, decimal>> ITalker.GetParameters(
            IEnumerable targetParameterIds)
        {
            var result = this.GetParameters(targetParameterIds?.OfType<TParameterId>());

            return (
                result.Value?.ToDictionary(kv => (object)kv.Key, kv => kv.Value),
                result.Message);
        }

        /// <summary>
        /// パラメータ群を設定する。
        /// </summary>
        /// <param name="parameters">設定するパラメータIDとその値の列挙。</param>
        /// <returns>
        /// 個々のパラメータIDとその設定成否を保持するディクショナリ。
        /// 処理を行えない状態ならば null 。
        /// </returns>
        /// <remarks>
        /// 設定処理自体行われなかったパラメータIDは戻り値のキーに含まれない。
        /// </remarks>
        Result<Dictionary<object, Result<bool>>> ITalker.SetParameters(
            IEnumerable<KeyValuePair<object, decimal>> parameters)
        {
            var result =
                this.SetParameters(
                    parameters?
                        .Where(kv => kv.Key is TParameterId)
                        .Select(
                            kv =>
                                new KeyValuePair<TParameterId, decimal>(
                                    (TParameterId)kv.Key,
                                    kv.Value)));

            return (
                result.Value?.ToDictionary(kv => (object)kv.Key, kv => kv.Value),
                result.Message);
        }

        #endregion
    }
}
