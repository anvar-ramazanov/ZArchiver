using System;

namespace ZArchiver
{
    /// <summary>
    /// Хранит состояние очереди потоков
    /// </summary>
    public class QueueState
    {
        /// <summary>
        /// Текущий индекс блока
        /// </summary>
        public Int32 CurrentBlockIndex { get; set; }
        /// <summary>
        /// Количество обработанных блоков
        /// </summary>
        public Int32 PreparedBlocks { get; set; }
        /// <summary>
        /// Количество свободных потоков
        /// </summary>
        public Int32 FreeThreads { get; set; }
        /// <summary>
        /// Флаг ошибки
        /// </summary>
        public Boolean HasError { get; set; }

        public QueueState(int currentBlockIndex, int preparedBlocks, int freeThreads)
        {
            CurrentBlockIndex = currentBlockIndex;
            PreparedBlocks = preparedBlocks;
            FreeThreads = freeThreads;
            HasError = false;
        }
    }
}
