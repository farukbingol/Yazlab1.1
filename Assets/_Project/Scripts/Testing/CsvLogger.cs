using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Yazlab.Testing
{
    /// <summary>
    /// Basit CSV yazıcı: dosya başlığını bir kez yazar, her çağrıda tek satır ekler.
    /// Sayılar kültürden bağımsız (nokta ondalık ayırıcı) biçimlenir; dosya hataları loglanır, oyunu durdurmaz.
    /// </summary>
    public sealed class CsvLogger
    {
        private const char Separator = ',';

        private readonly int _columnCount;
        private readonly StringBuilder _lineBuilder = new StringBuilder(256);

        /// <summary>Yazılan dosyanın tam yolu.</summary>
        public string FilePath { get; }

        /// <summary>Dosya açılıp başlık yazılabildiyse <c>true</c>; aksi halde satırlar sessizce atlanır.</summary>
        public bool IsValid { get; private set; }

        /// <param name="filePath">Hedef dosyanın tam yolu.</param>
        /// <param name="header">Sütun adları.</param>
        /// <param name="append"><c>true</c> ise mevcut dosyanın sonuna ekler (başlık yalnız dosya yoksa yazılır).</param>
        public CsvLogger(string filePath, IReadOnlyList<string> header, bool append = false)
        {
            FilePath = filePath;
            _columnCount = header != null ? header.Count : 0;

            try
            {
                string directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                if (!append || !File.Exists(filePath))
                {
                    File.WriteAllText(filePath, BuildLine(header), Encoding.UTF8);
                }

                IsValid = true;
            }
            catch (Exception e)
            {
                IsValid = false;
                Debug.LogError($"[CsvLogger] '{filePath}' oluşturulamadı: {e.Message}");
            }
        }

        /// <summary>Tek satır ekler. Alan sayısı başlıktan farklıysa uyarı verir ama satırı yine yazar.</summary>
        public void AppendRow(IReadOnlyList<string> fields)
        {
            if (!IsValid || fields == null)
            {
                return;
            }

            if (fields.Count != _columnCount)
            {
                Debug.LogWarning($"[CsvLogger] Alan sayısı ({fields.Count}) başlıkla ({_columnCount}) uyuşmuyor.");
            }

            try
            {
                File.AppendAllText(FilePath, BuildLine(fields), Encoding.UTF8);
            }
            catch (Exception e)
            {
                Debug.LogError($"[CsvLogger] '{FilePath}' dosyasına yazılamadı: {e.Message}");
            }
        }

        /// <summary>Ondalık sayıyı kültürden bağımsız biçimler (ör. <c>12.35</c>).</summary>
        public static string Format(float value, string format = "F2")
        {
            return value.ToString(format, CultureInfo.InvariantCulture);
        }

        /// <summary>Alanı gerekiyorsa tırnaklar (virgül, tırnak veya satır sonu içeriyorsa).</summary>
        public static string Escape(string field)
        {
            if (string.IsNullOrEmpty(field))
            {
                return string.Empty;
            }

            bool needsQuotes = field.IndexOf(Separator) >= 0 || field.IndexOf('"') >= 0 ||
                               field.IndexOf('\n') >= 0 || field.IndexOf('\r') >= 0;
            return needsQuotes ? "\"" + field.Replace("\"", "\"\"") + "\"" : field;
        }

        private string BuildLine(IReadOnlyList<string> fields)
        {
            _lineBuilder.Clear();
            if (fields != null)
            {
                for (int i = 0; i < fields.Count; i++)
                {
                    if (i > 0)
                    {
                        _lineBuilder.Append(Separator);
                    }

                    _lineBuilder.Append(Escape(fields[i]));
                }
            }

            _lineBuilder.Append('\n');
            return _lineBuilder.ToString();
        }
    }
}
