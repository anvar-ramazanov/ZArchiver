using System;

namespace ZArchiver
{
    /// <summary>
    /// Хранит информацию о блоках
    /// </summary>
    public class BlockInfo
    {
        /// <summary>
        /// Название файла
        /// </summary>
        public String FileName { get; private set; }
        /// <summary>
        /// Размер блока в байтах
        /// </summary>
        public Int32 Size { get; private set; }
        /// <summary>
        /// Смещение блока относительно начала файла
        /// </summary>
        public Int64 Offset { get; private set; }

        public BlockInfo(string filename, int blockSize, long offset)
        {
            FileName = filename;
            Size = blockSize;
            Offset = offset;
        }
    }
}
