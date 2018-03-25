using System;
using System.Collections.Generic;
using System.Threading;

namespace ZArchiver
{
    public class BlocksQueue
    {
        /// <summary>
        /// Количество обработанных блоков
        /// </summary>
        public Int32 PreparedBlocks { get; set; }

        /// <summary>
        /// Общее количество блоков
        /// </summary>
        public Int32 TotalBlocksCount { get; set; }

        public Boolean IsStoped { get; private set; }

        public FileBlock PopBlock()
        {
            lock (_blockQueue)
            {
                while (_data.Count == 0)
                    Monitor.Wait(_blockQueue);

                return _data.Dequeue();
            }
        }

        public void PushBlock(FileBlock block)
        {
            lock (_blockQueue)
            {
                _data.Enqueue(block);
                Monitor.PulseAll(_blockQueue);
            }
        }

        public void Stop()
        {
            IsStoped = true;
        }

        public BlocksQueue(int totalBlocksCount)
        {
            PreparedBlocks = 0;
            TotalBlocksCount = totalBlocksCount;
            _data = new Queue<FileBlock>();
        }

        private object _blockQueue = new object();

        private Queue<FileBlock> _data { get; set; }
    }
}
