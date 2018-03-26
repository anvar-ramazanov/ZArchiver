using NLog;
using System;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace ZArchiver
{
    public class Compressor : ZArchiver
    {
        public Compressor(int blockSize, int maxThreadCount) : base(blockSize, maxThreadCount)
        {
            _logger = LogManager.GetCurrentClassLogger();
        }

        public override void Launch(string filename, string outputFile)
        {
            try
            {
                _logger.Info("Start compressing file {0}", filename);
                _writeDataOffset = 0;
                _witeHeaderOffset = 0;
                Compress(filename, outputFile);
            }
            catch (Exception e)
            {
                _logger.Error(e, string.Format("An error occurred while compressing the file {0}", filename));
            }
        }

        private void Compress(string filename, string outputfile)
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

        protected override BlocksQueue GetReadQueue(string path)
        {
            var fileInfo = new FileInfo(path);
            double fileSize = fileInfo.Length / (_blockSize * 1f);
            var blocksCount = (int)Math.Ceiling(fileSize);
            var blocksQueue = new BlocksQueue(blocksCount);
            for (int i = 0; i < blocksCount - 1; ++i)
            {
                blocksQueue.PushBlock(new FileBlock(_blockSize, i * _blockSize));
            }

            var size = (int)(fileInfo.Length - (blocksCount - 1) * _blockSize);
            var offset = (blocksCount - 1) * _blockSize;
            blocksQueue.PushBlock(new FileBlock(size, offset));
            return blocksQueue;
        }

        protected override void Write(BlocksQueue writeQueue, string outputFile)
        {
            WriteHeader(writeQueue, outputFile);

            while (writeQueue.PreparedBlocks < writeQueue.TotalBlocksCount && !writeQueue.IsStoped)
            {
                try
                {
                    var block = writeQueue.PopBlock();
                    WriteBlock(outputFile, block);
                    writeQueue.PreparedBlocks++;
                }
                catch (Exception e)
                {
                    writeQueue.Stop();
                    _logger.Error(e, "An error occurred while writing block of compressed file");
                }
            }

            _logger.Info(string.Format("File compressed"));
        }

        private void ReadBlock(string path, FileBlock block)
        {
            try
            {
                using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read))
                using (var stream = new MemoryStream())
                {
                    using (var gzip = new GZipStream(stream, CompressionMode.Compress))
                    {
                        fileStream.Position = block.ReadOffset;
                        var buffer = new byte[block.Size];
                        fileStream.Read(buffer, 0, block.Size);
                        gzip.Write(buffer, 0, block.Size);
                    }
                    block.Data = stream.ToArray();
                }
            }
            catch (Exception e)
            {
                block.HasError = true;
                _logger.Error(e, string.Format("An error occurred while preparing block (offset = {0}, size = {1}) of compressed file", block.ReadOffset, block.Size));
            }
        }

        private void WriteHeader(BlocksQueue writeQueue, string outputFile)
        {
            using (var outStream = new FileStream(outputFile, FileMode.OpenOrCreate, FileAccess.Write))
            using (var binaryWriter = new BinaryWriter(outStream))
            {
                binaryWriter.Write(writeQueue.TotalBlocksCount);
                _witeHeaderOffset += sizeof(int);
                _writeDataOffset += (writeQueue.TotalBlocksCount * 2 + 1) * sizeof(int);
            }
        }

        private void WriteBlock(string file, FileBlock block)
        {
            using (var outStream = new FileStream(file, FileMode.OpenOrCreate, FileAccess.Write))
            using (var binaryWriter = new BinaryWriter(outStream))
            {
                outStream.Position = _witeHeaderOffset;
                binaryWriter.Write(block.Data.Length);
                binaryWriter.Write((int)block.ReadOffset);
                _witeHeaderOffset += sizeof(int) * 2;
                outStream.Position = _writeDataOffset;
                binaryWriter.Write(block.Data);
                _writeDataOffset += block.Data.Length;
                binaryWriter.Flush();
            }
        }

        private int _writeDataOffset;
        private int _witeHeaderOffset;
    }
}
