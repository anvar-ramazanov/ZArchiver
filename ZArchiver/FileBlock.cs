using System;

namespace ZArchiver
{
    /// <summary>
    /// Хранит информацию о блоках
    /// </summary>
    public class FileBlock
    {
        /// <summary>
        /// Размер блока в байтах
        /// </summary>
        public Int32 Size { get; private set; }
        /// <summary>
        /// Смещение блока в прочитанном файле
        /// </summary>
        public Int64 ReadOffset { get; private set; }
        /// <summary>
        /// Смещение в записанном файле
        /// </summary>
        public Int64 WriteOffset { get; private set; }
        /// <summary>
        /// Данные блока
        /// </summary>
        public byte[] Data { get; set; }

        /// <summary>
        /// Флаг ошибки
        /// </summary>
        public Boolean HasError { get; set; }

        public FileBlock(int blockSize, long readOffset)
        {
            Size = blockSize;
            ReadOffset = readOffset;
        }

        public FileBlock(int blockSize, long readOffset, long writeOffset)
        {
            Size = blockSize;
            ReadOffset = readOffset;
            WriteOffset = writeOffset;
        }
    }
}
