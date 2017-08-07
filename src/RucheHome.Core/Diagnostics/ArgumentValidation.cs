using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;

namespace RucheHome.Diagnostics
{
    /// <summary>
    /// メソッド実引数の検証処理を提供する静的クラス。
    /// </summary>
    public static class ArgumentValidation
    {
        /// <summary>
        /// 引数値が null ならば ArgumentNullException 例外を送出する。
        /// </summary>
        /// <typeparam name="T">引数の型。</typeparam>
        /// <param name="arg">引数値。</param>
        /// <param name="argName">引数名。例外メッセージに利用される。</param>
        public static void IsNotNull<T>(T arg, string argName = null)
        {
            if (arg == null)
            {
                throw
                    (argName == null) ?
                        new ArgumentNullException() : new ArgumentNullException(argName);
            }
        }

        /// <summary>
        /// 引数値が比較値より小さいならば ArgumentOutOfRangeException 例外を送出する。
        /// </summary>
        /// <typeparam name="T">引数の型。</typeparam>
        /// <param name="arg">引数値。</param>
        /// <param name="minValue">最小許容値。</param>
        /// <param name="argName">引数名。例外メッセージに利用される。</param>
        public static void IsEqualsOrGreaterThan<T>(
            T arg,
            T minValue,
            string argName = null)
        {
            if (Comparer<T>.Default.Compare(arg, minValue) < 0)
            {
                var message = $@"The value is less than {minValue}.";
                throw
                    (argName == null) ?
                        new ArgumentOutOfRangeException(message) :
                        new ArgumentOutOfRangeException(argName, arg, message);
            }
        }

        /// <summary>
        /// 引数値が比較値より大きいならば ArgumentOutOfRangeException 例外を送出する。
        /// </summary>
        /// <typeparam name="T">引数の型。</typeparam>
        /// <param name="arg">引数値。</param>
        /// <param name="maxValue">最大許容値。</param>
        /// <param name="argName">引数名。例外メッセージに利用される。</param>
        public static void IsEqualsOrLessThan<T>(
            T arg,
            T maxValue,
            string argName = null)
        {
            if (Comparer<T>.Default.Compare(arg, maxValue) > 0)
            {
                var message = $@"The value is greater than {maxValue}.";
                throw
                    (argName == null) ?
                        new ArgumentOutOfRangeException(message) :
                        new ArgumentOutOfRangeException(argName, arg, message);
            }
        }

        /// <summary>
        /// 引数値が範囲外ならば ArgumentOutOfRangeException 例外を送出する。
        /// </summary>
        /// <typeparam name="T">引数の型。</typeparam>
        /// <param name="arg">引数値。</param>
        /// <param name="minValue">最小許容値。</param>
        /// <param name="maxValue">最大許容値。</param>
        /// <param name="argName">引数名。例外メッセージに利用される。</param>
        public static void IsWithinRange<T>(
            T arg,
            T minValue = default(T),
            T maxValue = default(T),
            string argName = null)
        {
            IsEqualsOrGreaterThan(arg, minValue, argName);
            IsEqualsOrLessThan(arg, maxValue, argName);
        }

        /// <summary>
        /// 列挙型引数値が定義外の値ならば InvalidEnumArgumentException 例外を送出する。
        /// </summary>
        /// <typeparam name="T">引数の列挙型。</typeparam>
        /// <param name="arg">引数値。</param>
        /// <param name="argName">引数名。例外メッセージに利用される。</param>
        public static void IsEnumDefined<T>(T arg, string argName = null)
            where T : struct, IConvertible
        {
            if (!Enum.IsDefined(typeof(T), arg))
            {
                throw
                    (argName == null) ?
                        new InvalidEnumArgumentException() :
                        new InvalidEnumArgumentException(
                            argName,
                            Convert.ToInt32(arg),
                            typeof(T));
            }
        }

        /// <summary>
        /// 文字列引数値が null または空文字列ならば例外を送出する。
        /// </summary>
        /// <param name="arg">引数値。</param>
        /// <param name="argName">引数名。例外メッセージに利用される。</param>
        public static void IsNotNullOrEmpty(string arg, string argName = null)
        {
            IsNotNull(arg, argName);

            if (arg.Length == 0)
            {
                var message = @"The string is empty.";
                throw
                    (argName == null) ?
                        new ArgumentException(message) :
                        new ArgumentException(message, argName);
            }
        }

        /// <summary>
        /// 文字列引数値が null または空白文字のみで構成されるならば例外を送出する。
        /// </summary>
        /// <param name="arg">引数値。</param>
        /// <param name="argName">引数名。例外メッセージに利用される。</param>
        public static void IsNotNullOrWhiteSpace(
            string arg,
            string argName = null)
        {
            IsNotNull(arg, argName);

            if (string.IsNullOrWhiteSpace(arg))
            {
                var message = @"The string is blank.";
                throw
                    (argName == null) ?
                        new ArgumentException(message) :
                        new ArgumentException(message, argName);
            }
        }

        /// <summary>
        /// 列挙引数値のいずれかの要素が null ならば ArgumentException 例外を送出する。
        /// </summary>
        /// <typeparam name="T">列挙要素型。</typeparam>
        /// <param name="arg">引数値。</param>
        /// <param name="argName">引数名。例外メッセージに利用される。</param>
        /// <param name="elementsName">列挙要素群の名前。例外メッセージに利用される。</param>
        /// <remarks>
        /// 引数 arg 自体が null ならば ArgumentNullException 例外を送出する。
        /// </remarks>
        public static void AreNotNull<T>(
            IEnumerable<T> arg,
            string argName = null,
            string elementsName = null)
            =>
            CheckEnumerable(
                arg,
                argName,
                elementsName,
                a => (a == null) ? @"are null." : null);

        /// <summary>
        /// 列挙引数値のいずれかの要素が null ならば ArgumentException 例外を送出する。
        /// </summary>
        /// <param name="arg">引数値。</param>
        /// <param name="argName">引数名。例外メッセージに利用される。</param>
        /// <param name="elementsName">列挙要素群の名前。例外メッセージに利用される。</param>
        /// <remarks>
        /// 引数 arg 自体が null ならば ArgumentNullException 例外を送出する。
        /// </remarks>
        public static void AreNotNull(
            IEnumerable arg,
            string argName = null,
            string elementsName = null)
            =>
            AreNotNull((arg == null) ? null : arg.Cast<object>(), argName, elementsName);

        /// <summary>
        /// 文字列列挙引数値のいずれかの要素が null または空文字列ならば
        /// ArgumentException 例外を送出する。
        /// </summary>
        /// <param name="arg">引数値。</param>
        /// <param name="argName">引数名。例外メッセージに利用される。</param>
        /// <param name="elementsName">列挙要素群の名前。例外メッセージに利用される。</param>
        /// <remarks>
        /// 引数 arg 自体が null ならば ArgumentNullException 例外を送出する。
        /// </remarks>
        public static void AreNotNullOrEmpty(
            IEnumerable<string> arg,
            string argName = null,
            string elementsName = null)
            =>
            CheckEnumerable(
                arg,
                argName,
                elementsName,
                a => (a == null) ? @"are null." : null,
                a => (a.Length == 0) ? @"are empty." : null);

        /// <summary>
        /// 文字列列挙引数値のいずれかの要素が null または空白文字のみで構成されるならば
        /// ArgumentException 例外を送出する。
        /// </summary>
        /// <param name="arg">引数値。</param>
        /// <param name="argName">引数名。例外メッセージに利用される。</param>
        /// <param name="elementsName">列挙要素群の名前。例外メッセージに利用される。</param>
        /// <remarks>
        /// 引数 arg 自体が null ならば ArgumentNullException 例外を送出する。
        /// </remarks>
        public static void AreNotNullOrWhiteSpace(
            IEnumerable<string> arg,
            string argName = null,
            string elementsName = null)
            =>
            CheckEnumerable(
                arg,
                argName,
                elementsName,
                a => (a == null) ? @"are null." : null,
                a => string.IsNullOrWhiteSpace(a) ? @"are blank." : null);

        /// <summary>
        /// 列挙引数値の各要素を検証する。
        /// </summary>
        /// <typeparam name="T">列挙要素型。</typeparam>
        /// <param name="arg">引数値。</param>
        /// <param name="argName">引数名。例外メッセージに利用される。</param>
        /// <param name="elementsName">
        /// 列挙要素群の名前。例外メッセージに利用される。
        /// null ならば argName が使われる。 argName も null ならば "values" が使われる。
        /// </param>
        /// <param name="validators">
        /// 要素検証デリゲート配列。
        /// 正常ならば null を返す。
        /// 不正ならば "Some (列挙要素群の名前) " に続く例外メッセージ文字列を返す。
        /// </param>
        private static void CheckEnumerable<T>(
            IEnumerable<T> arg,
            string argName,
            string elementsName,
            params Func<T, string>[] validators)
        {
            Debug.Assert(validators != null);

            IsNotNull(arg, argName);

            foreach (var a in arg)
            {
                foreach (var validator in validators)
                {
                    Debug.Assert(validator != null);

                    var message =validator(a);
                    if (message != null)
                    {
                        message =
                            @"Some " +
                            (elementsName ?? argName ?? @"values") +
                            @" " +
                            message;
                        throw
                            (argName == null) ?
                                new ArgumentException(message) :
                                new ArgumentException(message, argName);
                    }
                }
            }
        }
    }
}
