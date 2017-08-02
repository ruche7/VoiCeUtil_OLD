using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using RucheHome.Diagnostics;

namespace RucheHome.Formats.Ini
{
    /// <summary>
    /// INIファイル形式文字列のパース処理を提供するクラス。
    /// </summary>
    public static class IniParser
    {
        /// <summary>
        /// INIファイル形式文字列を IniFileSectionCollection オブジェクトへパースする。
        /// </summary>
        /// <param name="iniString">INIファイル形式文字列。</param>
        /// <param name="strict">厳格な形式チェックを行うならば true 。</param>
        /// <returns>IniFileSectionCollection オブジェクト。</returns>
        public static IniSectionCollection Parse(
            string iniString,
            bool strict = false)
        {
            ArgumentValidation.IsNotNull(iniString, nameof(iniString));

            var ini = new IniSectionCollection();
            int lineNumber = 0;

            foreach (var line in ReadLines(iniString))
            {
                ++lineNumber;
                var trimmedLine = line.Trim();

                if (trimmedLine.Length == 0 || trimmedLine[0] == ';')
                {
                    continue;
                }

                if (trimmedLine[0] == '[')
                {
                    if (trimmedLine[trimmedLine.Length - 1] == ']')
                    {
                        var sectionName = trimmedLine.Substring(1, trimmedLine.Length - 2);
                        AddSection(ini, lineNumber, sectionName);
                    }
                    else if (strict)
                    {
                        throw new IniFormatException(lineNumber, @"Invalid section format.");
                    }
                    continue;
                }

                var eqIndex = line.IndexOf('=');
                if (eqIndex < 0)
                {
                    if (strict)
                    {
                        throw new IniFormatException(lineNumber, @"Invalid line.");
                    }
                    continue;
                }
                if (ini.Count <= 0)
                {
                    if (strict)
                    {
                        throw new IniFormatException(
                            lineNumber,
                            @"The item is found before a section.");
                    }
                    continue;
                }

                var name = line.Substring(0, eqIndex);
                var value = line.Substring(eqIndex + 1);

                AddItemToLastSection(ini, lineNumber, name, value);
            }

            return ini;
        }

        /// <summary>
        /// 文字列値から文字列を1行ずつ返す列挙を作成する。
        /// </summary>
        /// <param name="s">文字列値。</param>
        /// <returns>文字列を1行ずつ返す列挙。</returns>
        private static IEnumerable<string> ReadLines(string s)
        {
            Debug.Assert(s != null);

            using (var reader = new StringReader(s))
            {
                while (true)
                {
                    var line = reader.ReadLine();
                    if (line == null)
                    {
                        break;
                    }
                    yield return line;
                }
            }
        }

        /// <summary>
        /// IniFileSectionCollection オブジェクトへセクションを追加する。
        /// </summary>
        /// <param name="dest">IniFileSectionCollection オブジェクト。</param>
        /// <param name="lineNumber">行番号。</param>
        /// <param name="sectionName">セクション名。</param>
        private static void AddSection(
            IniSectionCollection dest,
            int lineNumber,
            string sectionName)
        {
            Debug.Assert(dest != null);

            IniSection section = null;

            try
            {
                section = new IniSection(sectionName);
            }
            catch (Exception ex)
            {
                throw new IniFormatException(lineNumber, @"Invalid section name.", ex);
            }

            try
            {
                dest.Add(section);
            }
            catch (Exception ex)
            {
                throw new IniFormatException(
                    lineNumber,
                    @"The section name (""" + section.Name + @""") is duplicated.",
                    ex);
            }
        }

        /// <summary>
        /// IniFileSectionCollection オブジェクトの末尾セクションへアイテムを追加する。
        /// </summary>
        /// <param name="dest">IniFileSectionCollection オブジェクト。</param>
        /// <param name="lineNumber">行番号。</param>
        /// <param name="name">名前。</param>
        /// <param name="value">値。</param>
        private static void AddItemToLastSection(
            IniSectionCollection dest,
            int lineNumber,
            string name,
            string value)
        {
            Debug.Assert(dest != null && dest.Count > 0);

            var section = dest[dest.Count - 1];
            IniItem item = null;

            try
            {
                item = new IniItem(name);
            }
            catch (Exception ex)
            {
                throw new IniFormatException(
                    lineNumber,
                    @"The name of the item (in the section """ +
                    section.Name +
                    @""") is invalid.",
                    ex);
            }

            try
            {
                item.Value = value;
            }
            catch (Exception ex)
            {
                throw new IniFormatException(
                    lineNumber,
                    @"The value of the item (""" +
                    item.Name +
                    @""" in the section """ +
                    section.Name +
                    @""") is invalid.",
                    ex);
            }

            try
            {
                section.Items.Add(item);
            }
            catch (Exception ex)
            {
                throw new IniFormatException(
                    lineNumber,
                    @"The item name (""" +
                    item.Name +
                    @""" in the section """ +
                    section.Name +
                    @""") is duplicated.",
                    ex);
            }
        }
    }
}
