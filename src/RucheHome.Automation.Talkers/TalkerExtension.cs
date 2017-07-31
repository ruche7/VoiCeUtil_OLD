using System;
using System.Collections.Generic;
using System.Linq;

namespace RucheHome.Automation.Talkers
{
    /// <summary>
    /// <see cref="ITalker"/> 派生インタフェースの拡張メソッドを提供する静的クラス。
    /// </summary>
    public static class TalkerExtension
    {
        #region ITalker の拡張メソッド群

        /// <summary>
        /// パラメータ群を設定する。
        /// </summary>
        /// <param name="talker"><see cref="ITalker"/> オブジェクト。</param>
        /// <param name="parameters">設定するパラメータIDとその値の列挙。</param>
        /// <returns>
        /// 個々のパラメータIDとその設定成否を保持するディクショナリ。
        /// 処理を行えない状態ならば null 。
        /// </returns>
        /// <remarks>
        /// 設定処理自体行われなかったパラメータIDは戻り値のキーに含まれない。
        /// </remarks>
        public static Result<Dictionary<object, Result<bool>>>
        SetParameters(
            this ITalker talker,
            IEnumerable<(object id, decimal value)> parameters)
            =>
            talker.SetParameters(
                parameters?.Select(
                    iv => new KeyValuePair<object, decimal>(iv.id, iv.value)));

        /// <summary>
        /// パラメータ群を設定する。
        /// </summary>
        /// <param name="talker"><see cref="ITalker"/> オブジェクト。</param>
        /// <param name="parameters">設定するパラメータIDとその値の配列。</param>
        /// <returns>
        /// 個々のパラメータIDとその設定成否を保持するディクショナリ。
        /// 処理を行えない状態ならば null 。
        /// </returns>
        /// <remarks>
        /// 設定処理自体行われなかったパラメータIDは戻り値のキーに含まれない。
        /// </remarks>
        public static Result<Dictionary<object, Result<bool>>>
        SetParameters(
            this ITalker talker,
            params (object id, decimal value)[] parameters)
            =>
            talker.SetParameters(parameters?.AsEnumerable());

        #endregion

        #region ITalker<TParameterId> の拡張メソッド群

        /// <summary>
        /// パラメータ群を設定する。
        /// </summary>
        /// <param name="talker"><see cref="ITalker{TParameterId}"/> オブジェクト。</param>
        /// <param name="parameters">設定するパラメータIDとその値の列挙。</param>
        /// <returns>
        /// 個々のパラメータIDとその設定成否を保持するディクショナリ。
        /// 処理を行えない状態ならば null 。
        /// </returns>
        /// <remarks>
        /// 設定処理自体行われなかったパラメータIDは戻り値のキーに含まれない。
        /// </remarks>
        public static Result<Dictionary<TParameterId, Result<bool>>>
        SetParameters<TParameterId>(
            this ITalker<TParameterId> talker,
            IEnumerable<(TParameterId id, decimal value)> parameters)
            =>
            talker.SetParameters(
                parameters?.Select(
                    iv => new KeyValuePair<TParameterId, decimal>(iv.id, iv.value)));

        /// <summary>
        /// パラメータ群を設定する。
        /// </summary>
        /// <param name="talker"><see cref="ITalker{TParameterId}"/> オブジェクト。</param>
        /// <param name="parameters">設定するパラメータIDとその値の配列。</param>
        /// <returns>
        /// 個々のパラメータIDとその設定成否を保持するディクショナリ。
        /// 処理を行えない状態ならば null 。
        /// </returns>
        /// <remarks>
        /// 設定処理自体行われなかったパラメータIDは戻り値のキーに含まれない。
        /// </remarks>
        public static Result<Dictionary<TParameterId, Result<bool>>>
        SetParameters<TParameterId>(
            this ITalker<TParameterId> talker,
            params (TParameterId id, decimal value)[] parameters)
            =>
            talker.SetParameters(parameters?.AsEnumerable());

        #endregion
    }
}
