using System.Threading;
using NLog;

namespace ZArchiver
{
    public abstract class ZArchiver
    {
        public ZArchiver(int blockSize, int maxThreadCount)
        {
            _blockSize = blockSize;
            _maxThreadCount = maxThreadCount;
        }

        protected int GetBlocksSize(int blocksCount)
        {
            return blocksCount *  sizeof(long) * 2 + sizeof(int);
        }

        protected abstract void ReadBlock(string path, FileBlock block);

        protected void DoJob(string filename, string outputfile)
        {
            var readQueue = GetReadQueue(filename);
            var writeQueue = new BlocksQueue(readQueue.TotalBlocksCount);

            var writerThread = new Thread(() =>
            {
                Write(writeQueue, outputfile);
            });
            writerThread.Start();

            var maxThreadForRead = _maxThreadCount - 1;
            var semaphore = new Semaphore(maxThreadForRead, maxThreadForRead);
            while (readQueue.PreparedBlocks < readQueue.TotalBlocksCount && !readQueue.IsStoped && semaphore.WaitOne())
            {
                var readThread = new Thread(() =>
                {
                    var block = readQueue.PopBlock();
                    ReadBlock(filename, block);
                    if (block.HasError)
                    {
                        readQueue.Stop();
                        writeQueue.Stop();
                    }
                    else
                    {
                        writeQueue.PushBlock(block);
                    }

                    semaphore.Release();
                });
                readThread.Start();
                readQueue.PreparedBlocks++;
            }
        }

        public abstract void Launch(string filename, string outputFile);

        protected abstract BlocksQueue GetReadQueue(string path);

        protected abstract void Write(BlocksQueue writeQueue, string outputFile);

        protected ILogger _logger;

        protected readonly int _blockSize;
        protected readonly int _maxThreadCount;
    }
}
