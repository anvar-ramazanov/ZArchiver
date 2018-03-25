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

        public abstract void Launch(string filename, string outputFile);

        protected abstract BlocksQueue GetReadQueue(string path);

        protected abstract void Write(BlocksQueue writeQueue, string outputFile);

        protected ILogger _logger;

        protected readonly int _blockSize;
        protected readonly int _maxThreadCount;
    }
}
