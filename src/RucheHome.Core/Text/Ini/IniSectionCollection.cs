using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using RucheHome.Diagnostics;

namespace RucheHome.Text.Ini
{
    /// <summary>
    /// INIファイル形式のセクションコレクションクラス。
    /// </summary>
    public class IniSectionCollection : Collection<IniSection>
    {
        /// <summary>
        /// コンストラクタ。
        /// </summary>
        public IniSectionCollection() : base()
        {
        }

        /// <summary>
        /// 指定した名前を持つセクションのアイテムコレクションを取得する。
        /// </summary>
        /// <param name="name">
        /// セクション名。先頭と末尾の空白文字は除去される。
        /// 制御文字および '\r', '\n', '[', ']' が含まれていてはならない。
        /// </param>
        /// <returns>アイテムコレクション。</returns>
        /// <remarks>
        /// セクションが見つからなければ、
        /// 空のアイテムコレクションを持つセクションが追加される。
        /// </remarks>
        public IniItemCollection this[string name]
        {
            get
            {
                var formattedName = IniSection.FormatName(name, nameof(name));

                var index = this.IndexOf(formattedName);
                return ((index < 0) ? this.Add(formattedName) : this[index]).Items;
            }
        }

        /// <summary>
        /// 指定した名前を持つセクションが含まれているか否かを取得する。
        /// </summary>
        /// <param name="name">
        /// セクション名。先頭と末尾の空白文字は除去される。
        /// 制御文字および '\r', '\n', '[', ']' が含まれていてはならない。
        /// </param>
        /// <returns>含まれているならば true 。そうでなければ false 。</returns>
        public bool Contains(string name)
        {
            var formattedName = IniSection.FormatName(name, nameof(name));

            return this.Any(s => s.Name == formattedName);
        }

        /// <summary>
        /// 指定した名前を持つセクションのインデックスを検索する。
        /// </summary>
        /// <param name="name">
        /// セクション名。先頭と末尾の空白文字は除去される。
        /// 制御文字および '\r', '\n', '[', ']' が含まれていてはならない。
        /// </param>
        /// <returns>インデックス。セクションが含まれていないならば -1 。</returns>
        public int IndexOf(string name)
        {
            var formattedName = IniSection.FormatName(name, nameof(name));

            for (int i = 0; i < this.Count; ++i)
            {
                if (this[i].Name == formattedName)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// 指定した名前とアイテムコレクションを持つセクションを末尾に追加する。
        /// </summary>
        /// <param name="name">
        /// セクション名。先頭と末尾の空白文字は除去される。
        /// 制御文字および '\r', '\n', '[', ']' が含まれていてはならない。
        /// </param>
        /// <param name="items">アイテムコレクション。</param>
        /// <returns>追加されたセクション。</returns>
        public IniSection Add(string name, IniItemCollection items)
        {
            var section = new IniSection(name, items);
            this.Add(section);
            return section;
        }

        /// <summary>
        /// 指定した名前と空のアイテムコレクションを持つセクションを末尾に追加する。
        /// </summary>
        /// <param name="name">
        /// セクション名。制御文字や改行文字が含まれていてはならない。
        /// </param>
        /// <returns>追加されたセクション。</returns>
        public IniSection Add(string name) =>
            this.Add(name, new IniItemCollection());

        /// <summary>
        /// 指定した名前と空のアイテムコレクションを持つセクションを挿入する。
        /// </summary>
        /// <param name="index">挿入先のインデックス。</param>
        /// <param name="name">
        /// セクション名。先頭と末尾の空白文字は除去される。
        /// 制御文字および '\r', '\n', '[', ']' が含まれていてはならない。
        /// </param>
        /// <param name="items">アイテムコレクション。</param>
        /// <returns>挿入されたセクション。</returns>
        public IniSection Insert(int index, string name, IniItemCollection items)
        {
            var section = new IniSection(name, items);
            this.Insert(index, section);
            return section;
        }

        /// <summary>
        /// 指定した名前と空のアイテムコレクションを持つセクションを挿入する。
        /// </summary>
        /// <param name="index">挿入先のインデックス。</param>
        /// <param name="name">
        /// セクション名。先頭と末尾の空白文字は除去される。
        /// 制御文字および '\r', '\n', '[', ']' が含まれていてはならない。
        /// </param>
        /// <returns>挿入されたセクション。</returns>
        public IniSection Insert(int index, string name) =>
            this.Insert(index, name, new IniItemCollection());

        /// <summary>
        /// 指定した名前を持つセクションを削除する。
        /// </summary>
        /// <param name="name">
        /// セクション名。先頭と末尾の空白文字は除去される。
        /// 制御文字および '\r', '\n', '[', ']' が含まれていてはならない。
        /// </param>
        /// <returns>削除できたならば true 。そうでなければ false 。</returns>
        public bool Remove(string name)
        {
            var formattedName = IniSection.FormatName(name, nameof(name));

            var index = this.IndexOf(formattedName);
            if (index < 0)
            {
                return false;
            }

            this.RemoveAt(index);
            return true;
        }

        /// <summary>
        /// このオブジェクトのクローンを作成する。
        /// </summary>
        /// <returns>このオブジェクトのクローン。</returns>
        public IniSectionCollection Clone() =>
            new IniSectionCollection(this.Select(s => s.Clone()).ToList());

        /// <summary>
        /// クローン用のコンストラクタ。
        /// </summary>
        /// <param name="list">ラップ対象のリスト。</param>
        public IniSectionCollection(IList<IniSection> list) : base(list)
        {
        }

        #region Collection<IniFileSection> のオーバライド

        /// <summary>
        /// セクションの挿入時に呼び出される。
        /// </summary>
        /// <param name="index">挿入先のインデックス。</param>
        /// <param name="section">挿入するセクション。</param>
        protected override void InsertItem(int index, IniSection section)
        {
            ArgumentValidation.IsNotNull(section, nameof(section));

            if (this.Contains(section.Name))
            {
                throw new ArgumentException(
                    '"' + section.Name + @""" is already contained section name.",
                    nameof(section));
            }

            base.InsertItem(index, section);
        }

        /// <summary>
        /// セクションの上書き時に呼び出される。
        /// </summary>
        /// <param name="index">上書き先のインデックス。</param>
        /// <param name="section">上書きするセクション。</param>
        protected override void SetItem(int index, IniSection section)
        {
            ArgumentValidation.IsNotNull(section, nameof(section));

            var contained = this.IndexOf(section.Name);
            if (contained >= 0 && contained != index)
            {
                throw new ArgumentException(
                    '"' + section.Name + @""" is already contained section name.",
                    nameof(section));
            }

            base.SetItem(index, section);
        }

        #endregion

        #region Object のオーバライド

        /// <summary>
        /// INIファイル形式文字列値を取得する。
        /// </summary>
        /// <returns>INIファイル形式文字列値。</returns>
        public override string ToString() =>
            string.Join(Environment.NewLine, this.Select(s => s.ToString()));

        #endregion
    }
}
