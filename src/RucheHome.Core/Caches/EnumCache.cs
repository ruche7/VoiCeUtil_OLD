using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace RucheHome.Caches
{
    /// <summary>
    /// 列挙型で定義されている全列挙値の情報をキャッシュする静的クラス。
    /// </summary>
    /// <typeparam name="TEnum">列挙型。</typeparam>
    public static class EnumCache<TEnum>
        where TEnum : struct, IComparable, IFormattable, IConvertible
    {
        /// <summary>
        /// 全列挙値のコレクション。
        /// </summary>
        public static readonly ReadOnlyCollection<TEnum> Values =
            Array.AsReadOnly((TEnum[])Enum.GetValues(typeof(TEnum)));

        /// <summary>
        /// 全列挙値名のコレクションを取得する。
        /// </summary>
        /// <remarks>
        /// 初回の参照時にキャッシュを構築する。
        /// </remarks>
        public static ReadOnlyCollection<string> Names => NamesCache.Cache;

        /// <summary>
        /// 全列挙値のハッシュセットを取得する。
        /// </summary>
        /// <remarks>
        /// <para>初回の参照時にキャッシュを構築する。</para>
        /// <para>内部値の等しい列挙値が複数ある場合は1つにまとめられる。</para>
        /// </remarks>
        public static HashSet<TEnum> HashSet => HashSetCache.Cache;

        /// <summary>
        /// 全列挙値名のキャッシュを提供する静的クラス。
        /// </summary>
        private static class NamesCache
        {
            /// <summary>
            /// 全列挙値名のキャッシュ。
            /// </summary>
            public static readonly ReadOnlyCollection<string> Cache =
                Array.AsReadOnly(Enum.GetNames(typeof(TEnum)));
        }

        /// <summary>
        /// 全列挙値のハッシュセットのキャッシュを提供する静的クラス。
        /// </summary>
        private static class HashSetCache
        {
            /// <summary>
            /// 全列挙値のハッシュセット。
            /// </summary>
            public static readonly HashSet<TEnum> Cache = new HashSet<TEnum>(Values);
        }
    }
}
