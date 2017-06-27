using System;

using static RucheHome.Util.ArgumentValidater;

namespace RucheHome.Talker
{
    /// <summary>
    /// 文章読み上げソフトパラメータ情報クラス。
    /// </summary>
    /// <typeparam name="TId">パラメータID型。</typeparam>
    public class ParameterInfo<TId> : IParameterInfo
    {
        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="id">パラメータID。</param>
        /// <param name="displayName">表示名。</param>
        /// <param name="digits">小数部の桁数。</param>
        /// <param name="defaultValue">既定値。</param>
        /// <param name="minValue">最小許容値。</param>
        /// <param name="maxValue">最大許容値。</param>
        public ParameterInfo(
            TId id,
            string displayName,
            int digits = 0,
            decimal defaultValue = 0,
            decimal minValue = 0,
            decimal maxValue = decimal.MaxValue)
        {
            ValidateArgumentOutOfRange(digits, 0, int.MaxValue, nameof(digits));
            if (minValue > maxValue)
            {
                throw new ArgumentException(@"minValue > maxValue");
            }
            ValidateArgumentOutOfRange(
                defaultValue,
                minValue,
                maxValue,
                nameof(defaultValue));

            this.Id = id;
            this.DisplayName = displayName;
            this.Digits = digits;
            this.DefaultValue = defaultValue;
            this.MinValue = minValue;
            this.MaxValue = maxValue;
        }

        /// <summary>
        /// パラメータIDを取得する。
        /// </summary>
        public TId Id { get; }

        /// <summary>
        /// 表示名を取得する。
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// 小数部の桁数を取得する。
        /// </summary>
        public int Digits { get; }

        /// <summary>
        /// 既定値を取得する。
        /// </summary>
        public decimal DefaultValue { get; }

        /// <summary>
        /// 最小許容値を取得する。
        /// </summary>
        public decimal MinValue { get; }

        /// <summary>
        /// 最大許容値を取得する。
        /// </summary>
        public decimal MaxValue { get; }

        #region IParameterInfo の明示的実装

        object IParameterInfo.Id => this.Id;

        #endregion
    }
}
