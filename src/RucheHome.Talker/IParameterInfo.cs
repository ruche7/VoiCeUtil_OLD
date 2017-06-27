using System;

namespace RucheHome.Talker
{
    /// <summary>
    /// 文章読み上げソフトパラメータ情報インタフェース。
    /// </summary>
    public interface IParameterInfo
    {
        /// <summary>
        /// パラメータIDを取得する。
        /// </summary>
        object Id { get; }

        /// <summary>
        /// 表示名を取得する。
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// 小数部の桁数を取得する。
        /// </summary>
        int Digits { get; }

        /// <summary>
        /// 既定値を取得する。
        /// </summary>
        decimal DefaultValue { get; }

        /// <summary>
        /// 最小許容値を取得する。
        /// </summary>
        decimal MinValue { get; }

        /// <summary>
        /// 最大許容値を取得する。
        /// </summary>
        decimal MaxValue { get; }
    }
}
