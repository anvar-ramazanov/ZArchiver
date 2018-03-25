using System;
using System.Collections.Generic;
using System.Threading;

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
        public Int32 ReadedBlock { get; set; }
        /// <summary>
        /// Количество свободных потоков
        /// </summary>
        public Int32 FreeThreads { get; set; }
        /// <summary>
        /// Флаг ошибки
        /// </summary>
        public Boolean HasError { get; set; }

        public Queue<FileBlock> BlocksQueue { get; set; }

        private object _blockQueue = new object();

        public FileBlock PopBlock()
        {
            lock (_blockQueue)
            {
                while (BlocksQueue.Count == 0)
                    Monitor.Wait(_blockQueue);

                return BlocksQueue.Dequeue();
            }
        }

        public void PushBlock(FileBlock block)
        {
            lock (_blockQueue)
            {
                BlocksQueue.Enqueue(block);
                Monitor.PulseAll(_blockQueue);
            }
        }

        public Int32 WritedBlocks { get; set; }

        public QueueState(int currentBlockIndex, int preparedBlocks, int freeThreads)
        {
            CurrentBlockIndex = currentBlockIndex;
            ReadedBlock = preparedBlocks;
            FreeThreads = freeThreads;
            WritedBlocks = 0;
            BlocksQueue = new Queue<FileBlock>();
            HasError = false;
        }
    }
}
